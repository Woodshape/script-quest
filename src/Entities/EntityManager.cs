using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;

namespace ScriptQuest.Entities;

public class EntityManager
{
    private readonly List<Entity> _entities = new();

    public IReadOnlyList<Entity> Entities => _entities;

    public void Add(Entity entity)
    {
        _entities.Add(entity);
    }

    public void Remove(Entity entity)
    {
        _entities.Remove(entity);
    }

    public List<Entity> GetEnemiesOf(Entity entity)
    {
        return _entities
            .Where(e => e.IsAlive && e.Team != entity.Team)
            .ToList();
    }

    public List<Entity> GetAlliesOf(Entity entity)
    {
        return _entities
            .Where(e => e.IsAlive && e.Team == entity.Team && e != entity)
            .ToList();
    }

    public List<Entity> GetEnemiesInRange(Entity entity, float range)
    {
        return GetEnemiesOf(entity)
            .Where(e => entity.DistanceTo(e) <= range)
            .ToList();
    }

    public Entity GetNearestEnemy(Entity entity)
    {
        return GetEnemiesOf(entity)
            .OrderBy(e => entity.DistanceTo(e))
            .FirstOrDefault();
    }

    public Entity GetEntityById(string id)
    {
        return _entities.FirstOrDefault(e => e.Id.Equals(id));
    }

    /// <summary>
    /// Resolve all pending actions after scripts have run.
    /// </summary>
    public void ResolveActions(float tickInterval)
    {
        foreach (var entity in _entities.Where(e => e.IsAlive && e.PendingAction != null))
        {
            var action = entity.PendingAction;
            switch (action.Type)
            {
                case ActionType.MoveTo:
                    ResolveMove(entity, action.TargetPosition, tickInterval);
                    break;
                case ActionType.Attack:
                    ResolveAttack(entity, action.TargetEntity);
                    break;
            }

            entity.PendingAction = null;
        }

        // Remove dead entities (or keep them for death animation later)
    }

    private void ResolveMove(Entity entity, Vector2 target, float tickInterval)
    {
        var direction = target - entity.Position;
        if (direction.LengthSquared() < 0.01f)
            return;

        direction.Normalize();
        var maxDistance = entity.Stats.Speed * tickInterval;
        var toTarget = target - entity.Position;

        if (toTarget.Length() <= maxDistance)
            entity.Position = target;
        else
            entity.Position += direction * maxDistance;
    }

    private void ResolveAttack(Entity attacker, Entity target)
    {
        if (target == null || !target.IsAlive)
            return;

        if (attacker.DistanceTo(target) > attacker.Stats.AttackRange)
            return;

        int damage = System.Math.Max(1, attacker.Stats.AttackDamage - target.Stats.Armor);
        target.Stats.Hp = System.Math.Max(0, target.Stats.Hp - damage);
    }
}
