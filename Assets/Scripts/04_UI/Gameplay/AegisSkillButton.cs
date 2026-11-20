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

        PlayerSkillVfx.PlayAegis(player, duration);
    }
}
