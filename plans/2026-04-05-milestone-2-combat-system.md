# Plan: Milestone 2 — Combat System

**Created:** 2026-04-05
**Status:** Draft
**Requirement:** Add abilities, cooldowns, mana regen, stun, and a damage calculator to complete the combat foundation.

---

## Overview

### What this plan achieves

Extends the existing tick-based combat stub with a full ability system (Shield Bash, Fireball, Backstab, Heal), tick-based cooldowns, mana regeneration, and a stun effect. Lua scripts gain `use_ability` and `can_use_ability` calls, making combat scriptable beyond the basic `attack()`.

### Why this matters

The scripting system is the core gameplay loop. Right now players can only move and auto-attack. Abilities are the first meaningful decision point in scripts — positioning, resource management, and timing all become relevant. This is the foundation every subsequent milestone builds on.

---

## Current State

### Relevant existing structure

| File | Role |
|---|---|
| `src/Entities/Entity.cs` | Entity data: Id, Name, Position, Stats, Team, PendingAction, ScriptPath |
| `src/Entities/Stats.cs` | Hp, Mana, MaxHp, MaxMana, Strength, Intelligence, Dexterity, Armor, Speed, AttackRange, AttackDamage |
| `src/Entities/EntityManager.cs` | Registry + `ResolveActions` (MoveTo, Attack) |
| `src/Scripting/LuaEngine.cs` | Loads and runs `on_tick(self)` per entity |
| `src/Scripting/LuaAPI.cs` | Builds `self` table; exposes perception, movement, attack |
| `src/Core/TickManager.cs` | 100ms fixed tick, `CurrentTick` counter |
| `Game1.cs` | Orchestrates: scripts → resolve → clamp → render |
| `scripts/characters/warrior_default.lua` | Sample script: move + basic attack |
| `scripts/monsters/goblin.lua` | Same pattern for enemy |

### Gaps or problems being addressed

- No ability system — `use_ability` doesn't exist in Lua API
- No cooldown tracking on entities
- No mana regeneration (mana stat exists but never changes)
- No stun effect (stunned entities should skip script execution)
- Damage formula is inlined in `EntityManager.ResolveAttack` — needs extraction
- `can_use_ability` doesn't exist — scripts can't check readiness before spending resources
- `PendingAction` only supports `MoveTo` and `Attack` — no ability action type

---

## Proposed Changes

### Summary of changes

- Create `src/Combat/` module with `Ability`, `AbilityDatabase`, and `DamageCalculator`
- Add `StunTicksRemaining` and `Cooldowns` dictionary to `Entity`
- Add mana regen per tick to `Entity` (or resolved via `EntityManager`)
- Add `ActionType.UseAbility` to `EntityAction`
- Extend `EntityManager.ResolveActions` to handle ability actions
- Extract damage formula from `EntityManager` into `DamageCalculator`
- Expose `use_ability` and `can_use_ability` in `LuaAPI`
- Skip stunned entities in `LuaEngine`
- Update warrior and mage Lua scripts to demonstrate abilities

### New files to create

| File path | Purpose |
|---|---|
| `src/Combat/Ability.cs` | Data class: name, mana cost, cooldown ticks, range, effect type |
| `src/Combat/AbilityDatabase.cs` | Static registry of all 4 MVP abilities |
| `src/Combat/DamageCalculator.cs` | `Calculate(attacker, target)` — single source of truth for damage |

### Files to modify

| File path | Changes |
|---|---|
| `src/Entities/Entity.cs` | Add `StunTicksRemaining`, `Cooldowns`, `ManaRegenPerTick` |
| `src/Entities/EntityManager.cs` | Add `UseAbility` action resolution; tick cooldowns + mana regen; extract damage to `DamageCalculator` |
| `src/Scripting/LuaAPI.cs` | Add `use_ability(name, target)`, `use_ability(name, x, y)`, `can_use_ability(name)` |
| `src/Scripting/LuaEngine.cs` | Skip entities where `StunTicksRemaining > 0`; decrement stun counter |
| `scripts/characters/warrior_default.lua` | Demonstrate `shield_bash` usage |
| `scripts/characters/mage_default.lua` | Demonstrate `fireball` usage |

---

## Design Decisions

### Key decisions made

1. **Abilities queue as `PendingAction`**: Consistent with existing move/attack pattern. All scripts decide first, then actions resolve. Avoids mid-tick state conflicts.

2. **Cooldowns on Entity as `Dictionary<string, int>`**: Tick-countdown approach. Each tick that an entity is processed, all cooldown values decrement by 1. Simple, deterministic, no timer objects needed.

3. **Mana regen on Entity as `ManaRegenPerTick`**: Resolved in `EntityManager.TickEntities()` (new method called before scripts run). Keeps stat data co-located with entity. Default value: 1 mana/tick (= 10 mana/second).

4. **Stun handled in `LuaEngine`, decrement in `EntityManager`**: `LuaEngine` checks `StunTicksRemaining > 0` and skips script execution. `EntityManager` decrements it during `TickEntities`. This keeps stun logic close to where it's applied (resolution) and where it's enforced (script gating).

5. **`AbilityDatabase` is a static class**: MVP has hardcoded abilities. No JSON loading needed yet. Clean, zero-dependency, easy to extend later.

6. **Fireball targets a position (x, y), others target an entity**: Matches the plan's `use_ability("fireball", x, y)` signature. `LuaAPI` needs two overloads differentiated by argument type.

7. **`DamageCalculator` is a static class with a single `Calculate` method**: Extracted from `EntityManager.ResolveAttack`. One source of truth for `max(1, damage - armor)`.

8. **Backstab bonus from behind**: "Behind" = target's movement direction opposite to attacker. Simplified: if attacker's position is behind the target relative to the target's last move direction. For MVP, use a simpler heuristic: backstab deals bonus damage if attacker is not within the target's forward 180° arc. Since entities don't track facing yet, MVP can use: backstab always applies +50% damage if attacker is NOT the target's closest enemy (i.e., attacking from a non-primary threat direction). **Open question — see below.**

9. **Heal targets an ally table**: Uses `ResolveEntity` like attack does. Restores a fixed amount based on caster's Intelligence stat.

### Alternatives considered

- **Cooldowns as absolute tick timestamps**: Simpler lookup (`readyAt <= currentTick`) but requires passing `CurrentTick` everywhere. Countdown approach is self-contained on the entity.
- **Separate `PendingAbility` field on Entity**: Would avoid extending `ActionType` but duplicates the queuing pattern unnecessarily.
- **Mana regen in `LuaEngine` before script runs**: Tempting but mixing regen logic into the script executor is wrong separation of concerns. `EntityManager` owns entity state updates.

### Open questions

1. **Backstab "from behind" definition**: No facing/direction currently tracked on Entity. Two options:
   - (a) Skip directional check for MVP — Backstab always deals +50% damage (simpler, less interesting).
   - (b) Add `LastMoveDirection` to Entity — Backstab checks if attacker is in the rear 120° arc.
   
   **Recommendation:** Option (b) — add `LastMoveDirection` to Entity, set it during `ResolveMove`. Low cost, makes the ability meaningful.

2. **Fireball AoE radius**: Not specified in plan. Suggest 2.0 tiles. Confirm?

---

## Step-by-Step Tasks

### Step 1: Create `src/Combat/DamageCalculator.cs`

Extract the damage formula from `EntityManager.ResolveAttack` into a static class.

**Actions:**
- Create file with static method `int Calculate(Entity attacker, Entity target)`
- Formula: `Math.Max(1, attacker.Stats.AttackDamage - target.Stats.Armor)`

**Files affected:**
- `src/Combat/DamageCalculator.cs` (new)

---

### Step 2: Create `src/Combat/Ability.cs`

Define the `Ability` data class and `AbilityEffectType` enum.

**Actions:**
- Create `AbilityEffectType` enum: `MeleeAttack`, `RangedAttack`, `AoEAtPosition`, `Heal`
- Create `Ability` class with properties:
  - `string Name`
  - `string Id` (lowercase key used in Lua: `"shield_bash"`, `"fireball"`, etc.)
  - `int ManaCost`
  - `int CooldownTicks`
  - `float Range`
  - `AbilityEffectType EffectType`
  - `int BaseDamage` (for damage abilities; 0 for Heal)
  - `int BaseHeal` (for heal abilities; 0 for damage)
  - `int StunTicks` (for Shield Bash; 0 for others)
  - `float AoERadius` (for Fireball; 0 for targeted)

**Files affected:**
- `src/Combat/Ability.cs` (new)

---

### Step 3: Create `src/Combat/AbilityDatabase.cs`

Static registry with the 4 MVP abilities.

**Actions:**
- Static class with `Dictionary<string, Ability> All`
- Static constructor populates the 4 abilities:

| Id | Name | ManaCost | CooldownTicks | Range | EffectType | Notes |
|---|---|---|---|---|---|---|
| `shield_bash` | Shield Bash | 15 | 30 (3s) | 1.5 | MeleeAttack | StunTicks=20, damage = 8 |
| `fireball` | Fireball | 25 | 50 (5s) | 6.0 | AoEAtPosition | AoERadius=2.0, BaseDamage=30 |
| `backstab` | Backstab | 10 | 20 (2s) | 1.5 | MeleeAttack | BaseDamage = 150% of normal |
| `heal` | Heal | 20 | 40 (4s) | 4.0 | Heal | BaseHeal=30 |

- Static method `Ability Get(string id)` — returns ability or null

**Files affected:**
- `src/Combat/AbilityDatabase.cs` (new)

---

### Step 4: Extend `Entity.cs`

Add stun state, cooldown tracking, mana regen rate, and last move direction.

**Actions:**
- Add `public int StunTicksRemaining { get; set; } = 0`
- Add `public Dictionary<string, int> Cooldowns { get; } = new()`
- Add `public Vector2 LastMoveDirection { get; set; } = Vector2.Zero`
- Add `using System.Collections.Generic` and `using Microsoft.Xna.Framework` if not present
- Add helper: `public bool IsStunned => StunTicksRemaining > 0`
- Add helper: `public bool IsOnCooldown(string abilityId) => Cooldowns.TryGetValue(abilityId, out int ticks) && ticks > 0`

**Files affected:**
- `src/Entities/Entity.cs`

---

### Step 5: Extend `ActionType` and `EntityAction`

Add `UseAbility` action type with ability id and positional/entity target support.

**Actions:**
- Add `UseAbility` to the `ActionType` enum
- Add to `EntityAction`:
  - `public string AbilityId { get; set; }`
  - `public Entity AbilityTarget { get; set; }` (for entity-targeted abilities)
  - `public Vector2 AbilityPosition { get; set; }` (for AoE position-targeted abilities)

**Files affected:**
- `src/Entities/Entity.cs`

---

### Step 6: Extend `EntityManager`

Add mana regen + cooldown tick-down, update `ResolveMove` to track direction, update `ResolveAttack` to use `DamageCalculator`, and add `ResolveAbility`.

**Actions:**

1. Add `using ScriptQuest.Combat;` import.

2. Add new method `public void TickEntities()` — called once per tick before scripts run:
   ```
   foreach alive entity:
     - Regen mana: entity.Stats.Mana = Min(MaxMana, Mana + ManaRegenPerTick)
     - Decrement stun: if StunTicksRemaining > 0, decrement by 1
     - Decrement cooldowns: foreach key in Cooldowns where value > 0, decrement by 1
   ```

3. Update `ResolveMove` to set `entity.LastMoveDirection` to the normalized direction vector before moving.

4. Update `ResolveAttack` to use `DamageCalculator.Calculate(attacker, target)` instead of inline formula.

5. Add `ResolveAbility(Entity caster, EntityAction action)` called from `ResolveActions` switch:
   - Lookup ability from `AbilityDatabase.Get(action.AbilityId)` — if null, return
   - Apply mana cost: `caster.Stats.Mana -= ability.ManaCost`
   - Set cooldown: `caster.Cooldowns[ability.Id] = ability.CooldownTicks`
   - Switch on `ability.EffectType`:
     - `MeleeAttack` / `RangedAttack`:
       - If `AbilityTarget` is null or dead, return
       - Check range: if `caster.DistanceTo(target) > ability.Range`, return
       - If `shield_bash`: deal `ability.BaseDamage` (ignoring armor for stun ability), apply `target.StunTicksRemaining = ability.StunTicks`
       - If `backstab`: check if attacker is "behind" (dot product of `LastMoveDirection` of target and direction from target to attacker > 0.5). If behind, damage = `DamageCalculator.Calculate(caster, target)` × 1.5. Else normal Calculate.
       - Otherwise: apply `DamageCalculator.Calculate(caster, target)` + `ability.BaseDamage`
     - `AoEAtPosition`:
       - Find all enemies within `ability.AoERadius` of `action.AbilityPosition`
       - Deal `ability.BaseDamage` to each (flat, no armor reduction for AoE MVP — or use DamageCalculator, your call)
     - `Heal`:
       - Target = `action.AbilityTarget`
       - If null or not alive, return
       - Check range
       - `target.Stats.Hp = Min(target.Stats.MaxHp, target.Stats.Hp + ability.BaseHeal)`

6. In `ResolveActions`, add `case ActionType.UseAbility: ResolveAbility(entity, action); break;`

**Files affected:**
- `src/Entities/EntityManager.cs`

---

### Step 7: Update `LuaEngine.cs`

Skip script execution for stunned entities.

**Actions:**
- In `ExecuteScript`, add early return if `entity.IsStunned`
  ```csharp
  if (entity.IsStunned) return;
  ```
  Place this after the null/file check, before creating the script.

**Files affected:**
- `src/Scripting/LuaEngine.cs`

---

### Step 8: Extend `LuaAPI.cs`

Add `can_use_ability` and `use_ability` (two variants) to the self table.

**Actions:**

1. Add `using ScriptQuest.Combat;`

2. Add method `CanUseAbility(string abilityId)`:
   - Lookup ability — if not found, return false
   - Return `!_self.IsOnCooldown(abilityId) && _self.Stats.Mana >= ability.ManaCost`

3. Add method `UseAbilityOnTarget(string abilityId, Table targetTable)`:
   - Lookup ability — if null, return
   - If `!CanUseAbility(abilityId)`, return
   - `ResolveEntity(targetTable)` — if null, return
   - Set `_self.PendingAction = new EntityAction { Type = ActionType.UseAbility, AbilityId = abilityId, AbilityTarget = targetEntity }`

4. Add method `UseAbilityAtPosition(string abilityId, double x, double y)`:
   - Lookup ability — if null or `EffectType != AoEAtPosition`, return
   - If `!CanUseAbility(abilityId)`, return
   - Set `_self.PendingAction = new EntityAction { Type = ActionType.UseAbility, AbilityId = abilityId, AbilityPosition = new Vector2((float)x, (float)y) }`

5. In `CreateSelfTable`, add:
   ```csharp
   self["can_use_ability"] = (Func<string, bool>)CanUseAbility;
   self["use_ability"] = (Action<string, Table>)UseAbilityOnTarget;
   self["use_ability_at"] = (Action<string, double, double>)UseAbilityAtPosition;
   ```
   Note: MoonSharp can't overload by argument count on the same key, so use `use_ability` for entity-targeted and `use_ability_at` for position-targeted. This is clear and explicit for script authors.

**Files affected:**
- `src/Scripting/LuaAPI.cs`

---

### Step 9: Update `Game1.cs`

Call `TickEntities()` before script execution each tick.

**Actions:**
- In `Update`, inside the tick block, add `_entityManager.TickEntities();` as the **first** operation before the script loop:
  ```csharp
  if (_tickManager.Update(gameTime))
  {
      _entityManager.TickEntities(); // mana regen, cooldown countdown, stun countdown
      foreach (var entity in _entityManager.Entities)
      { ... }
  }
  ```

**Files affected:**
- `Game1.cs`

---

### Step 10: Add `ManaRegenPerTick` to `Stats.cs`

**Actions:**
- Add `public int ManaRegenPerTick { get; set; } = 1`

**Files affected:**
- `src/Entities/Stats.cs`

---

### Step 11: Update Lua scripts to demonstrate abilities

**Actions:**

`scripts/characters/warrior_default.lua`:
```lua
function on_tick(self)
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    if dist <= 1.5 and self.can_use_ability("shield_bash") then
        self.use_ability("shield_bash", target)
    elseif dist <= 1.5 then
        self.attack(target)
    else
        self.move_towards(target)
    end
end
```

`scripts/characters/mage_default.lua`:
```lua
function on_tick(self)
    local target = self.get_nearest_enemy()
    if not target then return end

    local dist = self.distance_to(target)

    if self.can_use_ability("fireball") then
        self.use_ability_at("fireball", target.x, target.y)
    elseif dist <= 5.0 then
        self.attack(target)
    else
        self.move_towards(target)
    end
end
```

**Files affected:**
- `scripts/characters/warrior_default.lua`
- `scripts/characters/mage_default.lua`

---

### Step 12: Build and verify

**Actions:**
- Run `dotnet build` — fix any compilation errors
- Run `dotnet run` — observe abilities firing in the HUD log

**Files affected:**
- None (validation only)

---

## Connections & Dependencies

### Files that reference this area

- `Game1.cs` — orchestrates the tick loop; needs `TickEntities()` added
- `src/Scripting/LuaEngine.cs` — reads `entity.IsStunned`
- `src/Scripting/LuaAPI.cs` — calls `AbilityDatabase`, reads entity cooldowns/mana

### Updates needed for consistency

- `ScriptQuest-Plan.md` already documents `use_ability` — implementation matches the plan's API surface
- The HUD in `GameRenderer.DrawHUD` shows HP/Mana — mana will now visibly change, which is good

### Impact on existing workflows

- Existing scripts (`warrior_default.lua`, `goblin.lua`) continue to work — no breaking changes to existing Lua API
- `ResolveAttack` output is unchanged (same formula, just moved to `DamageCalculator`)

---

## Validation Checklist

- [ ] `dotnet build` succeeds with zero errors
- [ ] `dotnet run` launches without exceptions
- [ ] Warrior uses `shield_bash` — goblin freezes for ~2 seconds after being hit
- [ ] Mage fires `fireball` at goblin cluster — multiple goblins take damage
- [ ] Cooldown prevents ability re-use immediately after (verify via log output)
- [ ] Mana decreases on ability use and regenerates over time (visible in HUD)
- [ ] Stunned entity does not move or attack during stun duration
- [ ] Existing goblin scripts (basic attack) still work unchanged
- [ ] `can_use_ability` returns false when on cooldown or insufficient mana

---

## Success Criteria

1. All 4 abilities (`shield_bash`, `fireball`, `backstab`, `heal`) are defined in `AbilityDatabase` and resolvable via `ResolveAbility`
2. Lua scripts can call `self.use_ability("shield_bash", target)` and `self.use_ability_at("fireball", x, y)` without error
3. Mana depletes on ability use and regenerates at `ManaRegenPerTick` per tick
4. Stunned entities skip `on_tick` execution for the stun duration
5. `dotnet build` is clean

---

## Notes

- **Backstab** is the trickiest ability. Requires `LastMoveDirection` on `Entity`. If the target hasn't moved yet (zero vector), backstab should fall back to normal damage to avoid always triggering.
- **Fireball AoE** targets a position, not an entity — this is intentional. It rewards predictive scripting (fire where the enemy will be) and is the first AoE the player encounters.
- **Future**: abilities could be loaded from `data/abilities.json` and filtered per character class. The `AbilityDatabase` static class is a stepping stone — the interface (`Get(id)`) won't change.
- **Future**: `use_ability` currently overwrites `PendingAction` like move/attack do. If we ever want multi-action ticks, this needs a queue. Not needed for MVP.
- The `use_ability` vs `use_ability_at` split in Lua avoids MoonSharp overload ambiguity and is self-documenting in scripts.
