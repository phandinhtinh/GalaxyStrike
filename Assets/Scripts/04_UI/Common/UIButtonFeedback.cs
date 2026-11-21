using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Small, unscaled pointer/focus transitions without changing the button's authored colors.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
public sealed class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField] private Color accent = new Color(0.18f, 0.88f, 1f);
    [SerializeField, Range(0.9f, 1f)] private float pressedScale = 0.95f;
    [SerializeField, Range(1f, 1.08f)] private float focusScale = 1.025f;
    private UnityEngine.UI.Button button;
    private Vector3 restScale;
    private bool hovered, pressed, selected, initialized;
    private float scale = 1f;

    private void Awake()
    {
        button = GetComponent<UnityEngine.UI.Button>();
        restScale = transform.localScale;
        initialized = true;
    }

    private bool CanInteract => button != null && button.IsActive() && button.IsInteractable();

    private void Update()
    {
        float target = !CanInteract ? 1f : pressed ? pressedScale : hovered || selected ? focusScale : 1f;
        if (Mathf.Abs(scale - target) < 0.0001f) return;
        scale = Mathf.Lerp(scale, target, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
        if (Mathf.Abs(scale - target) < 0.0005f) scale = target;
        transform.localScale = restScale * scale;
    }

    public void OnPointerEnter(PointerEventData data) => hovered = true;
    public void OnPointerExit(PointerEventData data) { hovered = false; pressed = false; }
    public void OnSelect(BaseEventData data) => selected = true;
    public void OnDeselect(BaseEventData data) => selected = false;
    public void OnPointerUp(PointerEventData data) => pressed = false;

    public void OnPointerDown(PointerEventData data)
    {
        if (!CanInteract || data.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        UIInteractionParticles.Burst((RectTransform)transform, accent);
    }

    public void OnSubmit(BaseEventData data)
    {
        if (!CanInteract) return;
        scale = pressedScale;
        transform.localScale = restScale * scale;
        UIInteractionParticles.Burst((RectTransform)transform, accent);
    }

    private void OnDisable()
    {
        hovered = pressed = selected = false;
        scale = 1f;
        if (initialized) transform.localScale = restScale;
    }
}
