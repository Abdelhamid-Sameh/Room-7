using System.Collections;
using UnityEngine;

/// <summary>
/// The mirror room (past the Game Boy). A glass wall splits it in two; behind the glass stands Nabil's copy in a
/// black-tie suit. The copy is Nabil's point reflection through the middle of the room: walk forward and it walks
/// towards you, step left and it steps right. If the copy is blocked by something on its side, so are you.
/// Each Nabil has a plate to stand on; with both pressed for a moment the copy stops mirroring, turns to look at
/// you, and the exit door unlocks.
/// </summary>
public class MirrorRoom : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Transform copy;
    [SerializeField] private Animator copyAnimator;
    [Tooltip("The world point the player is reflected through (middle of the room, on the floor).")]
    [SerializeField] private Vector3 pivot;
    [Tooltip("Things on the copy's side that block it (and so block you).")]
    [SerializeField] private Collider[] copyBlockers;
    [SerializeField] private float copyRadius = 0.3f;
    [SerializeField] private Transform plateYou, plateCopy;
    [SerializeField] private Renderer glowYou, glowCopy;
    [SerializeField] private float plateRadius = 0.55f;
    [SerializeField] private LimboDoor exitDoor;
    [SerializeField] private Renderer lockLight;
    [SerializeField] private GameObject roomLights;
    [Tooltip("Game Boy room lights: switched off once Nabil is in here so they do not shine through the walls.")]
    [SerializeField] private GameObject gameBoyLights;
    [SerializeField] private Transform copySideCentre;

    [Header("Where the room starts (world z)")]
    [SerializeField] private float enterFromZ = 22.2f;
    [SerializeField] private float enterToZ = 24.0f;
    [SerializeField] private float insideZ = 23.3f;

    [Header("Look")]
    [SerializeField] private Color fogColor = new Color(0.06f, 0.06f, 0.065f);
    [SerializeField] private float fogDensity = 0.012f;
    [SerializeField] private Color ambientColor = new Color(0.17f, 0.16f, 0.15f);
    [SerializeField] private float reflectionIntensity = 0.8f;
    [SerializeField] private Cubemap reflection;

    [Header("Sound slots")]
    [SerializeField] private AudioClip musicBox;
    [SerializeField] private AudioClip drone;
    [SerializeField] private AudioClip plateClick;
    [SerializeField] private AudioClip sting;
    [SerializeField] private AudioClip footsteps;
    [SerializeField] private AudioClip knock;
    [Range(0f, 1f)] [SerializeField] private float musicBoxVolume = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float droneVolume = 0.35f;

    private enum Stage { Mirroring, Finale, Done }

    // what the corridor / Game Boy room had before we took over (LimboCorridor reads Blend to stay out of the way)
    private static readonly Color RoomFog = new Color(0.02f, 0.03f, 0.07f), RoomAmbient = new Color(0.035f, 0.04f, 0.07f);
    private const float RoomDensity = 0.025f;

    public float Blend { get; private set; }
    public bool CanKnock { get { return stage == Stage.Mirroring && inside; } }

    private Stage stage;
    private Transform player;
    private CharacterController cc;
    private AudioSource musicSrc, droneSrc, stepSrc;
    private Material matYou, matCopy;
    private Vector3 lastGood, lastCopy;
    private bool hasGood, inside;
    private float bothTimer, smoothSpeed, glowA, glowB;
    private bool litOnce;
    private static readonly int SpeedP = Animator.StringToHash("Speed");
    private static readonly int RateP = Animator.StringToHash("WalkRate");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (copy != null) copy.gameObject.SetActive(false);
        if (copyAnimator != null) copyAnimator.applyRootMotion = false;
        if (roomLights != null) roomLights.SetActive(false);
        if (glowYou != null) matYou = glowYou.material;
        if (glowCopy != null) matCopy = glowCopy.material;
        if (exitDoor != null) exitDoor.Locked = !GameState.MirrorSolved;

        musicSrc = NewSource("MusicBox", copySideCentre != null ? copySideCentre : transform, musicBox, true, 2f, 16f);
        droneSrc = NewSource("Drone", transform, drone, false, 1f, 10f);
        stepSrc = NewSource("CopySteps", copy != null ? copy : transform, footsteps, true, 2f, 12f);
        if (copy != null && stepSrc != null) { stepSrc.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1600f; }   // through the glass
    }

    private AudioSource NewSource(string n, Transform parent, AudioClip clip, bool spatial, float min, float max)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(parent, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        if (spatial) { s.minDistance = min; s.maxDistance = max; }
        s.volume = 0f;
        return s;
    }

    private Vector3 Reflect(Vector3 p) { return new Vector3(2f * pivot.x - p.x, pivot.y, 2f * pivot.z - p.z); }

    private bool Free(Vector3 p)
    {
        Vector3 c = Reflect(p);
        foreach (Collider col in copyBlockers)
        {
            if (col == null) continue;
            Bounds b = col.bounds;
            if (c.x > b.min.x - copyRadius && c.x < b.max.x + copyRadius && c.z > b.min.z - copyRadius && c.z < b.max.z + copyRadius) return false;
        }
        return true;
    }

    private void Teleport(Vector3 pos)
    {
        pos.y = player.position.y;
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform; cc = p.GetComponent<CharacterController>();
        }

        Vector3 pos = player.position;
        Blend = Mathf.InverseLerp(enterFromZ, enterToZ, pos.z);
        ApplyLook();
        inside = pos.z > insideZ;
        if (copy != null && copy.gameObject.activeSelf != inside) copy.gameObject.SetActive(inside);

        if (stage == Stage.Mirroring && inside)
        {
            if (hasGood && !Free(pos))
            {
                // the copy is blocked on its side: slide along it if one axis is free, otherwise stay put
                Vector3 a = new Vector3(pos.x, pos.y, lastGood.z), b = new Vector3(lastGood.x, pos.y, pos.z);
                if (Free(a)) pos = a; else if (Free(b)) pos = b; else pos = lastGood;
                Teleport(pos);
            }
            lastGood = pos; hasGood = true;
            DriveCopy(pos);
            CheckPlates(pos);
        }
        else if (stage != Stage.Mirroring && copy != null)
        {
            AnimateCopy(0f);
        }
        UpdateGlow();
    }

    private void ApplyLook()
    {
        if (Blend <= 0f) return;
        if (!litOnce)
        {
            litOnce = true;
            if (roomLights != null) roomLights.SetActive(true);
            if (reflection != null) { RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = reflection; }
            musicSrc.Play(); droneSrc.Play();
        }
        if (Blend > 0.6f && gameBoyLights != null && gameBoyLights.activeSelf) gameBoyLights.SetActive(false);

        RenderSettings.fogColor = Color.Lerp(RoomFog, fogColor, Blend);
        RenderSettings.fogDensity = Mathf.Lerp(RoomDensity, fogDensity, Blend);
        RenderSettings.ambientLight = Color.Lerp(RoomAmbient, ambientColor, Blend);
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.05f, reflectionIntensity, Blend);

        float fade = stage == Stage.Mirroring ? 1f : musicFade;
        musicSrc.volume = musicBoxVolume * AudioLevels.MusicVolume * Blend * fade;
        droneSrc.volume = droneVolume * AudioLevels.AmbientVolume * Blend;
    }

    private float musicFade = 1f;

    private void DriveCopy(Vector3 pos)
    {
        if (copy == null) return;
        Vector3 c = Reflect(pos);
        float dt = Time.deltaTime;
        float speed = 0f;
        if (dt > 0f && copy.position != Vector3.zero)
        {
            Vector3 d = c - lastCopy; d.y = 0f;
            speed = d.magnitude > 1.5f ? 0f : d.magnitude / dt;
        }
        lastCopy = c;
        copy.position = c;
        Transform cam = Camera.main != null ? Camera.main.transform : player;
        copy.rotation = Quaternion.Euler(0f, cam.eulerAngles.y + 180f, 0f);
        AnimateCopy(speed);
    }

    private void AnimateCopy(float speed)
    {
        smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-12f * Mathf.Max(Time.deltaTime, 0.0001f)));
        if (copyAnimator != null)
        {
            copyAnimator.SetFloat(SpeedP, smoothSpeed);
            copyAnimator.SetFloat(RateP, Mathf.Clamp(smoothSpeed / 1.6f, 1f, 2.2f));
        }
        if (stepSrc != null && stepSrc.clip != null)
        {
            bool moving = smoothSpeed > 0.3f;
            if (moving && !stepSrc.isPlaying) stepSrc.Play();
            else if (!moving && stepSrc.isPlaying) stepSrc.Pause();
            stepSrc.volume = 0.5f * AudioLevels.SfxVolume;
        }
    }

    private void CheckPlates(Vector3 pos)
    {
        bool you = Flat(pos, plateYou.position) < plateRadius;
        bool him = Flat(Reflect(pos), plateCopy.position) < plateRadius;
        glowA = Mathf.MoveTowards(glowA, you ? 1f : 0.12f, Time.deltaTime * 4f);
        glowB = Mathf.MoveTowards(glowB, him ? 1f : 0.12f, Time.deltaTime * 4f);
        if (you && him) bothTimer += Time.deltaTime; else bothTimer = 0f;
        if (bothTimer > 0.8f) StartCoroutine(Finale());
    }

    private static float Flat(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }

    private void UpdateGlow()
    {
        // same soft amber pulse as the tidy-up items; pressed = steady and bright
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.2f);
        float idle = 0.9f + 0.9f * pulse;
        if (matYou != null) matYou.SetColor(Emission, new Color(1.0f, 0.72f, 0.28f) * Mathf.Lerp(idle, 3.2f, Mathf.InverseLerp(0.12f, 1f, glowA)));
        if (matCopy != null) matCopy.SetColor(Emission, new Color(1.0f, 0.72f, 0.28f) * Mathf.Lerp(idle, 3.2f, Mathf.InverseLerp(0.12f, 1f, glowB)));
    }

    public void Knock()
    {
        if (!CanKnock) return;
        AudioLevels.PlaySfx(knock, player.position + player.forward * 0.6f);
        StartCoroutine(KnockBack());
    }

    private IEnumerator KnockBack()
    {
        yield return new WaitForSeconds(0.9f);
        if (stage == Stage.Mirroring && copy != null) AudioLevels.PlaySfx(knock, copy.position, 0.45f);
    }

    private IEnumerator Finale()
    {
        if (stage != Stage.Mirroring) yield break;
        stage = Stage.Finale;
        GameState.MirrorSolved = true;
        glowA = glowB = 1f;
        AudioLevels.PlaySfx(plateClick, plateYou.position);
        AudioLevels.PlaySfx(plateClick, plateCopy.position);

        // the player stays free: the finale plays out around them
        // the music box winds down
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.4f) { musicFade = 1f - t; yield return null; }
        musicFade = 0f; musicSrc.Stop();
        yield return new WaitForSeconds(0.6f);

        // it stops copying you and turns to look
        AudioLevels.PlaySfx(sting, player.position);
        PhoneUI.MirrorSolved();
        Quaternion from = copy.rotation;
        Vector3 toYou = player.position - copy.position; toYou.y = 0f;
        Quaternion to = Quaternion.LookRotation(toYou.normalized, Vector3.up);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.6f) { copy.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t)); yield return null; }
        copy.rotation = to;
        yield return new WaitForSeconds(3.2f);

        if (exitDoor != null) exitDoor.Locked = false;
        if (lockLight != null) lockLight.material.SetColor(Emission, new Color(0.2f, 1f, 0.4f) * 2f);
        AudioLevels.PlaySfx(plateClick, exitDoor != null ? exitDoor.transform.position : player.position);
        stage = Stage.Done;
    }
}
