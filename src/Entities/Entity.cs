using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace ScriptQuest.Entities;

public enum EntityTeam
{
    Player,
    Enemy
}

public class Entity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; }
    public Vector2 Position { get; set; }
    public Stats Stats { get; }
    public EntityTeam Team { get; set; }
    public bool IsAlive => Stats.Hp > 0;
    public bool IsStunned => StunTicksRemaining > 0;

    public int StunTicksRemaining { get; set; } = 0;
    public Dictionary<string, int> Cooldowns { get; } = new();
    public Vector2 LastMoveDirection { get; set; } = Vector2.Zero;
    public string ScriptPath { get; set; }
    public Color Color { get; set; } = Color.White;

    // Pending action from Lua script this tick
    public EntityAction? PendingAction { get; set; }

    public Entity(string name, EntityTeam team)
    {
        Name = name;
        Team = team;
        Stats = new Stats();
    }

    public bool IsOnCooldown(string abilityId) =>
        Cooldowns.TryGetValue(abilityId, out int ticks) && ticks > 0;

    public float DistanceTo(Entity other)
    {
        return Vector2.Distance(Position, other.Position);
    }
}

public enum ActionType
{
    None,
    MoveTo,
    Attack,
    UseAbility
}

public class EntityAction
{
    public ActionType Type { get; set; } = ActionType.None;
    public Vector2 TargetPosition { get; set; }
    public Entity TargetEntity { get; set; }
    public string AbilityId { get; set; }
    public Entity AbilityTarget { get; set; }
    public Vector2 AbilityPosition { get; set; }
}
