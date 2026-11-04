#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using GalaxyStrike.UI.Shop;

/// <summary>
/// Gắn và cấu hình ShopCategoryScroller cho Shop_Panel trong HomeScene.
///
/// Vì sao cần: nút Category_Sidebar và Header đích nằm ở nhiều nhánh Hierarchy khác nhau,
/// gán tay 10 tham chiếu rất dễ sót. Tool tự tìm theo tên và lưu đúng vào component.
///
/// Cách dùng: menu "Galaxy Strike/Giai đoạn 2 - Gắn cuộn danh mục Shop".
/// An toàn khi bấm nhiều lần.
/// </summary>
public static class GalaxyStrikeShopScrollerSetup
{
    private const string HomeScenePath = "Assets/Scenes/HomeScene.unity";

[MenuItem("Galaxy Strike/Phase 2 - Setup Shop Category Scroll", priority = 3)]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Galaxy Strike", "Hãy thoát Play Mode trước khi thiết lập.", "OK");
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != HomeScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            scene = EditorSceneManager.OpenScene(HomeScenePath, OpenSceneMode.Single);
        }

        GameObject shopPanel = FindByName(scene, "Shop_Panel");
        if (shopPanel == null)
        {
            Debug.LogError($"[GalaxyStrike] Không tìm thấy Shop_Panel trong {HomeScenePath}.");
            return;
        }

        var scrollRect = shopPanel.GetComponentInChildren<ScrollRect>(true);
        var content = FindRect(scene, "Content");
        if (scrollRect == null || content == null)
        {
            Debug.LogError("[GalaxyStrike] Thiếu ScrollRect hoặc Content trong Shop_Panel.");
            return;
        }

        var scroller = shopPanel.GetComponent<ShopCategoryScroller>();
        bool added = false;
        if (scroller == null)
        {
            scroller = Undo.AddComponent<ShopCategoryScroller>(shopPanel);
            added = true;
        }

        var serialized = new SerializedObject(scroller);

        SetObject(serialized, "scrollRect", scrollRect);
        SetObject(serialized, "content", content);

        SetObject(serialized, "shipsButton", FindButton(scene, "Tab_Ships"));
        SetObject(serialized, "weaponsButton", FindButton(scene, "Tab_Weapons"));
        SetObject(serialized, "itemsButton", FindButton(scene, "Tab_Items"));
        SetObject(serialized, "gemsButton", FindButton(scene, "Tab_Gems"));
        SetObject(serialized, "coinsButton", FindButton(scene, "Tab_Coins"));

        SetObject(serialized, "shipsTarget", FindRect(scene, "Header_Ships"));
        SetObject(serialized, "weaponsTarget", FindRect(scene, "Header_Weapon"));
        SetObject(serialized, "itemsTarget", FindRect(scene, "Header_Booster"));

        // Header gói IAP được tạo lúc runtime -> nếu chưa có thì để trống, ShopManager tự gán sau.
        SetObject(serialized, "gemsTarget", FindRect(scene, "Header_GemPacks"));
        SetObject(serialized, "coinsTarget", FindRect(scene, "Header_CoinPacks"));

        serialized.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GalaxyStrike] ShopCategoryScroller {(added ? "đã thêm" : "đã có sẵn")} trên Shop_Panel.\n" +
            "- Tab_Ships   -> Header_Ships\n" +
            "- Tab_Weapons -> Header_Weapon\n" +
            "- Tab_Items   -> Header_Booster\n" +
            "- Tab_Gems    -> Header_GemPacks (gán lúc runtime)\n" +
            "- Tab_Coins   -> Header_CoinPacks (gán lúc runtime)");
    }

    private static void SetObject(SerializedObject serialized, string fieldName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(fieldName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static Button FindButton(Scene scene, string name)
    {
        GameObject go = FindByName(scene, name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static RectTransform FindRect(Scene scene, string name)
    {
        GameObject go = FindByName(scene, name);
        return go != null ? go.transform as RectTransform : null;
    }

    private static GameObject FindByName(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] all = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < all.Length; j++)
            {
                if (all[j].gameObject.name == name)
                    return all[j].gameObject;
            }
        }

        return null;
    }
}
#endif
