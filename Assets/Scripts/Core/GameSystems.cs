using TMPro;
using UnityEngine;

/// <summary>
/// The one object that survives scene changes. Owns the screen fade and the
/// HUD, and gives every other script a single place to reach them.
///
/// A "GameSystems" object with this component (plus a RadioController) sits in
/// each scene; the second copy destroys itself in Awake. If a scene is ever
/// missing it, Bootstrap() recreates it from code.
/// </summary>
[DisallowMultipleComponent]
public class GameSystems : MonoBehaviour
{
    public static GameSystems Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Header("Scene fade")]
    [SerializeField] private float fadeDuration = 0.45f;

    [Header("HUD")]
    [Tooltip("Font for every HUD label. Use the Cairo SDF asset so Arabic text (radio track names, clues) shows correctly.")]
    [SerializeField] private TMP_FontAsset hudFont;

    public SceneFader Fader { get; private set; }
    public HUDController HUD { get; private set; }
    public RadioController Radio { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        Debug.Log("[GameSystems] No scene copy found - creating one from code.");
        GameObject go = new GameObject("GameSystems");
        go.AddComponent<RadioController>();
        go.AddComponent<GameSystems>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Radio = GetComponent<RadioController>();
        if (Radio == null) Radio = gameObject.AddComponent<RadioController>();

        BuildFade();
        BuildHud();

        GameState.Changed += OnStateChanged;
    }

    private void Start()
    {
        if (Fader != null) Fader.FadeIn();
        if (HUD != null) HUD.Refresh();
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        GameState.Changed -= OnStateChanged;
        Instance = null;
    }

    private void OnStateChanged()
    {
        if (HUD != null) HUD.Refresh();
    }

    // ------------------------------------------------------------------ UI --

    private void BuildFade()
    {
        GameObject canvasGO = NewCanvas("Fader", 900);

        GameObject imageGO = NewRect("FadeImage", canvasGO.transform);
        UnityEngine.UI.Image image = imageGO.AddComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        CanvasGroup canvasGroup = imageGO.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Fader = canvasGO.AddComponent<SceneFader>();
        Fader.Setup(canvasGroup, fadeDuration);
    }

    private void BuildHud()
    {
        GameObject canvasGO = NewCanvas("HUD", 800);
        HUD = canvasGO.AddComponent<HUDController>();
        HUD.Build(hudFont);
    }

    private GameObject NewCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        UnityEngine.UI.CanvasScaler scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        return go;
    }

    private static GameObject NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
