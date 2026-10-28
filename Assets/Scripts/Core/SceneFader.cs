using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen fade used between scenes. Built at runtime by GameSystems and
/// kept alive with DontDestroyOnLoad so the fade coroutine survives the scene
/// swap it is causing.
///
/// The actual scene load is asynchronous (SceneManager.LoadSceneAsync) so the
/// game never blocks on a long load while the screen just sits there - the
/// screen is already black by the time loading starts, and stays black until
/// loading is 100% done, then fades back in.
/// </summary>
public class SceneFader : MonoBehaviour
{
    private CanvasGroup group;
    private float duration = 0.45f;
    private bool busy;

    public bool IsBusy => busy;

    private CanvasGroup loadingText;
    private const float MinBlack = 0.9f;   // seconds the loading screen stays up, so a quick load never flashes

    public void Setup(CanvasGroup canvasGroup, float fadeDuration, CanvasGroup loading = null)
    {
        group = canvasGroup;
        loadingText = loading;
        duration = Mathf.Max(0.01f, fadeDuration);
    }

    /// <summary>Black -> clear. Called once when the game starts.</summary>
    public void FadeIn()
    {
        if (group == null) return;
        StopAllCoroutines();
        busy = false;
        StartCoroutine(Fade(1f, 0f));
    }

    /// <summary>Fade to black, load the scene, fade back in.</summary>
    public void LoadScene(string sceneName, string spawnId)
    {
        if (busy) return;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[SceneFader] Target scene name is empty.");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneFader] Scene '{sceneName}' is not in File > Build Settings (or is disabled there).");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName, spawnId));
    }

    /// <summary>
    /// The lights cut out: the screen snaps to black (no LOADING text), the scene loads behind it, a riser plays
    /// in the dark and then the lights flicker back on. 'onLit' runs the moment they do.
    /// </summary>
    public void LoadFromBlackout(string sceneName, string spawnId, AudioClip riser, System.Action onLit)
    {
        if (busy || group == null) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[SceneFader] Scene '{sceneName}' is not in File > Build Settings (or is disabled there).");
            return;
        }
        StartCoroutine(BlackoutRoutine(sceneName, spawnId, riser, onLit));
    }

    private IEnumerator BlackoutRoutine(string sceneName, string spawnId, AudioClip riser, System.Action onLit)
    {
        busy = true;
        try
        {
            if (loadingText != null) loadingText.alpha = 0f;
            // the lights die: a quick snap to black on screen (the scene switch happens behind it)
            for (float f = 0f; f < 0.35f; f += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Max(group.alpha, f / 0.35f);
                yield return null;
            }
            group.alpha = 1f;
            GameState.LastSpawnId = spawnId;

            float hold = riser != null ? Mathf.Clamp(riser.length - 0.4f, 2.5f, 12f) : 3f;
            if (riser != null)
            {
                GameObject go = new GameObject("Riser");
                DontDestroyOnLoad(go);
                AudioSource src = go.AddComponent<AudioSource>();
                src.spatialBlend = 0f;
                src.clip = riser;
                src.volume = Mathf.Clamp01(AudioLevels.SfxVolume);
                src.Play();
                Destroy(go, riser.length + 0.5f);
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            float held = 0f;
            while (op != null && !op.isDone) { held += Time.unscaledDeltaTime; yield return null; }
            for (int i = 0; i < 5; i++) { held += Time.unscaledDeltaTime; yield return null; }
            while (held < hold) { held += Time.unscaledDeltaTime; yield return null; }

            onLit?.Invoke();
            // a tired tube catching: on, off, on, flicker, on
            float[] levels = { 0.55f, 0f, 0.8f, 0.15f, 0.6f };
            float[] spans = { 0.07f, 0.10f, 0.08f, 0.06f, 0.12f };
            for (int i = 0; i < levels.Length; i++)
            {
                group.alpha = 1f - levels[i];
                for (float t = 0f; t < spans[i]; t += Time.unscaledDeltaTime) yield return null;
            }
            group.alpha = 0f;
        }
        finally
        {
            busy = false;
        }
    }

    private IEnumerator LoadRoutine(string sceneName, string spawnId)
    {
        busy = true;

        // try/finally (no catch) is legal around yield - this guarantees
        // 'busy' always clears and the screen never stays black forever,
        // even if something downstream (a spawn point lookup, a state
        // listener, ...) throws while the new scene is settling in.
        try
        {
            yield return Fade(group != null ? group.alpha : 0f, 1f);

            GameState.LastSpawnId = spawnId;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneFader] LoadSceneAsync('{sceneName}') returned null - scene not found.");
                yield break;
            }

            op.allowSceneActivation = true;
            float held = 0f;
            while (!op.isDone) { held += Time.unscaledDeltaTime; yield return null; }

            // Stay black until the new scene is really ready: a player camera exists, the spawn point has
            // been applied and a few frames have passed (this is what hid the stray corridor frame).
            for (int i = 0; i < 5; i++) { held += Time.unscaledDeltaTime; yield return null; }
            while (Camera.main == null && held < 5f) { held += Time.unscaledDeltaTime; yield return null; }
            while (held < MinBlack) { held += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(1f, 0f);
        }
        finally
        {
            busy = false;
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        if (group == null) yield break;

        float t = 0f;
        group.alpha = from;

        while (t < duration)
        {
            t += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            if (loadingText != null) loadingText.alpha = Mathf.Clamp01((group.alpha - 0.6f) * 2.5f);
            yield return null;
        }

        group.alpha = to;
        if (loadingText != null) loadingText.alpha = Mathf.Clamp01((to - 0.6f) * 2.5f);
    }
}
