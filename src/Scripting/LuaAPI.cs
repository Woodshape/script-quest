using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using ScriptQuest.Combat;
using ScriptQuest.Entities;

namespace ScriptQuest.Scripting;

/// <summary>
/// Wraps an Entity to expose a safe Lua API.
/// Each entity gets its own LuaAPI instance per tick.
/// </summary>
public class LuaAPI
{
    private readonly Entity _self;
    private readonly EntityManager _entityManager;

    public LuaAPI(Entity self, EntityManager entityManager)
    {
        _self = self;
        _entityManager = entityManager;
    }

    // === Perception ===

    [MoonSharpUserDataMetamethod("__index")]
    public DynValue Index(Script script, DynValue key)
    {
        return DynValue.Nil;
    }

    public Table GetEnemiesInRange(Script script, double range)
    {
        var enemies = _entityManager.GetEnemiesInRange(_self, (float)range);
        return EntitiesToTable(script, enemies);
    }

    public DynValue GetNearestEnemy(Script script)
    {
        var enemy = _entityManager.GetNearestEnemy(_self);
        if (enemy == null) return DynValue.Nil;
        return DynValue.NewTable(EntityToTable(script, enemy));
    }

    public int GetHp() => _self.Stats.Hp;
    public int GetMaxHp() => _self.Stats.MaxHp;
    public int GetMana() => _self.Stats.Mana;
    public int GetMaxMana() => _self.Stats.MaxMana;

    public Table GetPosition(Script script)
    {
        var table = new Table(script);
        table["x"] = _self.Position.X;
        table["y"] = _self.Position.Y;
        return table;
    }

    public double DistanceTo(Table targetTable)
    {
        var target = GetEntityFromTable(targetTable);
        return target != null ? _self.DistanceTo(target) : double.MaxValue;
    }

    public Table GetAllies(Script script)
    {
        var allies = _entityManager.GetAlliesOf(_self);
        return EntitiesToTable(script, allies);
    }

    // === Movement ===

    public void MoveTo(double x, double y)
    {
        Vector2 point = new((float)x, (float)y);
        SetMoveAction(point);
    }

    public void MoveTowards(Table targetTable)
    {
        var targetEntity = GetEntityFromTable(targetTable);
        if (targetEntity != null)
            SetMoveAction(targetEntity.Position);
    }

    public void MoveAwayFrom(Table targetTable)
    {
        var targetEntity = GetEntityFromTable(targetTable);
        if (targetEntity == null) return;

        var direction = _self.Position - targetEntity.Position;
        if (direction.LengthSquared() > 0)
        {
            direction.Normalize();
            SetMoveAction(_self.Position + direction * 1000f); // move to a point far away
        }
    }

    // === Combat ===

    public void SetAttackAction(Table targetTable)
    {
        var targetEntity = GetEntityFromTable(targetTable);
        if (targetEntity == null) return;

        _self.PendingAction = new EntityAction
        {
            Type = ActionType.Attack,
            TargetEntity = targetEntity
        };
    }

    public bool CanUseAbility(string abilityId)
    {
        return GetUsableAbility(abilityId, logFailures: false) != null;
    }

    public void UseAbilityOnTarget(string abilityId, Table targetTable)
    {
        var ability = GetUsableAbility(abilityId, logFailures: true);
        if (ability == null)
            return;

        if (ability.EffectType == AbilityEffectType.AoEAtPosition)
        {
            _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {ability.Name} requires a position target");
            return;
        }

        var targetEntity = GetEntityFromTable(targetTable);
        if (targetEntity == null)
        {
            _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {ability.Name} ignored because the target was invalid");
            return;
        }

        _self.PendingAction = new EntityAction
        {
            Type = ActionType.UseAbility,
            AbilityId = abilityId,
            AbilityTarget = targetEntity
        };
    }

    public void UseAbilityAtPosition(string abilityId, double x, double y)
    {
        var ability = GetUsableAbility(abilityId, logFailures: true);
        if (ability == null)
            return;

        if (ability.EffectType != AbilityEffectType.AoEAtPosition)
        {
            _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {ability.Name} requires an entity target");
            return;
        }

        _self.PendingAction = new EntityAction
        {
            Type = ActionType.UseAbility,
            AbilityId = abilityId,
            AbilityPosition = new Vector2((float)x, (float)y)
        };
    }

    public void Log(string message)
    {
        _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {message}");
    }

    // === Helpers ===

    private void SetMoveAction(Vector2 target)
    {
        _self.PendingAction = new EntityAction
        {
            Type = ActionType.MoveTo,
            TargetPosition = target
        };
    }

    private Entity? GetEntityFromTable(Table table)
    {
        if (table == null) return null;
        var id = table.Get("id").String;
        if (id == null) return null;

        foreach (var entity in _entityManager.Entities)
        {
            if (entity.Id == id && entity.IsAlive)
                return entity;
        }

        return null;
    }

    private Ability? GetUsableAbility(string abilityId, bool logFailures)
    {
        var ability = AbilityDatabase.Get(abilityId);
        if (ability == null)
        {
            if (logFailures)
                _entityManager.CombatLog.Add($"[Lua:{_self.Name}] Unknown ability '{abilityId}'");
            return null;
        }

        if (_self.IsOnCooldown(abilityId))
        {
            if (logFailures)
                _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {ability.Name} is on cooldown");
            return null;
        }

        if (_self.Stats.Mana < ability.ManaCost)
        {
            if (logFailures)
                _entityManager.CombatLog.Add($"[Lua:{_self.Name}] {ability.Name} needs {ability.ManaCost} mana");
            return null;
        }

        return ability;
    }

    private Table EntitiesToTable(Script script, List<Entity> entities)
    {
        var table = new Table(script);
        for (int i = 0; i < entities.Count; i++)
        {
            table[i + 1] = EntityToTable(script, entities[i]);
        }
        return table;
    }

    private Table EntityToTable(Script script, Entity entity)
    {
        var table = new Table(script);
        table["id"] = entity.Id;
        table["name"] = entity.Name;
        table["hp"] = entity.Stats.Hp;
        table["max_hp"] = entity.Stats.MaxHp;
        table["x"] = entity.Position.X;
        table["y"] = entity.Position.Y;
        table["team"] = entity.Team.ToString().ToLower();
        return table;
    }

    /// <summary>
    /// Register all API functions into a Lua table that acts as "self" in scripts.
    /// </summary>
    public Table CreateSelfTable(Script script)
    {
        var self = new Table(script);

        // Perception
        self["get_enemies_in_range"] = (Func<double, Table>)(range => GetEnemiesInRange(script, range));
        self["get_nearest_enemy"] = (Func<DynValue>)(() => GetNearestEnemy(script));
        self["get_hp"] = (Func<int>)GetHp;
        self["get_max_hp"] = (Func<int>)GetMaxHp;
        self["get_mana"] = (Func<int>)GetMana;
        self["get_max_mana"] = (Func<int>)GetMaxMana;
        self["get_position"] = (Func<Table>)(() => GetPosition(script));
        self["distance_to"] = (Func<Table, double>)DistanceTo;
        self["get_allies"] = (Func<Table>)(() => GetAllies(script));

        // Movement
        self["move_to"] = (Action<double, double>)MoveTo;
        self["move_towards"] = (Action<Table>)MoveTowards;
        self["move_away_from"] = (Action<Table>)MoveAwayFrom;

        // Combat
        self["attack"] = (Action<Table>)SetAttackAction;
        self["can_use_ability"] = (Func<string, bool>)CanUseAbility;
        self["use_ability"] = (Action<string, Table>)UseAbilityOnTarget;
        self["use_ability_at"] = (Action<string, double, double>)UseAbilityAtPosition;
        self["log"] = (Action<string>)Log;

        // Entity info
        self["id"] = _self.Id;
        self["name"] = _self.Name;
        self["hp"] = _self.Stats.Hp;
        self["max_hp"] = _self.Stats.MaxHp;

        return self;
    }
}
