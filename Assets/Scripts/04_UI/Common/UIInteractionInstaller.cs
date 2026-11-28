using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Install feedback once after each scene's UI builders have run.</summary>
public sealed class UIInteractionInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var host = new GameObject("[UI Feedback Setup]");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<UIInteractionInstaller>();
    }

    private IEnumerator Start()
    {
        yield return null;
        foreach (var button in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.gameObject.scene == gameObject.scene && button.GetComponent<UIButtonFeedback>() == null)
                button.gameObject.AddComponent<UIButtonFeedback>();

        // Popup toàn màn hình (khung có PopupFrameFitter) -> hiệu ứng mở/đóng.
        // Victory đã có hiệu ứng riêng nên bỏ qua.
        foreach (var frame in FindObjectsByType<GalaxyStrike.UI.PopupFrameFitter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform root = frame.transform.parent;
            if (root == null || root.gameObject.scene != gameObject.scene || root.GetComponent<VictoryPanelUI>() != null)
                continue;
            GalaxyStrike.UI.PopupTransition.Attach(root.gameObject);
        }
        Destroy(gameObject);
    }
}
