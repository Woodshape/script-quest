using System;
using ScriptQuest.Entities;

namespace ScriptQuest.Combat;

public static class DamageCalculator
{
    public static int Calculate(Entity attacker, Entity target)
    {
        return Math.Max(1, attacker.Stats.AttackDamage - target.Stats.Armor);
    }
}
