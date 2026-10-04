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

    public void Setup(CanvasGroup canvasGroup, float fadeDuration)
    {
        group = canvasGroup;
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

            // Don't let the new scene's Start() run while we're still black-
            // screened and before we're ready to reveal it.
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // One extra frame lets Awake/Start (and PlayerSpawner) finish
            // settling into the new scene before we reveal it.
            yield return null;

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
            yield return null;
        }

        group.alpha = to;
    }
}
