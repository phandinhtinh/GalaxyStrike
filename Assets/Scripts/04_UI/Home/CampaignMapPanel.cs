using System;
using GalaxyStrike.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Home
{
    public sealed class CampaignMapPanel : MonoBehaviour
    {
        public const int StagesPerZone = 10;
        public const float StageSpacing = 350f;

        private static readonly string[] ZoneNames =
        {
            "EARTH ORBIT", "ASTEROID BELT", "DEEP SPACE", "ALIEN VOID", "MOTHERSHIP CORE"
        };

        public static int ZoneCount => ZoneNames.Length;

        private static readonly Color ActiveColor = new Color32(111, 228, 255, 255);
        private static readonly Color ClearedColor = new Color32(255, 211, 124, 255);
        private static readonly Color LockedColor = new Color32(167, 190, 221, 255);

        [Serializable]
        private sealed class StageNode
        {
            public Button button;
            public Image planet;
            public Image focus;
            public Image badge;
            public TMP_Text number;
        }

        [SerializeField] private HomeSceneManager home;
        [SerializeField] private ScrollRect route;
        [SerializeField] private StageNode[] nodes;
        [SerializeField] private Image[] connections;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text zoneTitle;
        [SerializeField] private TMP_Text zoneNumber;
        [SerializeField] private Button previousZone;
        [SerializeField] private Button nextZone;
        [SerializeField] private TMP_Text missionTitle;
        [SerializeField] private TMP_Text missionStatus;
        [SerializeField] private Button missionButton;
        [SerializeField] private TMP_Text missionAction;
        [SerializeField] private Sprite mainActionSprite;
        [SerializeField] private Sprite secondaryActionSprite;

        private int currentStage;
        private int lastCleared;
        private int zone = -1;
        private int selectedStage;

        private void Awake()
        {
            previousZone.onClick.AddListener(() => ChangeZone(-1));
            nextZone.onClick.AddListener(() => ChangeZone(1));
            missionButton.onClick.AddListener(UseMissionAction);
            route.onValueChanged.AddListener(UpdateVisibleZone);
            for (int i = 0; i < nodes.Length; i++)
            {
                int stage = i + 1;
                nodes[i].button.onClick.AddListener(() => SelectStage(stage));
            }
        }

        private void OnEnable()
        {
            currentStage = Mathf.Clamp(LevelManager.SavedLevel, 1, nodes.Length);
            lastCleared = Mathf.Clamp(SaveManager.Instance.HighestLevel, 0, nodes.Length);
            RefreshRoute();
            FocusStage(currentStage);
        }

        private void RefreshRoute()
        {
            selectedStage = 0;
            zone = -1;
            for (int i = 0; i < nodes.Length; i++)
                UpdateNode(nodes[i], i + 1);

            for (int i = 0; i < connections.Length; i++)
                connections[i].color = i + 1 < currentStage
                    ? new Color32(161, 227, 255, 232)
                    : new Color32(141, 188, 255, 123);
        }

        private void ChangeZone(int step)
        {
            int targetZone = Mathf.Clamp(zone + step, 0, ZoneCount - 1);
            int first = targetZone * StagesPerZone + 1;
            int last = first + StagesPerZone - 1;
            FocusStage(currentStage >= first && currentStage <= last ? currentStage : first);
        }

        private void FocusStage(int stage)
        {
            SelectStage(stage);
            UpdateZone((stage - 1) / StagesPerZone);

            Canvas.ForceUpdateCanvases();
            route.StopMovement();
            float scrollableHeight = route.content.rect.height - route.viewport.rect.height;
            route.verticalNormalizedPosition = scrollableHeight > 0f
                ? Mathf.Clamp01((stage - 1) * StageSpacing / scrollableHeight)
                : 0f;
        }

        private void UpdateVisibleZone(Vector2 normalizedPosition)
        {
            float scrollableHeight = Mathf.Max(0f, route.content.rect.height - route.viewport.rect.height);
            float offset = Mathf.Clamp01(normalizedPosition.y) * scrollableHeight;
            int firstVisibleIndex = Mathf.Clamp(Mathf.RoundToInt(offset / StageSpacing), 0, nodes.Length - 1);
            UpdateZone(firstVisibleIndex / StagesPerZone);
        }

        private void UseMissionAction()
        {
            if (selectedStage == currentStage)
                home.PlayCampaignFromMap();
            else
                FocusStage(currentStage);
        }

        private void UpdateZone(int visibleZone)
        {
            if (zone == visibleZone)
                return;

            zone = visibleZone;
            int zoneStart = zone * StagesPerZone;
            int clearedInZone = Mathf.Clamp(lastCleared - zoneStart, 0, StagesPerZone);
            zoneTitle.text = ZoneNames[zone];
            zoneNumber.text = $"ZONE {zone + 1:00} / {ZoneCount:00}";
            progressLabel.text = $"{clearedInZone:00} / {StagesPerZone:00} CLEARED";
            progressFill.anchorMax = new Vector2((float)clearedInZone / StagesPerZone, 1f);
            progressFill.gameObject.SetActive(clearedInZone > 0);
            previousZone.interactable = zone > 0;
            nextZone.interactable = zone < ZoneCount - 1;
        }

        private void SelectStage(int stage)
        {
            int previous = selectedStage;
            selectedStage = stage;
            if (previous > 0 && previous != stage)
                UpdateNode(nodes[previous - 1], previous);
            UpdateNode(nodes[stage - 1], stage);

            bool ready = stage == currentStage;
            bool cleared = stage <= lastCleared;
            missionTitle.text = stage % StagesPerZone == 0 ? $"BOSS STAGE {stage:00}" : $"STAGE {stage:00}";
            missionStatus.text = cleared ? "STAGE CLEARED" : ready ? "READY TO LAUNCH" : "LOCKED";
            missionStatus.color = cleared ? ClearedColor : ready ? ActiveColor : LockedColor;
            missionButton.image.sprite = ready ? mainActionSprite : secondaryActionSprite;
            missionAction.text = ready ? "PLAY STAGE" : "GO TO ACTIVE STAGE";
        }

        private void UpdateNode(StageNode node, int stage)
        {
            bool ready = stage == currentStage;
            bool selected = stage == selectedStage;
            Color accent = ready ? ActiveColor : stage <= lastCleared ? ClearedColor : LockedColor;

            node.planet.color = stage <= currentStage ? Color.white : new Color32(151, 173, 203, 255);
            node.planet.rectTransform.localScale = selected ? Vector3.one * 1.10f : Vector3.one;
            node.focus.enabled = selected || ready;
            node.focus.color = accent;
            node.badge.color = ready ? new Color32(123, 225, 255, 255) : accent;
            node.number.color = accent;
            node.number.text = stage.ToString("00");
        }

#if UNITY_EDITOR
        public void PreviewLayout()
        {
            currentStage = 1;
            lastCleared = 0;
            RefreshRoute();
            UpdateZone(0);
            SelectStage(1);
        }
#endif
    }
}
