#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Installs the editable gameplay readouts and keeps the particle shader in player builds.</summary>
public static class GalaxyStrikePresentationSetup
{
    [MenuItem("GalaxyStrike/Presentation/Apply Gameplay UI")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "GameScene" || Application.isPlaying)
            throw new System.InvalidOperationException("Open GameScene in Edit Mode before applying presentation setup.");
        GameObject hud = GameObject.Find("HUD_Panel");
        if (hud == null) throw new System.InvalidOperationException("HUD_Panel is missing.");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Vfx"))
            AssetDatabase.CreateFolder("Assets/Resources", "Vfx");
        const string materialPath = "Assets/Resources/Vfx/CombatAdditive.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Additive");
            if (shader == null) throw new System.InvalidOperationException("The project's additive particle shader was not found.");
            AssetDatabase.CreateAsset(new Material(shader) { name = "CombatAdditive" }, materialPath);
        }
        if (hud.GetComponentInChildren<BattleFeedbackHUD>(true) == null)
        {
            var root = new GameObject("BattleFeedback", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Add gameplay presentation");
            root.layer = 5;
            root.transform.SetParent(hud.transform, false);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/UI/Fonts/BarlowCondensed-Bold SDF.asset");
            root.AddComponent<BattleFeedbackHUD>().Build(font != null ? font : TMP_Settings.defaultFontAsset);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
}
#endif
