using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using ScriptQuest.Combat;

namespace ScriptQuest.Entities;

public class EntityManager
{
	private readonly List<Entity> _entities = new();
	private readonly CombatLog _combatLog;

	public IReadOnlyList<Entity> Entities => _entities;
	public CombatLog CombatLog => _combatLog;

	public EntityManager(CombatLog combatLog)
	{
		_combatLog = combatLog;
	}

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

	public Entity? GetNearestEnemy(Entity entity)
	{
		return GetEnemiesOf(entity)
			.OrderBy(e => entity.DistanceTo(e))
			.FirstOrDefault();
	}

	public Entity? GetEntityById(string id)
	{
		return _entities.FirstOrDefault(e => e.Id.Equals(id));
	}

	/// <summary>
	/// Tick mana regen, cooldowns, and stun counters. Call once per tick before scripts run.
	/// </summary>
	public void TickEntities()
	{
		foreach (var entity in _entities.Where(e => e.IsAlive))
		{
			entity.Stats.Mana = Math.Min(entity.Stats.MaxMana, entity.Stats.Mana + entity.Stats.ManaRegenPerTick);

			if (entity.StunTicksRemaining > 0)
				entity.StunTicksRemaining--;

			foreach (var key in entity.Cooldowns.Keys.ToList())
			{
				if (entity.Cooldowns[key] <= 0)
					continue;

				entity.Cooldowns[key]--;

				if (entity.Cooldowns[key] == 0)
				{
					var ability = AbilityDatabase.Get(key);
					string abilityName = ability?.Name ?? key;
					_combatLog.Add($"{entity.Name}'s {abilityName} is ready again");
				}
			}
		}
	}

	/// <summary>
	/// Resolve all pending actions after scripts have run.
	/// </summary>
	public void ResolveActions(float tickInterval)
	{
		foreach (var entity in _entities.Where(e => e.IsAlive && e.PendingAction != null))
		{
			var action = entity.PendingAction;
			if (action == null) return; // stupid compiler

			switch (action.Type)
			{
				case ActionType.MoveTo:
					ResolveMove(entity, action.TargetPosition, tickInterval);
					break;
				case ActionType.Attack:
					ResolveAttack(entity, action.TargetEntity);
					break;
				case ActionType.UseAbility:
					ResolveAbility(entity, action);
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
		entity.LastMoveDirection = direction;

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

		int damage = DamageCalculator.Calculate(attacker, target);
		target.Stats.Hp = System.Math.Max(0, target.Stats.Hp - damage);
		// _combatLog.Add($"{attacker.Name} attacked {target.Name} for {damage} damage");
	}

	private void ResolveAbility(Entity caster, EntityAction action)
	{
		var ability = AbilityDatabase.Get(action.AbilityId);
		if (ability == null)
		{
			_combatLog.Add($"{caster.Name} failed to use unknown ability '{action.AbilityId}'");
			return;
		}

		if (caster.IsOnCooldown(ability.Id) || caster.Stats.Mana < ability.ManaCost)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name} because it was not ready");
			return;
		}

		bool abilityResolved = ability.EffectType switch
		{
			AbilityEffectType.MeleeAttack or AbilityEffectType.RangedAttack
				=> ResolveAbilityTargeted(caster, action.AbilityTarget, ability),
			AbilityEffectType.AoEAtPosition
				=> ResolveAbilityAoE(caster, action.AbilityPosition, ability),
			AbilityEffectType.Heal
				=> ResolveAbilityHeal(caster, action.AbilityTarget, ability),
			_ => false
		};

		if (!abilityResolved)
			return;

		caster.Stats.Mana -= ability.ManaCost;
		caster.Cooldowns[ability.Id] = ability.CooldownTicks;
		_combatLog.Add($"{caster.Name}'s {ability.Name} is on cooldown for {ability.CooldownTicks} ticks");
	}

	private bool ResolveAbilityTargeted(Entity caster, Entity target, Ability ability)
	{
		if (target == null || !target.IsAlive)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was invalid");
			return false;
		}

		if (target.Team == caster.Team)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was an ally");
			return false;
		}

		if (caster.DistanceTo(target) > ability.Range)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was out of range");
			return false;
		}

		int damage;
		if (ability.Id == "backstab")
		{
			// Behind check: attacker is behind if target's last move direction points away from attacker
			var toAttacker = caster.Position - target.Position;
			bool isBehind = target.LastMoveDirection.LengthSquared() > 0 &&
							Vector2.Dot(target.LastMoveDirection, toAttacker) > 0;
			damage = isBehind
				? (int)(DamageCalculator.Calculate(caster, target) * 1.5f)
				: DamageCalculator.Calculate(caster, target);
		}
		else
		{
			damage = ability.BaseDamage + DamageCalculator.Calculate(caster, target);
		}

		target.Stats.Hp = Math.Max(0, target.Stats.Hp - damage);

		if (ability.StunTicks > 0)
		{
			target.StunTicksRemaining = ability.StunTicks;
			_combatLog.Add($"{caster.Name} used {ability.Name} on {target.Name} for {damage} damage and stunned for {ability.StunTicks} ticks");
			return true;
		}

		_combatLog.Add($"{caster.Name} used {ability.Name} on {target.Name} for {damage} damage");

		return true;
	}

	private bool ResolveAbilityAoE(Entity caster, Vector2 position, Ability ability)
	{
		if (Vector2.Distance(caster.Position, position) > ability.Range)
		{
			_combatLog.Add($"{caster.Name} failed to cast {ability.Name}: target point was out of range");
			return false;
		}

		var targets = GetEnemiesOf(caster)
			.Where(e => Vector2.Distance(e.Position, position) <= ability.AoERadius)
			.ToList();

		if (targets.Count == 0)
		{
			_combatLog.Add($"{caster.Name} cast {ability.Name} but hit no targets");
			return false;
		}

		foreach (var target in targets)
			target.Stats.Hp = Math.Max(0, target.Stats.Hp - ability.BaseDamage);

		_combatLog.Add($"{caster.Name} cast {ability.Name} at ({position.X:F1}, {position.Y:F1}) hitting {targets.Count} targets ({string.Join(" ", targets.Select(p => p.Name))}) for {ability.BaseDamage} damage");

		return true;
	}

	private bool ResolveAbilityHeal(Entity caster, Entity target, Ability ability)
	{
		if (target == null || !target.IsAlive)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was invalid");
			return false;
		}

		if (target.Team != caster.Team)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was not an ally");
			return false;
		}

		if (caster.DistanceTo(target) > ability.Range)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was out of range");
			return false;
		}

		if (target.Stats.Hp >= target.Stats.MaxHp)
		{
			_combatLog.Add($"{caster.Name} failed to use {ability.Name}: target was already at full health");
			return false;
		}

		target.Stats.Hp = Math.Min(target.Stats.MaxHp, target.Stats.Hp + ability.BaseHeal);
		_combatLog.Add($"{caster.Name} used {ability.Name} on {target.Name} for {ability.BaseHeal} healing");
		return true;
	}
}
