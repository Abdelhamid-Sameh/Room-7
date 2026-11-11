using System.Collections;
using UnityEngine;

/// <summary>
/// The black-and-white film on the cinema screen: a shadow-puppet play (no filming, bodies only). A hidden stage far
/// below the room holds a pale backdrop and black puppet figures made of capsules; an orthographic camera renders it
/// into a RenderTexture that the screen shows, with film flicker and a vignette on top. The play follows the length of
/// the music clip: two figures meet, he reaches for her, she turns away, he is left alone and a flower grows.
///
/// Everything is procedural so the film needs no art assets.
/// </summary>
public class ShadowFilm : MonoBehaviour
{
    [SerializeField] private Renderer screen;          // the screen quad (set up by Room4Builder)
    [SerializeField] private AudioClip music;          // the lyricless track
    [SerializeField] private Light[] roomLights;       // dimmed while the film plays
    [SerializeField] private Unity.Cinemachine.CinemachineCamera filmView;   // a seat in the middle of the room, facing the screen

    public static bool Active;
    public bool Playing { get; private set; }
    public bool Finished { get; private set; }

    private Transform stage;
    private Camera cam;
    private RenderTexture rt;
    private Material screenMat, flickerMat;
    private Figure he, she;
    private Transform flower, bloom;
    private float[] lightBase;
    private Color restEmission;

    private class Figure
    {
        public Transform root, torso, head, armL, armR, legL, legR;
    }

    // --------------------------------------------------------------- build --

    private static Material Black() { Material m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", Color.black); return m; }

    private Transform Limb(Transform parent, string n, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        g.name = n; Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g.transform;
    }

    private Figure MakeFigure(string name, float height, Material m, bool dress)
    {
        Figure f = new Figure();
        f.root = new GameObject(name).transform; f.root.SetParent(stage, false);
        float s = height / 1.8f;
        f.torso = Limb(f.root, "Torso", PrimitiveType.Capsule, new Vector3(0f, 1.05f * s, 0f), new Vector3(0.42f * s, 0.42f * s, 0.2f * s), m);
        if (dress) f.torso.localScale = new Vector3(0.5f * s, 0.55f * s, 0.2f * s);
        f.head = Limb(f.root, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.62f * s, 0f), new Vector3(0.26f * s, 0.3f * s, 0.2f * s), m);
        // arms and legs hang from pivots so they can swing
        // limbs hinge just inside the torso outline so the silhouette stays joined when they swing
        float shoulder = dress ? 0.2f : 0.17f;
        f.armL = Pivot(f.root, "ArmL", new Vector3(-shoulder * s, 1.36f * s, 0f), m, 0.13f * s, 0.34f * s);
        f.armR = Pivot(f.root, "ArmR", new Vector3(shoulder * s, 1.36f * s, 0f), m, 0.13f * s, 0.34f * s);
        f.legL = Pivot(f.root, "LegL", new Vector3(-0.09f * s, 0.78f * s, 0f), m, 0.17f * s, 0.4f * s);
        f.legR = Pivot(f.root, "LegR", new Vector3(0.09f * s, 0.78f * s, 0f), m, 0.17f * s, 0.4f * s);
        Limb(f.root, "Neck", PrimitiveType.Capsule, new Vector3(0f, 1.47f * s, 0f), new Vector3(0.1f * s, 0.1f * s, 0.1f * s), m);
        return f;
    }

    private Transform Pivot(Transform parent, string n, Vector3 at, Material m, float thick, float halfLen)
    {
        Transform p = new GameObject(n).transform; p.SetParent(parent, false); p.localPosition = at;
        Limb(p, "Seg", PrimitiveType.Capsule, new Vector3(0f, -halfLen, 0f), new Vector3(thick, halfLen, thick), m);
        return p;
    }

    private void BuildStage()
    {
        stage = new GameObject("__ShadowStage").transform;
        stage.position = new Vector3(0f, -500f, 0f);

        GameObject camGo = new GameObject("Cam"); camGo.transform.SetParent(stage, false);
        camGo.transform.localPosition = new Vector3(0f, 1.15f, -6f);   // ground about a fifth of the way up the frame
        cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.6f; cam.aspect = 2f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.82f, 0.8f, 0.75f);
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        cam.cullingMask = ~0;
        rt = new RenderTexture(1024, 512, 16, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var data = camGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (data == null) data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;

        Material black = Black();
        // ground line
        Limb(stage, "Ground", PrimitiveType.Cube, new Vector3(0f, -0.5f, 1f), new Vector3(12f, 1f, 1f), black);   // solid black below the floor line
        he = MakeFigure("He", 1.8f, black, false);
        she = MakeFigure("She", 1.65f, black, true);

        // a narcissus: stem, petals, a dark cup
        flower = new GameObject("Flower").transform; flower.SetParent(stage, false);
        flower.localPosition = new Vector3(2.2f, 0f, 0f);
        Limb(flower, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f), new Vector3(0.03f, 0.3f, 0.03f), black);
        bloom = new GameObject("Bloom").transform; bloom.SetParent(flower, false); bloom.localPosition = new Vector3(0f, 0.62f, 0f);
        for (int i = 0; i < 6; i++)
        {
            Transform p = Limb(bloom, "Petal", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.1f, 0.22f, 0.04f), black);
            p.localRotation = Quaternion.Euler(0f, 0f, i * 60f); p.localPosition = p.localRotation * Vector3.up * 0.13f;
        }
        Limb(bloom, "Cup", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.12f, 0.12f, 0.06f), black);
        flower.localScale = Vector3.zero;

        // pale pool under the flower (the pond he stared into)
        Limb(stage, "Pond", PrimitiveType.Sphere, new Vector3(2.2f, 0.0f, 0.6f), new Vector3(1.8f, 0.04f, 0.5f), black);
    }

    private void BuildScreenOverlay()
    {
        screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        screenMat.SetTexture("_BaseMap", rt);
        screenMat.SetColor("_BaseColor", Color.white);
        screen.sharedMaterial = screenMat;
    }

    // ---------------------------------------------------------------- play --

    public void Play()
    {
        if (Playing || Finished) return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Playing = true; Active = true;
        if (stage == null) { BuildStage(); BuildScreenOverlay(); }

        MonoBehaviour ctrl = PauseMenu.FindPlayerController();
        if (ctrl != null) ctrl.enabled = false;
        if (filmView != null) filmView.Priority.Value = 50;   // the camera glides to a seat and watches the screen
        SetHud(false);
        if (GameSystems.HasInstance && GameSystems.Instance.Radio != null) GameSystems.Instance.Radio.enabled = false;
        GameState.RadioPlaying = false; GameState.RaiseChanged();

        lightBase = new float[roomLights.Length];
        for (int i = 0; i < roomLights.Length; i++) lightBase[i] = roomLights[i].intensity;

        AudioSource src = gameObject.AddComponent<AudioSource>();
        src.clip = music; src.spatialBlend = 0f; src.volume = AudioLevels.AmbientVolume * 1.2f; src.Play();
        AudioSource clatter = ProjectorSounds.Loop(gameObject, ProjectorSounds.K.Motor, 0.12f);   // the projector running behind the audience
        float len = music != null ? music.length : 120f;

        // dim the room
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            for (int i = 0; i < roomLights.Length; i++) roomLights[i].intensity = Mathf.Lerp(lightBase[i], lightBase[i] * 0.04f, t / 1.5f);
            yield return null;
        }

        float clock = 0f;
        while (clock < len)
        {
            clock += Time.deltaTime;
            Animate(clock / len, clock);
            // film flicker: the whole picture breathes a little
            float f = 0.86f + 0.1f * Mathf.PerlinNoise(clock * 14f, 3f) + (Random.value < 0.005f ? -0.12f : 0f);
            screenMat.SetColor("_BaseColor", new Color(f, f, f, 1f));
            yield return null;
        }

        // end: fade to black, lights back
        for (float t = 0f; t < 2f; t += Time.deltaTime)
        {
            screenMat.SetColor("_BaseColor", Color.Lerp(Color.white * 0.8f, Color.black, t / 2f));
            src.volume = Mathf.Lerp(AudioLevels.AmbientVolume * 1.2f, 0f, t / 2f);
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
        if (stage != null) Destroy(stage.gameObject);
        if (rt != null) rt.Release();
    }

    // ---------------------------------------------------------- the story --
    // k = 0..1 through the film. Figures are posed from k so scrubbing is stable.

    private static float Seg(float k, float a, float b) { return Mathf.Clamp01((k - a) / (b - a)); }
    private static float Ease(float t) { return t * t * (3f - 2f * t); }

    private void Walk(Figure f, float phase, float amount)
    {
        float s = Mathf.Sin(phase) * 28f * amount;
        f.legL.localRotation = Quaternion.Euler(0f, 0f, s); f.legR.localRotation = Quaternion.Euler(0f, 0f, -s);
        f.armL.localRotation = Quaternion.Euler(0f, 0f, -s * 0.8f); f.armR.localRotation = Quaternion.Euler(0f, 0f, s * 0.8f);
    }

    private void Animate(float k, float time)
    {
        // The frame is 6.4 m wide (x -3.2..3.2). Both figures are in shot from the first second.
        // 0-.08 they walk towards each other | .08-.30 together, laughing, he talks with his hands | .30-.40 he reaches
        // .40-.48 she turns away | .48-.62 she walks off | .62-.78 he is alone, head down | .78-.88 to the pond, kneels
        // .88-1 the flower grows
        float meet = Ease(Seg(k, 0f, 0.08f));
        float leave = Ease(Seg(k, 0.48f, 0.62f));
        float hx = Mathf.Lerp(-2.6f, -0.55f, meet);
        float sx = Mathf.Lerp(2.6f, 0.55f, meet) + leave * 3.6f;
        he.root.localPosition = new Vector3(hx, 0f, 0f);
        she.root.localPosition = new Vector3(sx, 0f, 0f);

        bool walkingIn = k < 0.08f, sheWalks = k > 0.48f && k < 0.62f;
        Walk(he, time * 5f, walkingIn ? 1f : 0f);
        Walk(she, time * 5f + 1.2f, walkingIn || sheWalks ? 1f : 0f);

        // together: a laugh bob, he talks with his hands, she tilts her head towards him
        bool together = k > 0.08f && k < 0.30f;
        float bob = together ? Mathf.Abs(Mathf.Sin(time * 6f)) * 0.04f : 0f;
        he.torso.localPosition = new Vector3(0f, 1.05f + bob, 0f);
        she.torso.localPosition = new Vector3(0f, 0.92f * 1.05f + bob * 0.7f, 0f);
        if (together)
        {
            he.armR.localRotation = Quaternion.Euler(0f, 0f, -35f - 25f * Mathf.Sin(time * 3f));
            she.head.localRotation = Quaternion.Euler(0f, 0f, 10f * Mathf.Sin(time * 1.3f));
        }

        // he reaches for her
        float reach = Ease(Seg(k, 0.30f, 0.38f)) * (1f - Ease(Seg(k, 0.50f, 0.56f)));
        if (!together) he.armR.localRotation = Quaternion.Euler(0f, 0f, -100f * reach);
        // she turns her back on him
        she.root.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 180f, Ease(Seg(k, 0.40f, 0.47f))), 0f);

        // alone, head down
        float down = Ease(Seg(k, 0.62f, 0.70f));
        he.head.localRotation = Quaternion.Euler(0f, 0f, 22f * down);
        he.head.localPosition = new Vector3(0.05f * down, 1.62f - 0.07f * down, 0f);
        he.torso.localRotation = Quaternion.Euler(0f, 0f, 6f * down);

        // to the pond, then kneels
        if (k > 0.78f)
        {
            float toPond = Ease(Seg(k, 0.78f, 0.86f));
            he.root.localPosition = new Vector3(Mathf.Lerp(-0.55f, 1.4f, toPond), -0.5f * Ease(Seg(k, 0.86f, 0.9f)), 0f);
            Walk(he, time * 4f, k < 0.86f ? 1f : 0f);
        }
        float grow = Ease(Seg(k, 0.88f, 1f));
        flower.localScale = Vector3.one * grow;
        bloom.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.5f) * 4f);
    }
}
