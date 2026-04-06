namespace ScriptQuest.Combat;

public enum AbilityEffectType
{
    MeleeAttack,
    RangedAttack,
    AoEAtPosition,
    Heal
}

public class Ability
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int ManaCost { get; set; }
    public int CooldownTicks { get; set; }
    public float Range { get; set; }
    public AbilityEffectType EffectType { get; set; }
    public int BaseDamage { get; set; }
    public int BaseHeal { get; set; }
    public int StunTicks { get; set; }
    public float AoERadius { get; set; }
}
