using UnityEngine;

public sealed class AegisSkillButton : SkillButton
{
    [SerializeField, Min(0.1f)] private float duration = 3f;

    protected override void ApplySkill(PlayerController player)
    {
        if (player.TryGetComponent(out PlayerEquipment equipment))
            equipment.ActivateShield(duration);
        else
            player.GrantInvulnerability(duration);

        var field = new GameObject("Aegis Field");
        field.transform.SetParent(player.transform, false);
        var renderer = field.AddComponent<SpriteRenderer>();
        renderer.sprite = RingSprite;
        renderer.color = new Color(1f, 0.76f, 0.34f, 0.85f);
        renderer.sortingOrder = 5;

        SpriteRenderer hull = player.GetComponent<SpriteRenderer>();
        Bounds hullBounds = hull != null && hull.sprite != null
            ? hull.sprite.bounds
            : new Bounds(Vector3.zero, Vector3.one);
        field.transform.localPosition = hullBounds.center;
        float diameter = hullBounds.size.magnitude * 1.1f;
        field.transform.localScale = Vector3.one * (diameter / renderer.sprite.bounds.size.x);
        Destroy(field, duration);
    }
}
