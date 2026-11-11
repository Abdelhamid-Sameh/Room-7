using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// The black-and-white film on the cinema screen. A VideoPlayer renders the clip into a RenderTexture that the screen
/// quad shows, with a little projector flicker on top. While it plays the camera moves to a seat (Cinemachine), the
/// room lights dim, the song plays and the player is locked; Space skips after a few seconds. When the clip ends the
/// screen fades to black, control returns and the song is unlocked on the radio.
/// (Class name kept from the earlier shadow-puppet version so the scene and the projector keep working.)
/// </summary>
public class ShadowFilm : MonoBehaviour
{
    [SerializeField] private Renderer screen;          // the screen quad (set up by Room4Builder)
    [SerializeField] private AudioClip music;          // the lyricless track
    [SerializeField] private Light[] roomLights;       // dimmed while the film plays
    [SerializeField] private Unity.Cinemachine.CinemachineCamera filmView;   // a seat in the middle of the room, facing the screen
    [SerializeField] private VideoClip clip;           // Assets/Room4/Film/BreakupAtPond.mp4

    public static bool Active;
    public bool Playing { get; private set; }
    public bool Finished { get; private set; }

    private VideoPlayer vp;
    private RenderTexture rt;
    private Material screenMat;
    private float[] lightBase;

    private void BuildScreen()
    {
        rt = new RenderTexture(clip.width > 0 ? (int)clip.width : 1072, clip.height > 0 ? (int)clip.height : 720, 0, RenderTextureFormat.ARGB32);
        vp = gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false; vp.isLooping = false; vp.skipOnDrop = true;
        vp.source = VideoSource.VideoClip; vp.clip = clip;
        vp.renderMode = VideoRenderMode.RenderTexture; vp.targetTexture = rt;
        vp.audioOutputMode = VideoAudioOutputMode.None;   // the song is the soundtrack

        screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        screenMat.SetTexture("_BaseMap", rt);
        screenMat.SetColor("_BaseColor", Color.black);
        screen.sharedMaterial = screenMat;

        // the clip is not as wide as the screen quad: keep the height, narrow the width so nothing is stretched
        Vector3 s = screen.transform.localScale;
        screen.transform.localScale = new Vector3(s.y * rt.width / rt.height, s.y, s.z);
    }

    public void Play()
    {
        if (Playing || Finished) return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Playing = true; Active = true;
        if (clip == null) { Debug.LogError("ShadowFilm: no video clip assigned"); Playing = false; Active = false; yield break; }
        if (vp == null) BuildScreen();
        vp.Prepare();
        float wait = 0f;
        while (!vp.isPrepared && wait < 8f) { wait += Time.deltaTime; yield return null; }

        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;
        if (filmView != null) filmView.Priority.Value = 50;   // the camera glides to a seat and watches the screen
        SetHud(false);
        if (GameSystems.HasInstance && GameSystems.Instance.Radio != null) GameSystems.Instance.Radio.enabled = false;
        GameState.RadioPlaying = false; GameState.RaiseChanged();

        lightBase = new float[roomLights.Length];
        for (int i = 0; i < roomLights.Length; i++) lightBase[i] = roomLights[i].intensity;

        float len = (float)clip.length;
        float musicVol = AudioLevels.AmbientVolume * 1.2f;
        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = music; src.spatialBlend = 0f; src.volume = musicVol; src.Play();
        AudioSource clatter = ProjectorSounds.Loop(gameObject, ProjectorSounds.K.Motor, 0.12f);   // the projector running behind the audience

        // dim the room
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            for (int i = 0; i < roomLights.Length; i++) roomLights[i].intensity = Mathf.Lerp(lightBase[i], lightBase[i] * 0.04f, t / 1.5f);
            yield return null;
        }

        vp.Play();
        PhoneUI.Hint("Space", "skip the film");
        float clock = 0f;
        while (clock < len + 2f && (clock < 1f || vp.isPlaying))
        {
            clock += Time.deltaTime;
            // projector flicker: the picture breathes a little
            float f = 0.9f + 0.08f * Mathf.PerlinNoise(clock * 14f, 3f) + (Random.value < 0.004f ? -0.1f : 0f);
            screenMat.SetColor("_BaseColor", new Color(f, f, f, 1f));
            // the song only lasts as long as the film: ease it out over the last four seconds
            src.volume = musicVol * Mathf.Clamp01((len - clock) / 4f);
            if (clock > 3f && SkipPressed()) break;
            yield return null;
        }
        vp.Stop();

        // end: fade to black, lights back
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            screenMat.SetColor("_BaseColor", Color.Lerp(Color.white * 0.8f, Color.black, t / 2f));
            src.volume = Mathf.Lerp(src.volume, 0f, t / 2f);
            for (int i = 0; i < roomLights.Length; i++) roomLights[i].intensity = Mathf.Lerp(lightBase[i] * 0.04f, lightBase[i], t / 2f);
            yield return null;
        }
        for (int i = 0; i < roomLights.Length; i++) roomLights[i].intensity = lightBase[i];
        Destroy(src); Destroy(clatter);

        if (filmView != null) filmView.Priority.Value = 0;
        SetHud(true);
        yield return new WaitForSeconds(1.2f);   // let the camera blend back before handing control over
        if (ctrl != null) ctrl.enabled = true;
        if (GameSystems.HasInstance && GameSystems.Instance.Radio != null) GameSystems.Instance.Radio.enabled = true;
        Playing = false; Active = false; Finished = true;
        StoryStateManager.SetFlag(StoryStateManager.Flags.Room4Film);
        GameState.UnlockedTracks.Add("Room04");
        GameState.RaiseChanged();
        Sfx.Play(SfxKind.Ping);
        PhoneUI.Hint("New track for the radio", "إن كنت غالى - عايدة الأيوبي");
    }

    private static bool SkipPressed()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    /// <summary>Task box and reticle off while the film plays (same approach as the hanging room).</summary>
    private static void SetHud(bool on)
    {
        HUDController h = FindFirstObjectByType<HUDController>();
        if (h == null) return;
        foreach (Canvas c in h.GetComponentsInChildren<Canvas>(true)) c.enabled = on;
        Canvas parent = h.GetComponentInParent<Canvas>();
        if (parent != null) parent.enabled = on;
    }

    private void OnDestroy()
    {
        Active = false;
        if (rt != null) rt.Release();
    }
}
