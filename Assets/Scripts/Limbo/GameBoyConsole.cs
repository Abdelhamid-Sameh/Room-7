using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The big red Game Boy. E walks up to it: the camera cuts to the screen and WASD / arrow keys steer a snake
/// through a maze (see SnakeMazeGame). Eat all four apples, then leave through the gap in the east wall to win -
/// the door in the console's side swings open. Esc or Q steps back (a run in progress is kept).
///
/// Sound slots: every clip below is optional and empty for now - drop the files in on the GameBoy object.
/// </summary>
public class GameBoyConsole : Interactable
{
    public static bool Playing { get; private set; }
    public static int ClosedFrame { get; private set; } = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Playing = false; ClosedFrame = -1; }

    [Header("Parts")]
    [SerializeField] private Renderer screen;
    [SerializeField] private CinemachineCamera closeUp;
    [SerializeField] private GameBoyDoor door;

    [Header("Sound slots (all optional)")]
    [Tooltip("Played when Nabil starts playing.")]
    [SerializeField] private AudioClip powerOnSound;
    [Tooltip("Every step the snake takes - keep it tiny.")]
    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip eatSound;
    [Tooltip("Hit a wall or yourself.")]
    [SerializeField] private AudioClip crashSound;
    [Tooltip("Out of the maze.")]
    [SerializeField] private AudioClip winSound;
    [Tooltip("Plays all the time, coming out of the console (spatial: you hear it through the wall too).")]
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;
    [Tooltip("The music pauses while the crash / win sound plays, and this scales the win sound.")]
    [Range(0f, 1f)] [SerializeField] private float winVolume = 0.5f;

    [Header("Game")]
    [Tooltip("Seconds per step. Lower = faster.")]
    [SerializeField] private float stepSeconds = 0.17f;

    private SnakeMazeGame game;
    private Texture2D tex;
    private Color32[] pixels;
    private AudioSource musicSrc;
    private MonoBehaviour player;
    private float stepTimer, nextDraw, deadUntil;
    private bool busy, flashing;
    private int enteredFrame;

    public override string Prompt { get { return "Play the Game Boy"; } }
    public override bool CanInteract { get { return !Playing && !busy && !GameState.GameBoySolved; } }

    private void Awake()
    {
        game = new SnakeMazeGame();
        pixels = new Color32[SnakeMazeGame.ScreenW * SnakeMazeGame.ScreenH];
        tex = new Texture2D(SnakeMazeGame.ScreenW, SnakeMazeGame.ScreenH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        if (screen != null)
        {
            Material m = screen.material;   // an instance, so the asset keeps no runtime picture
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
        }

        game.OnMove += () => { if (moveSound != null) AudioLevels.PlaySfx(moveSound, transform.position); };
        game.OnEat += () => { if (eatSound != null) AudioLevels.PlaySfx(eatSound, transform.position); };
        game.OnCrash += () =>
        {
            musicSrc.Pause();
            if (crashSound != null) AudioLevels.PlaySfx(crashSound, transform.position);
            deadUntil = Time.time + 0.9f;
        };
        game.OnWin += () =>
        {
            musicSrc.Pause();
            if (winSound != null) AudioLevels.PlaySfx(winSound, transform.position, winVolume);
            StartCoroutine(Win());
        };

        musicSrc = NewSource("Music", true, musicClip);
    }

    private AudioSource NewSource(string n, bool spatial, AudioClip clip)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        if (spatial) { s.minDistance = 3f; s.maxDistance = 25f; }
        return s;
    }

    private IEnumerator Start()
    {
        if (closeUp != null) closeUp.Priority.Value = 0;
        Camera cam = Camera.main;
        CinemachineBrain brain = cam != null ? cam.GetComponent<CinemachineBrain>() : null;
        if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.9f);

        Redraw();
        while (!GameState.LimboLit) yield return null;   // nothing plays in the dark
        if (musicSrc.clip != null) musicSrc.Play();
    }

    private void Update()
    {
        musicSrc.volume = musicVolume * AudioLevels.MusicVolume;

        if (Playing && !game.Won)
        {
            ReadKeys();
            if (game.Started && !game.Dead)
            {
                stepTimer += Time.deltaTime;
                while (stepTimer >= stepSeconds) { stepTimer -= stepSeconds; game.Step(); }
            }
            else if (game.Dead && Time.time >= deadUntil) { game.Reset(); stepTimer = 0f; musicSrc.UnPause(); }
        }

        if (Time.time >= nextDraw) Redraw();
    }

    private void Redraw()
    {
        nextDraw = Time.time + 1f / 24f;
        bool flash = flashing || (game.Dead && (int)(Time.time * 10f) % 2 == 0);
        game.Render(pixels, Time.time, flash);
        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    private void ReadKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || Time.frameCount == enteredFrame) return;

        if (kb.escapeKey.wasPressedThisFrame || kb.qKey.wasPressedThisFrame) { StartCoroutine(Leave(false)); return; }

        if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) game.Turn(SnakeMazeGame.Up);
        else if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) game.Turn(SnakeMazeGame.Down);
        else if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) game.Turn(SnakeMazeGame.Left);
        else if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) game.Turn(SnakeMazeGame.Right);
    }

    public override void Interact(PlayerInteraction interactor)
    {
        if (Playing || busy) return;
        Playing = true;
        enteredFrame = Time.frameCount;
        stepTimer = 0f;

        player = PauseMenu.FindPlayerController();
        if (player != null) player.enabled = false;
        if (closeUp != null) closeUp.Priority.Value = 50;
        if (powerOnSound != null) AudioLevels.PlaySfx(powerOnSound, transform.position);
    }

    private IEnumerator Win()
    {
        flashing = true;
        yield return new WaitForSeconds(1.6f);
        flashing = false;
        GameState.GameBoySolved = true;
        GameState.RaiseChanged();
        yield return Leave(true);
    }

    private IEnumerator Leave(bool openDoor)
    {
        if (!Playing) yield break;
        Playing = false;
        ClosedFrame = Time.frameCount;
        busy = true;
        if (closeUp != null) closeUp.Priority.Value = 0;

        yield return new WaitForSeconds(0.9f);
        musicSrc.UnPause();
        PauseMenu.ClearStaleInput();
        if (player != null) player.enabled = true;
        player = null;
        busy = false;

        if (openDoor && door != null)
        {
            yield return new WaitForSeconds(0.4f);
            door.Open();
        }
    }

    private void OnDestroy()
    {
        if (Playing) { Playing = false; if (player != null) player.enabled = true; }
        if (tex != null) Destroy(tex);
    }
}
