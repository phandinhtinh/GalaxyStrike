#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GalaxyStrike.Data;
using GalaxyStrike.IAP;
using GalaxyStrike.Save;
using GalaxyStrike.UI;
using GalaxyStrike.UI.Home;
using GalaxyStrike.UI.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>Builds an editable uGUI store from the project's ship catalog and existing icons.</summary>
public static class GalaxyStrikeShopLayoutBuilder
{
    private const string Folder = "Assets/Sprites/UI/ShopPanel/";
    private const string Atlas = "Assets/Sprites/Neon Sci-Fi Store UI Sprite Sheet.png";
    private const string PrefabPath = "Assets/Prefabs/ShopItemCardWide.prefab";
    private const string PackPrefabPath = "Assets/Prefabs/ShopPackCardWide.prefab";
    private static readonly Color Cyan = new Color32(0, 209, 255, 255);
    private static readonly Color Gold = new Color32(255, 213, 45, 255);
    private static readonly Color Ink = new Color32(8, 20, 45, 255);
    private static TMP_FontAsset font;
    private static Sprite coin, gem;

    [MenuItem("Tools/GalaxyStrike/Shop/Build Reference Layout")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before rebuilding the store.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/HomeScene.unity") throw new InvalidOperationException("Open HomeScene first.");
        Transform panel = Find("Shop_Panel");
        ShopManager manager = panel.GetComponent<ShopManager>();
        if (manager == null) throw new InvalidOperationException("ShopManager is missing.");
        Selection.activeGameObject = panel.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Redesign Store");
        PrepareAssets();
        ShopItemUI prefab = BuildCardPrefab();
        IapPackCardUI packPrefab = BuildPackPrefab();
        while (panel.childCount > 0) Undo.DestroyObjectImmediate(panel.GetChild(0).gameObject);
        Stretch((RectTransform)panel);
        Image oldImage = panel.GetComponent<Image>();
        oldImage.enabled = false;
        oldImage.raycastTarget = false;
        BuildTitle(panel);
        BuildBanner(panel);
        Button[] tabs = BuildTabs(panel);
        RectTransform viewport = Rect(panel, "Main_ScrollView");
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(48, 352); viewport.offsetMax = new Vector2(-48, -804);
        Image hitArea = viewport.gameObject.AddComponent<Image>(); hitArea.color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.sizeDelta = new Vector2(0, 0); content.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 12; vertical.childControlWidth = true; vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true; vertical.childForceExpandHeight = false;
        vertical.padding = new RectOffset(0,0,4,24);
        ContentSizeFitter fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        scroll.decelerationRate = .08f;

        RectTransform shipsHeader = Header(content,"Header_Ships","SHIP COLLECTION", "08 FIGHTERS");
        RectTransform ships = Grid(content,"Grid_Ships");
        RectTransform weaponsHeader = Header(content,"Header_Weapon","WEAPONS", "EQUIP YOUR FLEET");
        RectTransform weapons = Grid(content,"Grid_Weapons");
        RectTransform itemsHeader = Header(content,"Header_Booster","ITEMS", "MISSION ESSENTIALS");
        RectTransform items = Grid(content,"Grid_Boosters");
        var so = new SerializedObject(manager);
        Set(so,"itemCardPrefab",prefab); Set(so,"packCardPrefab",packPrefab);
        Set(so,"shipGrid",ships); Set(so,"weaponGrid",weapons); Set(so,"boosterGrid",items);
        so.FindProperty("wideLayout").boolValue = true;
        so.FindProperty("headerHeight").floatValue = 38;
        so.ApplyModifiedPropertiesWithoutUndo();
        ShopCategoryScroller categories = panel.GetComponent<ShopCategoryScroller>();
        if (categories == null) categories = panel.gameObject.AddComponent<ShopCategoryScroller>();
        var cs = new SerializedObject(categories); cs.FindProperty("filterSections").boolValue = true; cs.ApplyModifiedPropertiesWithoutUndo();
        categories.Bind(scroll,content,tabs[0],shipsHeader,tabs[1],weaponsHeader,tabs[2],itemsHeader,tabs[3],null,tabs[4],null);
        BindCategoryButtons(panel,categories);
        PopulatePreview(manager, ships, weapons, items, prefab);
        // Keep the redesigned store visible and editable in the scene. HomeSceneManager still opens Home on launch.
        ApplyHomeShell();
        panel.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Debug.Log("[Shop Redesign] Saved editable store in HomeScene; 8 real ship sprites, two-column cards, category tabs and navigation connected.");
    }

    private static void PrepareAssets()
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        coin = Load("Assets/Sprites/coin.png");
        gem = Load("Assets/Sprites/UI/Task_Panel/Rewards/Reward_Gem_GalaxyStrike.png");
        // Slice existing illustrations, excluding the old baked card frames and cart buttons.
        Slice("CategoryShips", Store(0), new Rect(.22f,.44f,.56f,.45f));
        Slice("CategoryWeapons", Store(33), new Rect(.22f,.42f,.56f,.45f));
        Slice("CategoryItems", Store(3), new Rect(.22f,.43f,.56f,.45f));
        Slice("LaserIcon", Store(5), new Rect(.18f,.36f,.65f,.49f));
        Slice("RocketIcon", Store(4), new Rect(.18f,.36f,.65f,.49f));
        Slice("ShieldIcon", Store(19), new Rect(.18f,.36f,.65f,.49f));
        Slice("MagnetIcon", Store(10), new Rect(.18f,.36f,.65f,.49f));
        AssignItemIcon("Weapon_Laser", "LaserIcon"); AssignItemIcon("Weapon_Rocket", "RocketIcon");
        AssignItemIcon("Booster_Shield", "ShieldIcon"); AssignItemIcon("Booster_Magnet", "MagnetIcon");
        IapCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<IapCatalogAsset>("Assets/Resources/Config/IapCatalog.asset");
        SerializedObject cso = new SerializedObject(catalog);
        foreach (string group in new[] {"gemPacks","coinPacks"})
        {
            SerializedProperty packs = cso.FindProperty("catalog." + group);
            for (int i=0;i<packs.arraySize;i++) packs.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = group == "gemPacks" ? gem : coin;
        }
        cso.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssignItemIcon(string name,string icon)
    {
        ItemDataSO item = AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/ScriptableObject/ItemData/"+name+".asset");
        if (item == null) return;
        item.itemIcon = Load(Folder+icon+".asset"); EditorUtility.SetDirty(item);
    }

    private static void BuildTitle(Transform panel)
    {
        TextMeshProUGUI title = Text(panel,"Store_Title","STORE",102,Color.white,TextAlignmentOptions.Center);
        Top(title.rectTransform,296,174,488,106); title.fontStyle = FontStyles.Bold | FontStyles.Italic;
        title.characterSpacing = 2;
        Shadow(title.gameObject, new Vector2(0,-4), new Color(0,.5f,1,.8f));
        TextMeshProUGUI subtitle = Text(panel,"Store_Subtitle","G A L A X Y   S T R I K E   —   F L E E T   A R S E N A L",20,new Color32(173,216,255,255),TextAlignmentOptions.Center);
        Top(subtitle.rectTransform,120,286,840,30);
        for (int side=0;side<2;side++) for (int i=0;i<3;i++)
        {
            var wing = Frame(panel,"Title_Wing_"+side+"_"+i,Cyan,5);
            wing.topColor = new Color32(0,240,255,255); wing.bottomColor = new Color32(0,105,242,255); wing.glowWidth = 3;
            float width = 94-i*16; Top(wing.rectTransform,side==0?210+i*16:776,208+i*20,width,13);
        }
    }

    private static void BuildBanner(Transform panel)
    {
        var frame=Frame(panel,"Featured_Banner",Cyan,30); Top(frame.rectTransform,48,344,984,328);
        frame.topColor=new Color32(15,25,75,240); frame.bottomColor=new Color32(2,17,51,245); frame.borderWidth=3; frame.glowWidth=10;
        // The art and text remain separate; the ship is the exact NOVA sprite used in combat.
        Image nebula = Image(frame.transform,"Banner_Nebula",Find("Background_Image").GetComponent<Image>().sprite);
        Top(nebula.rectTransform,7,7,970,314); nebula.color=new Color(1,1,1,.38f); nebula.preserveAspect=false;
        ShipDataSO nova=AssetDatabase.LoadAssetAtPath<ShipDataSO>("Assets/ScriptableObject/ShipData/ShipData_3.asset");
        Image ship=Image(frame.transform,"Featured_NOVA",nova.shipSprite); Top(ship.rectTransform,92,57,336,230);
        ship.rectTransform.localEulerAngles=new Vector3(0,0,-20);
        Shadow(ship.gameObject,new Vector2(8,-10),new Color(0,.6f,1,.35f));
        var badge=Frame(frame.transform,"Featured_Badge",new Color32(211,76,255,255),13); Top(badge.rectTransform,4,4,276,51);
        badge.topColor=new Color32(163,34,224,255); badge.bottomColor=new Color32(83,6,158,255); badge.glowWidth=3;
        Image star=Image(badge.transform,"Featured_Star",Load("Assets/Sprites/UI/LeaderBoard_Panel/Sliced/DecorativeStar.png")); Top(star.rectTransform,13,5,42,40);
        var featured=Text(badge.transform,"Featured_Label","FEATURED",30,Color.white); Top(featured.rectTransform,67,3,192,42); featured.fontStyle=FontStyles.Bold|FontStyles.Italic;
        var headline=Text(frame.transform,"Banner_Headline","NEW SHIP",55,Color.white,TextAlignmentOptions.Center); Top(headline.rectTransform,473,40,464,64); headline.fontStyle=FontStyles.Bold|FontStyles.Italic;
        var name=Text(frame.transform,"Banner_Ship_Name",nova.shipName,34,Cyan,TextAlignmentOptions.Center); Top(name.rectTransform,490,105,430,45); name.fontStyle=FontStyles.Bold|FontStyles.Italic;
        var copy=Text(frame.transform,"Banner_Description","Build your fleet.\nCommand the galaxy.",25,new Color32(207,227,255,255),TextAlignmentOptions.Center); Top(copy.rectTransform,487,159,431,65); copy.fontStyle=FontStyles.Italic;
        Button get=GoldButton(frame.transform,"Featured_GetNow",new Vector2(563,241),new Vector2(341,65));
        var label=Text(get.transform,"Label","EXPLORE  >>",34,Ink,TextAlignmentOptions.Center); Stretch(label.rectTransform); label.fontStyle=FontStyles.Bold|FontStyles.Italic;
    }

    private static Button[] BuildTabs(Transform panel)
    {
        var root=Rect(panel,"Category_Tabs"); Top(root,48,712,984,128);
        string[] names={"Ships","Weapons","Items","Gems","Coins"};
        Sprite[] icons={AssetDatabase.LoadAssetAtPath<ShipDataSO>("Assets/ScriptableObject/ShipData/ShipData_1.asset").shipSprite,Load(Folder+"CategoryWeapons.asset"),Load(Folder+"CategoryItems.asset"),gem,coin};
        var result=new Button[5];
        for(int i=0;i<5;i++)
        {
            var frame=Frame(root,"Tab_"+names[i],new Color32(35,92,163,255),20); Top(frame.rectTransform,i*199,0,188,126);
            frame.glowWidth=0; frame.raycastTarget=true;
            Button b=frame.gameObject.AddComponent<Button>(); b.targetGraphic=frame; b.transition=Selectable.Transition.ColorTint;
            var selected=Frame(frame.transform,"Selected_Glow",Cyan,20); Stretch(selected.rectTransform);
            selected.topColor=new Color32(0,177,245,255); selected.bottomColor=new Color32(0,59,214,255); selected.borderWidth=3; selected.glowWidth=9;
            selected.gameObject.SetActive(i==0);
            Image icon=Image(frame.transform,"Category_Icon",icons[i]); Top(icon.rectTransform,62,12,64,63);
            var label=Text(frame.transform,"Category_Label",names[i],29,Color.white,TextAlignmentOptions.Center); Top(label.rectTransform,5,82,178,35);
            result[i]=b;
        }
        return result;
    }

    private static void BindCategoryButtons(Transform panel,ShopCategoryScroller categories)
    {
        UnityEventTools.AddIntPersistentListener(panel.Find("Featured_Banner/Featured_GetNow").GetComponent<Button>().onClick,categories.SelectCategory,0);
    }

    [MenuItem("Tools/GalaxyStrike/Shop/Use Home Background and Navigation")]
    public static void ApplyHomeShell()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Transform panel = Find("Shop_Panel");
        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Use Home shop shell");
        foreach (string name in new[] { "Store_Bottom_Navigation", "Shop_Space_Background", "Store_TopBar" })
        {
            Transform child = panel.Find(name);
            if (child != null) Undo.DestroyObjectImmediate(child.gameObject);
        }
        // Share Home's Canvas as well as its background and navigation.
        GraphicRaycaster raycaster = panel.GetComponent<GraphicRaycaster>();
        if (raycaster != null) Undo.DestroyObjectImmediate(raycaster);
        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas != null) Undo.DestroyObjectImmediate(canvas);
        // A transparent hit area prevents clicks reaching the inactive Home content.
        Image hitArea = panel.GetComponent<Image>();
        hitArea.sprite = null; hitArea.color = Color.clear; hitArea.enabled = true; hitArea.raycastTarget = true;
        Component legacyHud = panel.GetComponent("ShopHudUI");
        if (legacyHud != null) Undo.DestroyObjectImmediate(legacyHud);
        Find("TopBar_Panel").gameObject.SetActive(true);
        Find("SettingBtn").gameObject.SetActive(true);
        Find("Center_Panel").gameObject.SetActive(false);
        Find("Bottom_Panel").gameObject.SetActive(false);
        Top(panel.Find("Store_Title").GetComponent<RectTransform>(), 296, 132, 488, 80);
        Top(panel.Find("Store_Subtitle").GetComponent<RectTransform>(), 120, 218, 840, 30);
        for (int side=0; side<2; side++) for (int i=0; i<3; i++)
            Top(panel.Find("Title_Wing_"+side+"_"+i).GetComponent<RectTransform>(), side==0?210+i*16:776, 163+i*18, 94-i*16, 13);
        Top(panel.Find("Featured_Banner").GetComponent<RectTransform>(), 48, 256, 984, 328);
        Top(panel.Find("Category_Tabs").GetComponent<RectTransform>(), 48, 624, 984, 128);
        var viewport = panel.Find("Main_ScrollView").GetComponent<RectTransform>();
        viewport.offsetMin = new Vector2(48, 388); viewport.offsetMax = new Vector2(-48, -768);
        Image banner = panel.Find("Featured_Banner/Banner_Nebula").GetComponent<Image>();
        banner.sprite = Find("Background_Image").GetComponent<Image>().sprite;
        Canvas.ForceUpdateCanvases();
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("[Shop Redesign] Reusing the original Home HUD, Background_Image and Bottom_Navigation_Bar without changing their layout or artwork.");
    }

    private static ShopItemUI BuildCardPrefab()
    {
        var root=Rect(null,"ShopItemCardWide"); root.sizeDelta=new Vector2(474,340);
        var frame=root.gameObject.AddComponent<ShopPanelGraphic>(); frame.borderColor=Cyan; frame.cornerCut=24; frame.glowWidth=5;
        var presentation=root.gameObject.AddComponent<ShopCardPresentation>(); presentation.frame=frame; presentation.coinSprite=coin; presentation.gemSprite=gem;
        var sheen=Frame(root,"Corner_Accent",new Color32(49,118,230,100),10); Top(sheen.rectTransform,15,4,75,9); sheen.topColor=new Color(0,.7f,1,.25f); sheen.bottomColor=sheen.topColor; sheen.glowWidth=0;
        Image ship=Image(root,"Item_Icon",null); Top(ship.rectTransform,18,24,228,232);
        var badge=Frame(root,"Designation_Badge",Cyan,12); Top(badge.rectTransform,270,22,173,42); badge.glowWidth=2; presentation.badge=badge;
        presentation.designation=Text(badge.transform,"Designation","MK. 02",24,Color.white,TextAlignmentOptions.Center); Stretch(presentation.designation.rectTransform); presentation.designation.fontStyle=FontStyles.Bold|FontStyles.Italic;
        var name=Text(root,"Name","ZEPHYR",35,Color.white,TextAlignmentOptions.Left); Top(name.rectTransform,252,79,202,52); name.fontStyle=FontStyles.Bold|FontStyles.Italic;
        presentation.health=Text(root,"Health","HP   3",25,Color.white); Top(presentation.health.rectTransform,256,144,190,34);
        presentation.fireRate=Text(root,"Fire_Rate","FIRE/s  2.17",25,Color.white); Top(presentation.fireRate.rectTransform,256,190,196,34);
        Button buy=GoldButton(root,"Buy_Button",new Vector2(88,264),new Vector2(304,60));
        presentation.currencyIcon=Image(buy.transform,"Currency",coin); Top(presentation.currencyIcon.rectTransform,33,9,42,42);
        var price=Text(buy.transform,"Price_Text","800",36,Ink,TextAlignmentOptions.Center); Top(price.rectTransform,86,4,197,51); price.fontStyle=FontStyles.Bold|FontStyles.Italic;
        presentation.priceLabel=price;
        ShopItemUI component=root.gameObject.AddComponent<ShopItemUI>();
        var serialized=new SerializedObject(component); Set(serialized,"iconImage",ship); Set(serialized,"nameText",name); Set(serialized,"priceText",price); Set(serialized,"buyButton",buy); Set(serialized,"presentation",presentation); serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabPath); Object.DestroyImmediate(root.gameObject);
        return prefab.GetComponent<ShopItemUI>();
    }

    private static IapPackCardUI BuildPackPrefab()
    {
        var root=Rect(null,"ShopPackCardWide"); root.sizeDelta=new Vector2(474,340);
        var frame=root.gameObject.AddComponent<ShopPanelGraphic>(); frame.borderColor=new Color32(174,81,255,255); frame.cornerCut=24; frame.glowWidth=5;
        Image icon=Image(root,"Pack_Icon",gem); Top(icon.rectTransform,35,45,175,175);
        var badge=Text(root,"Badge","SUPPLY PACK",22,Cyan); Top(badge.rectTransform,228,30,221,35);
        var amount=Text(root,"Amount","650",54,Color.white); Top(amount.rectTransform,228,84,220,70);
        var name=Text(root,"Name","Medium Gem Pack",25,Color.white); Top(name.rectTransform,228,165,220,60);
        Button buy=GoldButton(root,"Buy_Button",new Vector2(88,264),new Vector2(304,60));
        var price=Text(buy.transform,"Price","...",31,Ink,TextAlignmentOptions.Center); Stretch(price.rectTransform);
        var card=root.gameObject.AddComponent<IapPackCardUI>(); var so=new SerializedObject(card);
        Set(so,"priceText",price); Set(so,"buyButton",buy); Set(so,"iconImage",icon); Set(so,"packAmount",amount); Set(so,"packName",name); Set(so,"packBadge",badge); so.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab=PrefabUtility.SaveAsPrefabAsset(root.gameObject,PackPrefabPath); Object.DestroyImmediate(root.gameObject);
        return prefab.GetComponent<IapPackCardUI>();
    }

    private static void PopulatePreview(ShopManager manager,Transform ships,Transform weapons,Transform items,ShopItemUI prefab)
    {
        SerializedObject so=new SerializedObject(manager); SerializedProperty data=so.FindProperty("shipDataArray");
        int[] order={1,2,3,4,0,5,6,7};
        foreach(int index in order)
        {
            ShipDataSO ship=data.GetArrayElementAtIndex(index).objectReferenceValue as ShipDataSO;
            var card=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,ships);
            Preview(card,ship,index);
        }
        foreach(var group in new[] { ("weaponDataArray",weapons), ("boosterDataArray",items) })
        {
            SerializedProperty array=so.FindProperty(group.Item1);
            for(int i=0;i<array.arraySize;i++)
            {
                ItemDataSO item=array.GetArrayElementAtIndex(i).objectReferenceValue as ItemDataSO;
                var card=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,group.Item2);
                card.name="Preview_"+item.itemID; card.transform.Find("Item_Icon").GetComponent<Image>().sprite=item.itemIcon;
                card.transform.Find("Name").GetComponent<TextMeshProUGUI>().text=item.itemName;
                card.transform.Find("Buy_Button/Price_Text").GetComponent<TextMeshProUGUI>().text=item.price.ToString("N0");
                card.GetComponent<ShopCardPresentation>().Configure(item);
            }
        }
        weapons.gameObject.SetActive(false);items.gameObject.SetActive(false);
        foreach(var g in new[]{ships,weapons,items})
        {
            var le=g.GetComponent<LayoutElement>(); float h=Mathf.Ceil(g.childCount/2f)*364-24; le.minHeight=h;le.preferredHeight=h;
        }
    }

    private static void Preview(GameObject card,ShipDataSO ship,int index)
    {
        card.name="Preview_"+ship.shipName; card.transform.Find("Item_Icon").GetComponent<Image>().sprite=ship.shipSprite;
        card.transform.Find("Name").GetComponent<TextMeshProUGUI>().text=ship.shipName;
        card.transform.Find("Buy_Button/Price_Text").GetComponent<TextMeshProUGUI>().text=ship.price==0?"SELECT":ship.price.ToString("N0");
        card.GetComponent<ShopCardPresentation>().Configure(ship,index);
    }

    private static RectTransform Header(Transform content,string name,string title,string detail)
    {
        var root=Rect(content,name); var le=root.gameObject.AddComponent<LayoutElement>(); le.minHeight=38; le.preferredHeight=38;
        var label=Text(root,"Label",title,21,new Color32(129,191,232,255)); Stretch(label.rectTransform); label.characterSpacing=1.8f;
        var count=Text(root,"Detail",detail,18,new Color32(93,146,191,255),TextAlignmentOptions.Right); Stretch(count.rectTransform);
        return root;
    }
    private static RectTransform Grid(Transform content,string name)
    {
        var root=Rect(content,name); var grid=root.gameObject.AddComponent<GridLayoutGroup>(); grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount=2;
        grid.cellSize=new Vector2(474,340); grid.spacing=new Vector2(24,24);
        var le=root.gameObject.AddComponent<LayoutElement>(); le.minHeight=340;le.preferredHeight=340;
        return root;
    }
    private static Button GoldButton(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var frame=Frame(parent,name,new Color32(255,242,116,255),18); Top(frame.rectTransform,position.x,position.y,size.x,size.y);
        frame.topColor=new Color32(255,231,25,255);frame.bottomColor=new Color32(255,170,0,255);frame.borderWidth=3;frame.glowWidth=5;frame.raycastTarget=true;
        var button=frame.gameObject.AddComponent<Button>(); button.targetGraphic=frame;
        var colors=button.colors; colors.highlightedColor=new Color(1,1,.85f);colors.pressedColor=new Color(.85f,.72f,.5f);colors.disabledColor=new Color(.75f,.75f,.85f,.85f);button.colors=colors;
        return button;
    }
    private static ShopPanelGraphic Frame(Transform parent,string name,Color border,float cut)
    {
        var r=Rect(parent,name);var g=r.gameObject.AddComponent<ShopPanelGraphic>();g.borderColor=border;g.cornerCut=cut;g.raycastTarget=false;return g;
    }
    private static RectTransform Rect(Transform parent,string name)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;var r=(RectTransform)go.transform;r.SetParent(parent,false);return r;
    }
    private static Image Image(Transform parent,string name,Sprite sprite)
    {
        var r=Rect(parent,name);var image=r.gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
    }
    private static TextMeshProUGUI Text(Transform parent,string name,string text,float size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
    {
        var r=Rect(parent,name);var t=r.gameObject.AddComponent<TextMeshProUGUI>();if(font)t.font=font;t.text=text;t.fontSize=size;t.fontSizeMin=size*.65f;t.fontSizeMax=size;t.enableAutoSizing=true;t.fontStyle=FontStyles.Bold;t.alignment=alignment;t.color=color;t.raycastTarget=false;t.textWrappingMode = TextWrappingModes.NoWrap;return t;
    }
    private static void Top(RectTransform r,float x,float y,float w,float h)
    {
        r.anchorMin=new Vector2(0,1);r.anchorMax=r.anchorMin;r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
    }
    private static void Stretch(RectTransform r) {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;}
    private static void Shadow(GameObject go,Vector2 offset,Color color) {var s=go.AddComponent<Shadow>();s.effectDistance=offset;s.effectColor=color;}
    private static Sprite Store(int index) => AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().First(s=>s.name=="Neon Sci-Fi Store UI Sprite Sheet_"+index);
    private static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    private static void Slice(string name,Sprite source,Rect relative)
    {
        string path=Folder+name+".asset";if(AssetDatabase.LoadAssetAtPath<Sprite>(path))return;
        Rect rect=source.rect;rect=new Rect(rect.x+relative.x*rect.width,rect.y+relative.y*rect.height,rect.width*relative.width,rect.height*relative.height);
        Sprite sprite=Sprite.Create(source.texture,rect,new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=name;AssetDatabase.CreateAsset(sprite,path);
    }
    private static void Set(SerializedObject so,string name,Object value) {var p=so.FindProperty(name);if(p==null)throw new InvalidOperationException(name);p.objectReferenceValue=value;}
    private static Transform Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name);

    [MenuItem("Tools/GalaxyStrike/Shop/Set Portrait Game View")]
    public static void PortraitGameView()
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        Assembly assembly=typeof(EditorWindow).Assembly;
        Type sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        object sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",flags).GetValue(null);
        object group=sizesType.GetProperty("currentGroup",flags).GetValue(sizes);
        Type groupType=group.GetType();
        int count=(int)groupType.GetMethod("GetTotalCount",flags).Invoke(group,null);
        int index=-1;
        for(int i=0;i<count;i++)
        {
            object size=groupType.GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
            if((int)size.GetType().GetProperty("width",flags).GetValue(size)==1080 && (int)size.GetType().GetProperty("height",flags).GetValue(size)==1920) {index=i;break;}
        }
        if(index<0)
        {
            Type sizeType=assembly.GetType("UnityEditor.GameViewSize");
            Type enumType=assembly.GetType("UnityEditor.GameViewSizeType");
            object size=Activator.CreateInstance(sizeType,flags,null,new object[]{Enum.Parse(enumType,"FixedResolution"),1080,1920,"GalaxyStrike Portrait"},null);
            groupType.GetMethod("AddCustomSize",flags).Invoke(group,new[]{size}); index=count;
        }
        Type gameView=assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(gameView);
        gameView.GetProperty("selectedSizeIndex",flags).SetValue(window,index);window.Repaint();
        Debug.Log("[Shop Redesign] Game View set to 1080 x 1920.");
    }

    [MenuItem("Tools/GalaxyStrike/Shop/Capture Portrait Preview")]
    public static void Capture() => CaptureSize(1080,1920,"shop-1080x1920.png");
    [MenuItem("Tools/GalaxyStrike/Shop/Capture Tall Preview")]
    public static void CaptureTall() => CaptureSize(1080,2340,"shop-1080x2340.png");
    private static void CaptureSize(int width,int height,string filename)
    {
        Canvas canvas=Find("Canvas").GetComponent<Canvas>();CanvasScaler scaler=canvas.GetComponent<CanvasScaler>();
        Camera camera=Camera.main;var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;float oldDistance=canvas.planeDistance;float oldScale=canvas.scaleFactor;
        bool oldScaler=scaler.enabled;var oldTarget=camera.targetTexture;var rt=RenderTexture.GetTemporary(width,height,24);Texture2D texture=null;
        RenderTexture previous=RenderTexture.active;
        try
        {
            scaler.enabled=false;camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;
            Canvas.ForceUpdateCanvases();
            foreach(var layout in Find("Shop_Panel").GetComponentsInChildren<VerticalLayoutGroup>())LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            Directory.CreateDirectory("Temp/ShopRedesign");File.WriteAllBytes("Temp/ShopRedesign/"+filename,texture.EncodeToPNG());
            Debug.Log("[Shop Redesign] Captured "+Path.GetFullPath("Temp/ShopRedesign/"+filename));
        }
        finally {RenderTexture.active=previous;canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;canvas.scaleFactor=oldScale;camera.targetTexture=oldTarget;scaler.enabled=oldScaler;RenderTexture.ReleaseTemporary(rt);if(texture)Object.DestroyImmediate(texture);Canvas.ForceUpdateCanvases();}
    }

    [MenuItem("Tools/GalaxyStrike/Shop/Validate Runtime Navigation")]
    public static void ValidateRuntime()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        HomeSceneManager home=Find("HomeSceneManager").GetComponent<HomeSceneManager>();home.OnTabSelected("Shop");
        EditorApplication.delayCall+=()=>
        {
            Transform panel=Find("Shop_Panel");string[] tabs={"Ships","Weapons","Items","Gems","Coins"};
            int[] counts={8,2,2,3,3};
            for(int i=0;i<tabs.Length;i++)
            {
                panel.Find("Category_Tabs/Tab_"+tabs[i]).GetComponent<Button>().onClick.Invoke();
                var cards=panel.GetComponentsInChildren<ShopItemUI>();var packs=panel.GetComponentsInChildren<IapPackCardUI>();
                int count=cards.Length+packs.Length;
                if(count!=counts[i])throw new InvalidOperationException(tabs[i]+" expected "+counts[i]+" cards, got "+count);
                if(panel.GetComponentsInChildren<Button>().Any(b=>!b.targetGraphic))throw new InvalidOperationException("Unbound button graphic");
                Canvas.ForceUpdateCanvases();
                var tab=panel.Find("Category_Tabs/Tab_"+tabs[i]).GetComponent<Button>();
                AssertClickable(tab);
                if(panel.GetComponentsInChildren<Image>().Any(image=>image.name=="Item_Icon" && !image.sprite))throw new InvalidOperationException("Missing item sprite");
                Debug.Log("[Shop Redesign] PASS "+tabs[i]+": "+count+" data-bound cards");
            }
            panel.Find("Featured_Banner/Featured_GetNow").GetComponent<Button>().onClick.Invoke();
            if(panel.GetComponentsInChildren<ShopItemUI>().Length!=8)throw new InvalidOperationException("Featured ship shortcut failed");
            home.OnTabSelected("Upgrade");if(panel.gameObject.activeSelf)throw new InvalidOperationException("Navigation away failed");
            home.OnTabSelected("Shop");
            Debug.Log("[Shop Redesign] PASS category switching, featured ship shortcut, return navigation. No purchases or save mutations performed.");
        };
    }

    private static void AssertClickable(Button button)
    {
        var rect=(RectTransform)button.transform;
        Vector2 center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=center};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<Button>()!=button)throw new InvalidOperationException("Button is blocked: "+button.name);
    }
}
#endif
