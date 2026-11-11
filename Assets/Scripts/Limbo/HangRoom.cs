using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The hanging room. Sits on the trigger in the doorway of the room at the end of the corridor.
///
/// Before the trigger: the door behind Nabil slams shut once he has walked a few steps in, and the room (a rope, a chair
/// on its side, one tired lamp) can be seen through the door ahead.
/// After it: Nabil freezes. The lamp flickers and in each dark moment he is hanging from the rope, gone again when the
/// light comes back. Messages from the unknown number pile onto the screen with the voice clips from Audio/Hang, faster
/// and faster, until the screen closes in and he passes out. He wakes up in the hotel corridor (see WakeUpFx).
/// </summary>
public class HangRoom : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private Light lamp;
    [SerializeField] private Renderer bulb;
    [SerializeField] private Light coldFill;            // a faint cold light that only shows in the dark moments
    [SerializeField] private FlickerLight idleFlicker;  // the lamp's gentle flicker before the sequence takes over
    [SerializeField] private Transform hangPivot;       // at the ceiling; the body swings from here
    [SerializeField] private GameObject hanged;
    [SerializeField] private Transform slamHinge;       // the door behind the player
    [SerializeField] private float slamAtZ = 3.2f;
    [SerializeField] private float lampBase = 2.6f;

    [Header("Audio")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private AudioClip ambience, heartbeat;
    [SerializeField] private AudioClip drone;   // dark chord pad under everything
    [SerializeField] private AudioClip voiceCalm, voiceSarcastic, voiceAngry, voiceDie;
    [SerializeField] private AudioClip laugh1, laugh2, laughLoud;

    private const float Length = 38f;       // seconds from the shock to the blackout

    private Transform player;
    private bool slammed, started, ending;
    private float clock, fear;
    private AudioSource voiceSrc, beatSrc, ambSrc, bedA, bedB, droneSrc;
    private MaterialPropertyBlock block;
    private Quaternion hangBase;
    private float swaySeed;

    private RectTransform shakeRoot;
    private Image vignette, black, red;
    private readonly List<GameObject> pops = new List<GameObject>();

    // ------------------------------------------------------------------ setup --

    private void Start()
    {
        block = new MaterialPropertyBlock();
        swaySeed = Random.value * 50f;
        if (hanged != null) hanged.SetActive(false);
        if (coldFill != null) coldFill.enabled = false;
        if (hangPivot != null) hangBase = hangPivot.localRotation;

        voiceSrc = NewSource("Voice", null, false);
        beatSrc = NewSource("Heartbeat", heartbeat, true);
        ambSrc = NewSource("Ambience", ambience, true);
        beatSrc.volume = 0f;
        ambSrc.volume = 0.35f * AudioLevels.AmbientVolume;
        if (ambSrc.clip != null) ambSrc.Play();
        droneSrc = NewSource("Drone", drone, true);
        droneSrc.volume = 0.5f * AudioLevels.AmbientVolume;
        droneSrc.volume = 0f;   // fades in when the sequence starts (the mirror room shares this scene)
    }

    private AudioSource NewSource(string n, AudioClip clip, bool loop)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = clip; s.loop = loop; s.spatialBlend = 0f; s.playOnAwake = false;
        return s;
    }

    private Transform Player()
    {
        if (player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
        return player;
    }

// the corridor and room are built in a frame (rotated to run out of the mirror room's east door)
    private Transform Frame() { return transform.parent != null ? transform.parent : transform; }


    // ------------------------------------------------------------------ frame --

    private void Update()
    {
        Transform p = Player();
        if (!slammed && p != null && Frame().InverseTransformPoint(p.position).z > slamAtZ) { slammed = true; StartCoroutine(Slam()); }

        if (hangPivot != null && hanged != null && hanged.activeSelf)
        {
            float t = Time.time + swaySeed;
            hangPivot.localRotation = hangBase * Quaternion.Euler(Mathf.Sin(t * 1.1f) * 2.2f, Mathf.Sin(t * 0.6f) * 9f, Mathf.Sin(t * 0.9f) * 1.6f);
        }

        if (!started) return;
        clock += Time.deltaTime;
        fear = Mathf.Clamp01(clock / Length);

        if (shakeRoot != null)
        {
            float amp = fear * fear * 16f;
            shakeRoot.anchoredPosition = new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp));
        }
        if (vignette != null) vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(fear * fear * 1.15f));
        if (!ending && beatSrc != null)
        {
            beatSrc.volume = Mathf.Lerp(0.15f, 0.95f, fear) * AudioLevels.AmbientVolume;
            beatSrc.pitch = Mathf.Lerp(0.95f, 1.5f, fear);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (started || other.GetComponentInParent<CharacterController>() == null) return;
        started = true;
        StartCoroutine(Run());
    }

    // ------------------------------------------------------- the door behind --

    private IEnumerator Slam()
    {
        if (slamHinge == null) yield break;
        yield return new WaitForSeconds(0.35f);
        Quaternion from = slamHinge.localRotation, to = Quaternion.identity;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.14f)
        {
            slamHinge.localRotation = Quaternion.Slerp(from, to, t * t);
            yield return null;
        }
        slamHinge.localRotation = to;
        LimboDoor ld = slamHinge.GetComponent<LimboDoor>();
        if (ld != null) ld.Locked = true;   // no way back
        AudioClip shut = Resources.Load<AudioClip>("Sfx/Door_Close");   // a door closing, not the opening sound
        if (shut != null) AudioSource.PlayClipAtPoint(shut, slamHinge.position, AudioLevels.SfxVolume);
        else Sfx.Play(SfxKind.Door);
    }

    // ------------------------------------------------------------ the sequence --

    private struct Beat
    {
        public float At; public string Text; public AudioClip Voice; public bool Big;
        public Beat(float at, string text, AudioClip voice, bool big = false) { At = at; Text = text; Voice = voice; Big = big; }
    }

    private static readonly string[] Filler =
    {
        "كنت فين ؟", "ليه سبتني ؟", "فاكرني ؟", "حجر", "سلم", "مبسوط معاها ؟",
        "رفضتك", "يا بائس", "انت ولا حاجه", "يا ضعيف", "انا نسيتك"
    };

private IEnumerator Run()
    {
        Freeze();
        HideHud();
        StartCoroutine(LampLoop());
        if (beatSrc != null && beatSrc.clip != null) beatSrc.Play();
        BuildOverlay();
        if (droneSrc != null && droneSrc.clip != null) { droneSrc.Play(); StartCoroutine(Lerp(v => droneSrc.volume = v * AudioLevels.AmbientVolume, 0f, 0.5f, 6f)); }
        bedA = NewSource("LaughBedA", laugh1, true); bedA.volume = 0f; bedA.Play();
        bedB = NewSource("LaughBedB", laugh2, true); bedB.volume = 0f; bedB.pitch = 0.92f;

        AudioClip[] order = { voiceCalm, voiceSarcastic, voiceAngry, voiceDie };
        int said = 0;
        float nextVoice = 1.8f, nextPop = 4f;
        bool loudDone = false, bedBOn = false;

        while (clock < Length)
        {
            // the laughter never stops: it is the room's background and only gets louder
            float bed = AudioLevels.SfxVolume;
            if (bedA != null) bedA.volume = Mathf.Lerp(0.1f, 0.5f, fear) * Mathf.Clamp01(clock / 3f) * bed;
            if (!bedBOn && clock > 11f) { bedBOn = true; if (bedB != null) bedB.Play(); }
            if (bedB != null && bedBOn) bedB.volume = Mathf.Lerp(0.1f, 0.5f, fear) * bed;

            // lines overlap: the next one starts well before the last has finished, faster as it goes
            if (clock >= nextVoice)
            {
                AudioClip c = said < order.Length ? order[said] : (Random.value < 0.3f ? voiceDie : order[Random.Range(0, 3)]);
                said++;
                Voice(c);
                Pop(c == voiceDie ? "انت هتموت هنا" : (c == voiceAngry ? "لسه مش فاكر اسمى ؟!" : "لسه مش فاكر اسمى ؟"), true);
                nextVoice = clock + Mathf.Lerp(2.2f, 0.7f, fear) * Random.Range(0.8f, 1.25f);
            }
            if (clock >= nextPop)
            {
                float k = Mathf.Clamp01(clock / Length);
                nextPop = clock + Mathf.Lerp(1.3f, 0.12f, k);
                Pop(Random.value < 0.25f ? "انت هتموت هنا" : Filler[Random.Range(0, Filler.Length)], Random.value < 0.2f);
            }
            if (!loudDone && clock > 27f)
            {
                loudDone = true;
                Voice(laughLoud);
                StartCoroutine(RedFlash());
            }
            yield return null;
        }

        // the screen closes in; he goes down
        ending = true;
        StartCoroutine(Lerp(v => { if (beatSrc != null) beatSrc.volume = v * AudioLevels.AmbientVolume; }, 0.95f, 0f, 2.5f));
        StartCoroutine(Lerp(v => { if (droneSrc != null) droneSrc.volume = v * AudioLevels.AmbientVolume; }, 0.5f, 0f, 2.5f));
        StartCoroutine(Lerp(v => { if (ambSrc != null) ambSrc.volume = v * AudioLevels.AmbientVolume; }, 0.35f, 0f, 2.5f));
        StartCoroutine(Lerp(v => { if (bedA != null) bedA.volume = v * AudioLevels.SfxVolume; if (bedB != null) bedB.volume = v * AudioLevels.SfxVolume; }, 0.5f, 0f, 2.5f));
        yield return Lerp(a => { if (black != null) black.color = new Color(0f, 0f, 0f, a); }, 0f, 1f, 2.2f);
        if (lamp != null) lamp.intensity = 0f;
        Sfx.Play(SfxKind.Door, 0.5f);   // the thud of the floor
        yield return new WaitForSeconds(2.4f);

        GameState.HangDone = true;
        GameState.Limbo = false;
        GameState.LimboLit = false;
        GameState.WakeUpPending = true;
        ShowHud();   // the sequence switched the HUD canvases off; Nabil is back in the hotel
        GameState.RaiseChanged();
        if (GameSystems.HasInstance) GameSystems.Instance.Fader.LoadScene("HotelFloor_Corridor", GameState.SpawnStart);
        else UnityEngine.SceneManagement.SceneManager.LoadScene("HotelFloor_Corridor");
    }

    private void Freeze()
    {
        Transform p = Player();
        if (p == null) return;
        foreach (MonoBehaviour mb in p.GetComponentsInChildren<MonoBehaviour>())
        {
            string n = mb.GetType().Name;
            if (n == "FirstPersonController" || n == "PlayerInteraction" || n == "CarrySystem") mb.enabled = false;
        }
        StartCoroutine(TurnToRope(p));
    }

    private IEnumerator TurnToRope(Transform p)
    {
        Quaternion from = p.rotation, to = Frame().rotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)
        {
            p.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
    }

    // ------------------------------------------------------------------- lamp --

    private IEnumerator LampLoop()
    {
        if (idleFlicker != null) idleFlicker.enabled = false;
        SetLamp(1f);
        yield return new WaitForSeconds(1.6f);
        while (!ending)
        {
            float lit = Mathf.Lerp(1.5f, 0.22f, fear) * Random.Range(0.6f, 1.4f);
            float dark = Mathf.Lerp(0.16f, 0.42f, fear) * Random.Range(0.6f, 1.3f);
            // he is there in the dark and gone in the light
            SetLamp(0f);
            if (hanged != null) hanged.SetActive(true);
            if (coldFill != null) coldFill.enabled = true;
            yield return new WaitForSeconds(dark);
            SetLamp(1f);
            if (hanged != null) hanged.SetActive(false);
            if (coldFill != null) coldFill.enabled = false;
            if (Random.value < 0.35f) { yield return new WaitForSeconds(0.05f); SetLamp(0.2f); yield return new WaitForSeconds(0.06f); SetLamp(1f); }
            yield return new WaitForSeconds(lit);
        }
    }

    private void SetLamp(float level)
    {
        if (lamp != null) lamp.intensity = lampBase * level * Random.Range(0.92f, 1.05f);
        if (bulb != null)
        {
            block.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.5f) * (2.2f * level));
            bulb.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------------- overlay --

    private Sprite radial;

    private Sprite RadialSprite()
    {
        if (radial != null) return radial;
        const int n = 128;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x / (float)(n - 1) - 0.5f) * 2f, dy = (y / (float)(n - 1) - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0.25f, 1.25f, r)));
            }
        t.Apply();
        radial = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return radial;
    }

    private void BuildOverlay()
    {
        GameObject go = new GameObject("HangOverlay", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        Canvas c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 40;
        CanvasScaler s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f); s.matchWidthOrHeight = 0.5f;

        red = PhoneUI.Img("Red", go.transform, new Color(0.6f, 0f, 0f, 0f)); StretchFull(red); red.raycastTarget = false;

        GameObject sh = new GameObject("Shake", typeof(RectTransform));
        sh.transform.SetParent(go.transform, false);
        shakeRoot = (RectTransform)sh.transform;
        shakeRoot.anchorMin = Vector2.zero; shakeRoot.anchorMax = Vector2.one; shakeRoot.offsetMin = shakeRoot.offsetMax = Vector2.zero;

        vignette = PhoneUI.Img("Vignette", go.transform, new Color(0f, 0f, 0f, 0f));
        vignette.sprite = RadialSprite(); StretchFull(vignette); vignette.raycastTarget = false;

        black = PhoneUI.Img("Black", go.transform, new Color(0f, 0f, 0f, 0f)); StretchFull(black); black.raycastTarget = false;
    }

    /// <summary>The task box and the reticle have no business on screen while this happens.</summary>
    private static void HideHud()
    {
        HUDController h = FindFirstObjectByType<HUDController>();
        if (h == null) return;
        foreach (Canvas c in h.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        Canvas parent = h.GetComponentInParent<Canvas>();
        if (parent != null) parent.enabled = false;
    }

    private static void ShowHud()
    {
        HUDController h = FindFirstObjectByType<HUDController>();
        if (h == null) return;
        foreach (Canvas c in h.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
        Canvas parent = h.GetComponentInParent<Canvas>();
        if (parent != null) parent.enabled = true;
    }

    private static void StretchFull(Graphic g)
    {
        RectTransform rt = g.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private void Pop(string text, bool big)
    {
        if (shakeRoot == null) return;
        Sfx.Play(SfxKind.Ping);

        float scale = Mathf.Lerp(1f, 1.5f, fear) * (big ? 1.25f : 1f);
        float w = 560f * scale, h = 118f * scale;
        Image bg = PhoneUI.Img("Msg", shakeRoot, new Color(0.06f, 0.06f, 0.08f, 0.96f), true);
        RectTransform rt = bg.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        float mx = 960f - w / 2f - 20f, my = 540f - h / 2f - 20f;
        rt.anchoredPosition = new Vector2(Random.Range(-mx, mx), Random.Range(-my, my));
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-7f, 7f));

        TMP_Text from = PhoneUI.Txt(bg.transform, font, "From", 20f * scale, TextAlignmentOptions.Left, new Color(0.6f, 0.62f, 0.7f), "Unknown number");
        PhoneUI.Place(from, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f * scale, -8f * scale), new Vector2(w - 30f, 28f * scale));
        TMP_Text body = PhoneUI.Txt(bg.transform, font, "Body", 42f * scale, TextAlignmentOptions.Center, Color.white, text);
        PhoneUI.Place(body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f * scale), new Vector2(w - 24f, h - 40f * scale));
        from.raycastTarget = false; body.raycastTarget = false; bg.raycastTarget = false;

        pops.Add(bg.gameObject);
        if (pops.Count > 30) { if (pops[0] != null) Destroy(pops[0]); pops.RemoveAt(0); }
        StartCoroutine(PopIn(rt));
    }

    private IEnumerator PopIn(RectTransform rt)
    {
        for (float t = 0f; t < 1f && rt != null; t += Time.deltaTime / 0.12f)
        {
            rt.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }

    private IEnumerator RedFlash()
    {
        for (int i = 0; i < 6 && red != null; i++)
        {
            red.color = new Color(0.6f, 0f, 0f, 0.35f);
            yield return new WaitForSeconds(0.09f);
            red.color = new Color(0.6f, 0f, 0f, 0f);
            yield return new WaitForSeconds(0.2f);
        }
    }

private void Voice(AudioClip clip)
    {
        if (clip == null) return;
        // one fresh source per line so lines overlap instead of queueing; pitch and pan vary so repeats do not sound alike
        AudioSource s = NewSource("Line", clip, false);
        s.pitch = Random.Range(0.9f, 1.1f);
        s.panStereo = Random.Range(-0.6f, 0.6f);
        s.volume = Mathf.Clamp01(AudioLevels.SfxVolume) * Random.Range(0.8f, 1f);
        s.Play();
        Destroy(s.gameObject, clip.length / s.pitch + 0.2f);
    }

    private static IEnumerator Lerp(System.Action<float> set, float a, float b, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            set(Mathf.Lerp(a, b, t));
            yield return null;
        }
        set(b);
    }
}

/// <summary>After the hanging room Nabil wakes up in the hotel corridor: the screen opens in two heavy blinks.</summary>
public static class WakeUpFx
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
    }

    private static void OnLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) { Check(); }

    private static void Check()
    {
        if (!GameState.WakeUpPending) return;
        GameState.WakeUpPending = false;
        new GameObject("WakeUp").AddComponent<WakeUpRunner>();
    }

    private class WakeUpRunner : MonoBehaviour
    {
        private Image veil;

        private IEnumerator Start()
        {
            GameObject go = new GameObject("WakeUpCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 960;
            veil = PhoneUI.Img("Veil", go.transform, Color.black);
            veil.raycastTarget = false;
            RectTransform rt = veil.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;

            yield return new WaitForSecondsRealtime(1.2f);
            yield return Fade(1f, 0.55f, 1.1f);
            yield return Fade(0.55f, 0.97f, 0.3f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Fade(0.97f, 0.2f, 1.2f);
            yield return Fade(0.2f, 0.7f, 0.25f);
            yield return Fade(0.7f, 0f, 1.6f);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float a, float b, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                veil.color = new Color(0f, 0f, 0f, Mathf.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            veil.color = new Color(0f, 0f, 0f, b);
        }
    }
}
