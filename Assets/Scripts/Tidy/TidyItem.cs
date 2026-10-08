using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How an untidy thing gets put right.</summary>
public enum TidyMode
{
    /// <summary>It is out of place - press E and it moves back to where it belongs (chair, pillow, drawer, lamp...).</summary>
    Restore,

    /// <summary>It does not belong at all - press E and it is carried off to 'Dispose Target' and disappears (rubbish, laundry).</summary>
    Dispose,

    /// <summary>It is dirty - HOLD E and the marks fade away (smudged mirror).</summary>
    Hold
}

/// <summary>
/// One thing in a room that needs tidying. While it is untidy it pulses with a soft amber glow (brighter
/// when the player looks at it) and a small marker floats above it; tidying it removes both and counts
/// towards the room's progress bar.
///
/// Restore: the item sits in its UNTIDY pose in the scene. 'Tidy Position/Euler' is where it belongs
///          (local to its parent). Right-click the component for tools to capture and preview poses.
/// Dispose: the item flies to 'Dispose Target' (a bin, a closet...) and is hidden.
/// Hold:    'Fade On Hold' are the renderers (smudges) that fade out while the player holds E.
///
/// Whether an item is tidy is remembered in GameState, so leaving the room and coming back keeps it tidy.
/// </summary>
public class TidyItem : Interactable
{
    [Header("What it is")]
    [Tooltip("Unique within the room. Used to remember that it has been tidied.")]
    [SerializeField] private string itemId = "";
    [SerializeField] private string tidyPrompt = "Tidy up";
    [SerializeField] private TidyMode mode = TidyMode.Restore;

    [Header("Restore - where it belongs (local to the parent)")]
    [SerializeField] private Vector3 tidyPosition;
    [SerializeField] private Vector3 tidyEuler;
    [SerializeField] private float moveSeconds = 0.5f;

    [Header("Dispose - where it goes")]
    [SerializeField] private Transform disposeTarget;

    [Header("Hold - wiping")]
    [SerializeField] private float holdSeconds = 1.6f;
    [Tooltip("Renderers (smudges, dust) that fade away while the player holds the button.")]
    [SerializeField] private Renderer[] fadeOnHold;

    [Header("Soft body (optional) - e.g. a blanket")]
    [Tooltip("A SkinnedMeshRenderer with a blend shape that holds the crumpled, untidy shape. 100 = untidy, 0 = tidy. Put this item's pose offset to zero if the shape does all the work.")]
    [SerializeField] private SkinnedMeshRenderer softBody;
    [SerializeField] private int softShapeIndex = 0;

    [Header("Look")]
    [SerializeField] private Color glowColor = new Color(1f, 0.72f, 0.28f);
    [Tooltip("How strongly it glows while untidy. 0 = no glow.")]
    [SerializeField, Range(0f, 2f)] private float glowStrength = 0.4f;
    [SerializeField] private bool showMarker = true;
    [Tooltip("Height of the floating marker above the item (metres).")]
    [SerializeField] private float markerHeight = 0.22f;
    [SerializeField] private Sprite markerSprite;

    public bool IsTidy { get; private set; }
    public string ItemId => itemId;

    private string Key => (TidyRoom.Instance != null ? TidyRoom.Instance.RoomId : gameObject.scene.name) + "/" + itemId;

    // ---- Interactable ----
    public override string Prompt => tidyPrompt;
    public override string KeyHint => mode == TidyMode.Hold ? "hold E" : "E";
    public override float HoldDuration => mode == TidyMode.Hold ? holdSeconds : 0f;
    public override bool CanInteract => !IsTidy && !busy;

    // ---- glow ----
    private struct GlowSlot
    {
        public Material Mat;
        public Color BaseEmission;
        public bool HadKeyword;
    }

    private GlowSlot[] slots = new GlowSlot[0];
    private float glowLevel = 1f;
    private float phase;
    private bool busy;

    private Material[] fadeMaterials = new Material[0];
    private float[] fadeBaseAlpha = new float[0];

    private SpriteRenderer marker;
    private PlayerInteraction viewer;
    private Camera cam;

    // editor pose preview
    [SerializeField, HideInInspector] private Vector3 previewBackupPosition;
    [SerializeField, HideInInspector] private Vector3 previewBackupEuler;

    private void Awake()
    {
        phase = Random.value * 6.2831f;
        SetupGlow();
        SetupFade();
    }

    private void Start()
    {
        cam = Camera.main;
        viewer = FindFirstObjectByType<PlayerInteraction>();

        if (string.IsNullOrWhiteSpace(itemId)) itemId = gameObject.name;
        if (TidyRoom.Instance != null) TidyRoom.Instance.Register(this);

        if (GameState.TidiedItems.Contains(Key)) ApplyTidyInstantly();
        else
        {
            SetSoft(100f);
            if (showMarker && markerSprite != null) CreateMarker();
        }
    }

    private void SetSoft(float weight)
    {
        if (softBody != null && softBody.sharedMesh != null && softBody.sharedMesh.blendShapeCount > softShapeIndex)
            softBody.SetBlendShapeWeight(softShapeIndex, weight);
    }

    // ------------------------------------------------------------------ looks --

    private void SetupGlow()
    {
        var list = new List<GlowSlot>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is SpriteRenderer || IsFadeRenderer(r)) continue;

            Material[] mats = r.materials;   // instances, so only this item glows
            foreach (Material m in mats)
            {
                if (m == null || !m.HasProperty("_EmissionColor")) continue;

                GlowSlot s = new GlowSlot();
                s.Mat = m;
                s.HadKeyword = m.IsKeywordEnabled("_EMISSION");
                s.BaseEmission = s.HadKeyword ? m.GetColor("_EmissionColor") : Color.black;
                m.EnableKeyword("_EMISSION");
                list.Add(s);
            }
        }
        slots = list.ToArray();
    }

    private bool IsFadeRenderer(Renderer r)
    {
        if (fadeOnHold == null) return false;
        foreach (Renderer f in fadeOnHold) if (f == r) return true;
        return false;
    }

    private void SetupFade()
    {
        if (fadeOnHold == null) return;

        var mats = new List<Material>();
        var alphas = new List<float>();
        foreach (Renderer r in fadeOnHold)
        {
            if (r == null) continue;
            Material m = r.material;
            mats.Add(m);
            alphas.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : 1f);
        }
        fadeMaterials = mats.ToArray();
        fadeBaseAlpha = alphas.ToArray();
    }

    private void ApplyGlow(float strength)
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i].Mat.SetColor("_EmissionColor", slots[i].BaseEmission + glowColor * strength);
    }

    private void ResetGlowMaterials()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Mat.SetColor("_EmissionColor", slots[i].BaseEmission);
            if (!slots[i].HadKeyword) slots[i].Mat.DisableKeyword("_EMISSION");
        }
    }

    private void CreateMarker()
    {
        GameObject go = new GameObject("TidyMarker_" + itemId);
        go.transform.SetParent(transform.root, true);
        marker = go.AddComponent<SpriteRenderer>();
        marker.sprite = markerSprite;
        marker.color = new Color(1f, 0.82f, 0.45f, 0.9f);
        marker.sortingOrder = 10;
        marker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker.receiveShadows = false;
    }

    private Bounds WorldBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool has = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(false))
        {
            if (r is SpriteRenderer || !r.enabled) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return b;
    }

    private void Update()
    {
        if (slots.Length == 0 && marker == null) return;
        if (IsTidy && glowLevel <= 0.001f) return;

        glowLevel = Mathf.MoveTowards(glowLevel, IsTidy ? 0f : 1f, Time.deltaTime * 2.5f);

        bool looked = viewer != null && viewer.Current == this;
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.2f + phase);
        float strength = glowStrength * pulse * (looked ? 1.8f : 1f) * glowLevel;
        ApplyGlow(strength);

        if (IsTidy && glowLevel <= 0.001f) ResetGlowMaterials();
    }

    private void LateUpdate()
    {
        if (marker == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Bounds b = WorldBounds();
        Vector3 pos = new Vector3(b.center.x, b.max.y + markerHeight + 0.03f * Mathf.Sin(Time.time * 2f + phase), b.center.z);
        marker.transform.position = pos;
        marker.transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        // a little bigger when far away, so it can be spotted across the room
        float distance = Vector3.Distance(cam.transform.position, pos);
        float size = Mathf.Clamp(distance * 0.05f, 0.14f, 0.36f);
        marker.transform.localScale = Vector3.one * size;

        Color c = marker.color;
        c.a = (0.55f + 0.35f * Mathf.Sin(Time.time * 2.2f + phase)) * Mathf.Clamp01(glowLevel);
        marker.color = c;
    }

    // ------------------------------------------------------------ interaction --

    public override void Interact(PlayerInteraction interactor)
    {
        if (IsTidy || busy) return;

        switch (mode)
        {
            case TidyMode.Restore: StartCoroutine(RestoreRoutine()); break;
            case TidyMode.Dispose: StartCoroutine(DisposeRoutine()); break;
            case TidyMode.Hold: FinishHold(); break;
        }
    }

    public override void OnHold(float progress)
    {
        for (int i = 0; i < fadeMaterials.Length; i++)
        {
            if (!fadeMaterials[i].HasProperty("_BaseColor")) continue;
            Color c = fadeMaterials[i].GetColor("_BaseColor");
            c.a = fadeBaseAlpha[i] * (1f - progress);
            fadeMaterials[i].SetColor("_BaseColor", c);
        }
    }

    public override void OnHoldCancelled()
    {
        OnHold(0f);   // the smudge comes back if the player stops wiping
    }

    private IEnumerator RestoreRoutine()
    {
        busy = true;

        Vector3 p0 = transform.localPosition;
        Quaternion r0 = transform.localRotation;
        Vector3 p1 = tidyPosition;
        Quaternion r1 = Quaternion.Euler(tidyEuler);
        float w0 = softBody != null && softBody.sharedMesh != null && softBody.sharedMesh.blendShapeCount > softShapeIndex ? softBody.GetBlendShapeWeight(softShapeIndex) : 0f;

        // Things that have a long way to go take longer and hop a little, so the move can be followed by eye.
        float dist = transform.parent != null ? transform.parent.TransformVector(p1 - p0).magnitude : (p1 - p0).magnitude;
        float duration = Mathf.Clamp(moveSeconds + dist * 0.25f, moveSeconds, 1.4f);
        float hop = Mathf.Min(0.18f, dist * 0.12f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, duration);
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            transform.localPosition = Vector3.Lerp(p0, p1, e);
            transform.localRotation = Quaternion.Slerp(r0, r1, e);
            transform.position += Vector3.up * (Mathf.Sin(Mathf.PI * e) * hop);
            SetSoft(Mathf.Lerp(w0, 0f, e));
            yield return null;
        }

        SetSoft(0f);
        transform.localPosition = p1;
        transform.localRotation = r1;
        busy = false;
        MarkTidy();
    }

    private IEnumerator DisposeRoutine()
    {
        busy = true;

        Vector3 p0 = transform.position;
        Vector3 s0 = transform.localScale;
        Vector3 p1 = disposeTarget != null ? disposeTarget.position : p0 + Vector3.up * 0.5f;

        // Lifted, carried over in an arc, and only shrunk near the end - so it is seen travelling.
        float dist = Vector3.Distance(p0, p1);
        float duration = Mathf.Clamp(0.5f + dist * 0.22f, 0.7f, 1.5f);
        float arc = 0.25f + dist * 0.07f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            float shrink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, e));
            transform.position = Vector3.Lerp(p0, p1, e) + Vector3.up * (Mathf.Sin(Mathf.PI * e) * arc);
            transform.localScale = Vector3.Lerp(s0, s0 * 0.05f, shrink);
            yield return null;
        }

        busy = false;
        MarkTidy();
        gameObject.SetActive(false);
    }

    private void FinishHold()
    {
        if (fadeOnHold != null)
            foreach (Renderer r in fadeOnHold) if (r != null) r.enabled = false;
        MarkTidy();
    }

    private void MarkTidy()
    {
        IsTidy = true;
        GameState.TidiedItems.Add(Key);

        if (marker != null) { Destroy(marker.gameObject); marker = null; }
        if (TidyRoom.Instance != null) TidyRoom.Instance.NotifyChanged();
    }

    /// <summary>Coming back to a room: things already tidied are simply tidy.</summary>
    private void ApplyTidyInstantly()
    {
        IsTidy = true;
        glowLevel = 0f;
        ResetGlowMaterials();

        switch (mode)
        {
            case TidyMode.Restore:
                SetSoft(0f);
                transform.localPosition = tidyPosition;
                transform.localRotation = Quaternion.Euler(tidyEuler);
                break;

            case TidyMode.Dispose:
                gameObject.SetActive(false);
                break;

            case TidyMode.Hold:
                if (fadeOnHold != null)
                    foreach (Renderer r in fadeOnHold) if (r != null) r.enabled = false;
                break;
        }
    }

    // ------------------------------------------------------------ editor tools --

    [ContextMenu("Pose: capture CURRENT as the TIDY pose")]
    private void CaptureTidyPose()
    {
        tidyPosition = transform.localPosition;
        tidyEuler = transform.localEulerAngles;
    }

    [ContextMenu("Pose: preview the TIDY pose")]
    private void PreviewTidy()
    {
        previewBackupPosition = transform.localPosition;
        previewBackupEuler = transform.localEulerAngles;
        transform.localPosition = tidyPosition;
        transform.localEulerAngles = tidyEuler;
    }

    [ContextMenu("Pose: go back to the UNTIDY pose (after previewing)")]
    private void RestoreUntidy()
    {
        transform.localPosition = previewBackupPosition;
        transform.localEulerAngles = previewBackupEuler;
    }
}
