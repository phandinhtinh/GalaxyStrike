using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyStrike.UI.Tasks
{
    /// <summary>Scene references for one reusable task row in the scroll list.</summary>
    public sealed class TaskPopupRowView : MonoBehaviour
    {
        public GameObject root;
        public Image icon;
        public TMP_Text title;
        public Image progressFill;
        public TMP_Text progressLabel;
        public Image rewardOneIcon;
        public TMP_Text rewardOneAmount;
        public Image rewardTwoIcon;
        public TMP_Text rewardTwoAmount;
        public Button actionButton;
        public Image actionFace;
        public TMP_Text actionLabel;

        private void Reset()
        {
            root = gameObject;
        }
    }
}
