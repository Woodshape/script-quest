using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
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
        if (targetTable == null) return double.MaxValue;

        // Find the entity by name from the table
        var name = targetTable.Get("name").String;
        if (name == null) return double.MaxValue;

        foreach (var entity in _entityManager.Entities)
        {
            if (entity.Name == name && entity.IsAlive)
                return _self.DistanceTo(entity);
        }

        return double.MaxValue;
    }

    public Table GetAllies(Script script)
    {
        var allies = _entityManager.GetAlliesOf(_self);
        return EntitiesToTable(script, allies);
    }

    // === Movement ===

    public void MoveTo(double x, double y)
    {
        _self.PendingAction = new EntityAction
        {
            Type = ActionType.MoveTo,
            TargetPosition = new Microsoft.Xna.Framework.Vector2((float)x, (float)y)
        };
    }

    public void MoveTowards(Table targetTable)
    {
        var targetEntity = ResolveEntity(targetTable);
        if (targetEntity == null) return;

        _self.PendingAction = new EntityAction
        {
            Type = ActionType.MoveTo,
            TargetPosition = targetEntity.Position
        };
    }

    public void MoveAwayFrom(Table targetTable)
    {
        var targetEntity = ResolveEntity(targetTable);
        if (targetEntity == null) return;

        var direction = _self.Position - targetEntity.Position;
        if (direction.LengthSquared() > 0)
        {
            direction.Normalize();
            var fleeTarget = _self.Position + direction * _self.Stats.Speed;
            _self.PendingAction = new EntityAction
            {
                Type = ActionType.MoveTo,
                TargetPosition = fleeTarget
            };
        }
    }

    // === Combat ===

    public void Attack(Table targetTable)
    {
        var targetEntity = ResolveEntity(targetTable);
        if (targetEntity == null) return;

        _self.PendingAction = new EntityAction
        {
            Type = ActionType.Attack,
            TargetEntity = targetEntity
        };
    }

    // === Helpers ===

    private Entity ResolveEntity(Table table)
    {
        if (table == null) return null;
        var name = table.Get("name").String;
        if (name == null) return null;

        foreach (var entity in _entityManager.Entities)
        {
            if (entity.Name == name && entity.IsAlive)
                return entity;
        }

        return null;
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
        self["attack"] = (Action<Table>)Attack;

        // Entity info
        self["name"] = _self.Name;
        self["hp"] = _self.Stats.Hp;
        self["max_hp"] = _self.Stats.MaxHp;

        return self;
    }
}
