using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Full-screen fade used between scenes. Built at runtime by GameSystems and
/// kept alive with DontDestroyOnLoad so the fade-out coroutine survives the
/// scene swap it is causing.
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
            Debug.LogError($"[SceneFader] Scene '{sceneName}' is not in File > Build Settings.");
            return;
        }

        StartCoroutine(LoadRoutine(sceneName, spawnId));
    }

    private IEnumerator LoadRoutine(string sceneName, string spawnId)
    {
        busy = true;

        yield return Fade(group != null ? group.alpha : 0f, 1f);

        GameState.LastSpawnId = spawnId;
        SceneManager.LoadScene(sceneName);

        // One frame lets Awake/Start (and PlayerSpawner) run before we reveal.
        yield return null;

        yield return Fade(1f, 0f);

        busy = false;
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
