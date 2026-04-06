using System.Collections.Generic;

namespace ScriptQuest.Combat;

public static class AbilityDatabase
{
    public static readonly Dictionary<string, Ability> All = new()
    {
        ["shield_bash"] = new Ability
        {
            Id = "shield_bash",
            Name = "Shield Bash",
            ManaCost = 15,
            CooldownTicks = 30,
            Range = 1.5f,
            EffectType = AbilityEffectType.MeleeAttack,
            BaseDamage = 8,
            StunTicks = 20
        },
        ["fireball"] = new Ability
        {
            Id = "fireball",
            Name = "Fireball",
            ManaCost = 25,
            CooldownTicks = 50,
            Range = 6.0f,
            EffectType = AbilityEffectType.AoEAtPosition,
            BaseDamage = 30,
            AoERadius = 2.0f
        },
        ["backstab"] = new Ability
        {
            Id = "backstab",
            Name = "Backstab",
            ManaCost = 10,
            CooldownTicks = 20,
            Range = 1.5f,
            EffectType = AbilityEffectType.MeleeAttack,
            BaseDamage = 0  // damage multiplied from DamageCalculator in resolver
        },
        ["heal"] = new Ability
        {
            Id = "heal",
            Name = "Heal",
            ManaCost = 20,
            CooldownTicks = 40,
            Range = 4.0f,
            EffectType = AbilityEffectType.Heal,
            BaseHeal = 30
        }
    };

    public static Ability Get(string id)
    {
        All.TryGetValue(id, out var ability);
        return ability;
    }
}
