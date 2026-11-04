using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Loading
{
    /// <summary>Shows real scene loading progress and activates the requested scene when ready.</summary>
    public sealed class LoadingSceneController : MonoBehaviour
    {
        private const string LoadingSceneName = "LoadingScene";
        private const string TargetSceneKey = "GalaxyStrike.Loading.TargetScene";

        [Header("Loading UI")]
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI percentageLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Load Settings")]
        [SerializeField] private string defaultTargetScene = "HomeScene";
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 1.5f;
        [SerializeField, Min(0f)] private float readyHoldSeconds = 0.25f;
        [SerializeField, Min(0.1f)] private float progressCatchUpSpeed = 0.9f;

        private static bool transitionRequested;

        /// <summary>Route a scene transition through LoadingScene.</summary>
        public static void LoadScene(string targetSceneName)
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[Loading] Target scene name is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError("[Loading] Scene is not enabled in Build Settings: " + targetSceneName);
                return;
            }

            if (transitionRequested)
                return;

            Time.timeScale = 1f;
            if (!Application.CanStreamedLevelBeLoaded(LoadingSceneName))
            {
                Debug.LogWarning("[Loading] LoadingScene is not in Build Settings; opening target directly.");
                SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
                return;
            }

            transitionRequested = true;
            PlayerPrefs.SetString(TargetSceneKey, targetSceneName);
            PlayerPrefs.Save();
            SceneManager.LoadScene(LoadingSceneName, LoadSceneMode.Single);
        }

        /// <summary>Called by the scene builder to connect serialized UI references.</summary>
        public void Configure(Image fill, TextMeshProUGUI percentage, TextMeshProUGUI status)
        {
            progressFill = fill;
            percentageLabel = percentage;
            statusLabel = status;
        }

        private void Awake()
        {
            transitionRequested = false;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            SetProgress(0f);

            string targetScene = PlayerPrefs.GetString(TargetSceneKey, defaultTargetScene);
            PlayerPrefs.DeleteKey(TargetSceneKey);

            if (string.IsNullOrWhiteSpace(targetScene) || targetScene == LoadingSceneName ||
                !Application.CanStreamedLevelBeLoaded(targetScene))
            {
                ShowLoadError("SCENE NOT FOUND");
                Debug.LogError("[Loading] Target scene is missing or not enabled in Build Settings: " + targetScene);
                return;
            }

            StartCoroutine(LoadTargetScene(targetScene));
        }

        private IEnumerator LoadTargetScene(string targetScene)
        {
            if (statusLabel != null)
                statusLabel.text = "LOADING…";

            float startTime = Time.unscaledTime;
            float displayedProgress = 0f;
            AsyncOperation operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);

            if (operation == null)
            {
                ShowLoadError("LOAD FAILED");
                Debug.LogError("[Loading] Unity could not start loading scene: " + targetScene);
                yield break;
            }

            operation.allowSceneActivation = false;

            while (true)
            {
                float targetProgress = Mathf.Clamp01(operation.progress / 0.9f);
                displayedProgress = Mathf.MoveTowards(
                    displayedProgress,
                    targetProgress,
                    progressCatchUpSpeed * Time.unscaledDeltaTime);
                SetProgress(displayedProgress);

                bool sceneReady = operation.progress >= 0.9f;
                bool displayTimeElapsed = Time.unscaledTime - startTime >= minimumDisplaySeconds;
                if (sceneReady && displayTimeElapsed && displayedProgress >= 0.999f)
                {
                    SetProgress(1f);
                    if (statusLabel != null)
                        statusLabel.text = "READY";

                    if (readyHoldSeconds > 0f)
                        yield return new WaitForSecondsRealtime(readyHoldSeconds);

                    operation.allowSceneActivation = true;
                    yield break;
                }

                yield return null;
            }
        }

        private void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (progressFill != null)
                progressFill.fillAmount = value;
            if (percentageLabel != null)
                percentageLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void ShowLoadError(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
            if (percentageLabel != null)
                percentageLabel.text = "--%";
        }
    }
}
