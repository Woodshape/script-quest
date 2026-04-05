# ScriptQuest — Design & Architektur-Dokument

## Context

Ein Idle-RPG, bei dem die zentrale Mechanik das Programmieren ist: Spieler schreiben Lua-Scripts, um ihre Charaktere zu steuern. Nichts passiert automatisch — jede Bewegung, jeder Angriff, jede Entscheidung muss geskriptet werden. High-Level-Befehle werden als Spielbelohnungen freigeschaltet.

**Tech-Stack:** C# / .NET + MonoGame (2D Rendering) + MoonSharp (Lua-Interpreter)

---

## 1. Spielübersicht

| Aspekt | Details |
|---|---|
| Genre | Idle-RPG mit Lua-Scripting als Kernmechanik |
| Perspektive | 2D Top-Down |
| Grafik | Einfache Pixel-Art / Spritesheets (MVP) |
| Plattform | Desktop (Windows/Linux/Mac via MonoGame) |
| Multiplayer | Nein (vorerst) |
| Kampfsystem | Echtzeit, Tick-basiert |

### Spielwelt (MVP)

- **Dorf:** Shop (kaufen/verkaufen), Rekrutierung neuer Charaktere, Leveling/Skill-Trees
- **Dungeon:** Lineare Level-Folge, jedes Level eine Arena. Gegner skalieren mit Level-Tiefe. Kein Proc-Gen — handdesignte oder formelbasierte Arenen

---

## 2. Lua-Scripting-System (Kernmechanik)

### 2.1 Philosophie

- **Alles wird geskriptet:** Bewegung, Zielauswahl, Angriffe, Fähigkeiten, Mana-Management, Ausrüstung, Heiltränke, Handel
- **Granulare API als Basis:** Spieler starten mit Low-Level-Befehlen
- **High-Level-Befehle als Belohnung:** Im Spielverlauf schaltet man abstraktere Script-Bausteine frei (z.B. `auto_target_nearest()` statt manueller Positionsberechnung)
- **Monster ebenfalls Lua-gesteuert:** Keine versteckte KI — alles transparent via Scripts
- **Scripts sind Dateien:** Alle Scripts liegen lokal im Dateisystem, können extern editiert werden
- **Script-Sharing:** Spieler können Scripts teilen und importieren

### 2.2 Tick-System

```
Game Loop:
  Jeder Tick (z.B. 100ms):
    1. Für jede Entity (Spieler & Monster):
       → Lua-Script ausführen (sandboxed, zeitbegrenzt)
       → Script gibt Aktionen zurück
    2. Aktionen auflösen (Bewegung, Kampf, Items)
    3. Zustand aktualisieren (HP, Mana, Cooldowns, Positionen)
    4. Rendering
```

### 2.3 Lua-API (MVP) — Granulare Befehle

```lua
-- === Wahrnehmung ===
local enemies = self:get_enemies_in_range(radius)
local nearest = self:get_nearest_enemy()
local hp = self:get_hp()
local max_hp = self:get_max_hp()
local mana = self:get_mana()
local pos = self:get_position()
local target_pos = target:get_position()
local distance = self:distance_to(target)
local allies = self:get_allies()
local inventory = self:get_inventory()

-- === Bewegung ===
self:move_to(x, y)
self:move_towards(target)
self:move_away_from(target)

-- === Kampf ===
self:attack(target)                    -- Basis-Angriff (Nah/Fern je nach Waffe)
self:use_ability("shield_bash", target) -- Fähigkeit nutzen
self:use_ability("fireball", x, y)     -- Flächenschaden auf Position

-- === Items ===
self:use_item("health_potion")
self:equip("iron_sword")
self:unequip("shield")

-- === Handel (nur im Dorf) ===
shop:buy("health_potion", 5)
shop:sell("rusty_sword")
```

### 2.4 High-Level-Befehle (Belohnungen)

Werden im Spielverlauf freigeschaltet — Komfort-Wrapper:

```lua
-- Freigeschaltet nach Dungeon Level 5:
self:auto_attack_nearest()

-- Freigeschaltet nach erstem Boss:
self:auto_heal_if_below(0.3)  -- Heile wenn HP < 30%

-- Freigeschaltet durch Quest:
self:auto_loot()
```

### 2.5 Beispiel: Krieger-Script

```lua
function on_tick(self)
    local hp_pct = self:get_hp() / self:get_max_hp()

    -- Heiltrank wenn HP niedrig
    if hp_pct < 0.25 and self:has_item("health_potion") then
        self:use_item("health_potion")
        return
    end

    -- Nächsten Gegner finden
    local target = self:get_nearest_enemy()
    if not target then return end

    local dist = self:distance_to(target)

    -- Shield Bash wenn in Reichweite und Ability bereit
    if dist <= 1.5 and self:can_use_ability("shield_bash") then
        self:use_ability("shield_bash", target)
    -- Sonst nähern und angreifen
    elseif dist <= 1.5 then
        self:attack(target)
    else
        self:move_towards(target)
    end
end
```

### 2.6 Sandboxing & Sicherheit

- MoonSharp Sandbox: Kein Zugriff auf Dateisystem, Netzwerk, OS-Funktionen
- **Tick-Timeout:** Max. Ausführungszeit pro Script pro Tick (z.B. 5ms)
- Nur exponierte API-Funktionen verfügbar
- Fehlerbehandlung: Script-Fehler stoppen den Charakter, crashen nicht das Spiel

### 2.7 Script-Editor (In-Game)

- Syntax-Highlighting für Lua
- Autocomplete für die exponierte API
- Echtzeit-Fehlermeldungen
- Log/Debug-Konsole pro Charakter
- Scripts werden als `.lua`-Dateien im lokalen Projektordner gespeichert
- Extern editierbar (VS Code etc.), Hot-Reload bei Dateiänderung

---

## 3. RPG-Systeme

### 3.1 Charaktere & Klassen

**MVP-Klassen:**

| Klasse | Rolle | Basis-Abilities |
|---|---|---|
| Krieger | Nahkampf-Tank | Shield Bash (Stun), Heavy Strike |
| Magier | Fernkampf-AoE | Fireball (AoE), Frost Bolt (Slow) |
| Dieb | Nahkampf-DPS | Backstab (Bonus von hinten), Stealth |
| Priester | Heiler/Support | Heal, Smite, Buff |

**Stats:** HP, Mana, Stärke, Intelligenz, Geschicklichkeit, Rüstung, Geschwindigkeit

**Leveling:** XP-basiert. Bei Level-Up: Stat-Punkte verteilen + Skill-Tree-Punkte.

### 3.2 Skill-Trees

Jede Klasse hat einen eigenen Skill-Tree:
- Aktive Abilities (neue Lua-Befehle werden freigeschaltet!)
- Passive Boni (Stat-Multiplikatoren)
- High-Level-Script-Bausteine als spezielle Skill-Tree-Nodes

**Wichtig:** Neue Abilities = neue API-Funktionen im Lua-Script. Der Skill-Tree erweitert direkt die Scripting-Möglichkeiten.

### 3.3 Loot-System (Diablo-Style)

**Rarities:** Common → Uncommon → Rare → Epic → Legendary

**Loot-Drops:** Zufällig generiert mit:
- Basistyp (Schwert, Stab, Rüstung, Ring, ...)
- Rarity bestimmt Anzahl der Bonus-Affixe
- Affixe: +Stärke, +Feuerresistenz, +Angriffsgeschwindigkeit, etc.
- Legendary-Items: Einzigartige Effekte (z.B. "Fireball hat 20% Chance, doppelt zu casten")

**Loot-Evaluierung per Script:** Spieler können Scripts schreiben, die Loot automatisch bewerten und sortieren:

```lua
function evaluate_loot(item)
    if item.rarity >= RARE and item.type == "sword" then
        return "keep"
    end
    return "sell"
end
```

### 3.4 Economy

- **Gold:** Hauptwährung, durch Monster-Kills und Verkauf
- **Shop:** Basis-Equipment, Heiltränke, Mana-Tränke, Skill-Resets
- **Verkauf:** Alles verkaufbar, Preis basiert auf Rarity/Level
- **Handel per Script:** Spieler können Buy/Sell-Logik automatisieren

### 3.5 Party-System

- 1 Party mit 4–5 Mitgliedern (MVP)
- Jedes Mitglied hat ein eigenes Lua-Script
- Scripts können Allies abfragen → koordiniertes Verhalten möglich
- Rekrutierung im Dorf: Neue Charaktere mit zufälligen Basis-Stats

---

## 4. Kampfsystem

### 4.1 Echtzeit-Tick-Kampf

- Alle Entities agieren gleichzeitig pro Tick
- **Angriffsreichweite:** Nahkampf (1–2 Tiles), Fernkampf (5+ Tiles)
- **Cooldowns:** Abilities haben Tick-basierte Cooldowns
- **Mana:** Abilities kosten Mana, regeneriert langsam pro Tick
- **Aggro:** Monster haben ein einfaches Aggro-System (per Lua gesteuert)

### 4.2 MVP-Abilities

| Ability | Klasse | Typ | Effekt |
|---|---|---|---|
| Shield Bash | Krieger | Nahkampf | Schaden + Stun (X Ticks) |
| Heavy Strike | Krieger | Nahkampf | Hoher Einzelschaden |
| Fireball | Magier | AoE | Flächenschaden auf Position |
| Frost Bolt | Magier | Fernkampf | Schaden + Slow |
| Backstab | Dieb | Nahkampf | Bonus-Schaden von hinten |
| Stealth | Dieb | Self | Unsichtbar für X Ticks |
| Heal | Priester | Ally | HP wiederherstellen |
| Smite | Priester | Fernkampf | Heiliger Schaden |

### 4.3 Monster (Lua-gesteuert)

Monster verwenden dieselbe Lua-API wie Spieler. Beispiel Goblin:

```lua
function on_tick(self)
    local target = self:get_nearest_enemy()
    if not target then return end

    if self:distance_to(target) <= 1.5 then
        self:attack(target)
    else
        self:move_towards(target)
    end
end
```

Designer schreibt die Monster-Scripts. Spieler könnten sie theoretisch einsehen → Transparenz.

---

## 5. Architektur

```
┌─────────────────────────────────────────────────┐
│                   MonoGame                       │
│              (Rendering, Input, Audio)           │
├─────────────────────────────────────────────────┤
│                  Game Engine                     │
│  ┌───────────┐  ┌──────────┐  ┌──────────────┐ │
│  │ Tick       │  │ Entity   │  │ Scene        │ │
│  │ Manager   │  │ Manager  │  │ Manager      │ │
│  │ (100ms)   │  │          │  │ (Dorf/Dungeon│ │
│  └─────┬─────┘  └────┬─────┘  └──────────────┘ │
│        │              │                          │
│  ┌─────▼──────────────▼─────┐                   │
│  │    Lua Script Engine      │                   │
│  │    (MoonSharp Sandbox)    │                   │
│  │  ┌──────────────────────┐ │                   │
│  │  │ Exposed API Layer    │ │                   │
│  │  │ (movement, combat,   │ │                   │
│  │  │  items, perception)  │ │                   │
│  │  └──────────────────────┘ │                   │
│  └───────────────────────────┘                   │
│                                                  │
│  ┌────────────┐ ┌──────────┐ ┌───────────────┐  │
│  │ Combat     │ │ Loot     │ │ Economy       │  │
│  │ System     │ │ Generator│ │ System        │  │
│  └────────────┘ └──────────┘ └───────────────┘  │
│                                                  │
│  ┌────────────────────────────────────────────┐  │
│  │ In-Game Script Editor                      │  │
│  │ (Syntax Highlighting, Autocomplete, Debug) │  │
│  └────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────┤
│              Dateisystem                         │
│  scripts/  data/  saves/                         │
└─────────────────────────────────────────────────┘
```

### 5.1 Projektstruktur

```
ScriptQuest/
├── ScriptQuest.sln
├── src/
│   ├── Core/                    # Game Loop, Tick Manager
│   │   ├── GameLoop.cs
│   │   ├── TickManager.cs
│   │   └── TimeManager.cs
│   ├── Entities/                # Characters, Monsters
│   │   ├── Entity.cs
│   │   ├── Character.cs
│   │   ├── Monster.cs
│   │   ├── Stats.cs
│   │   └── Party.cs
│   ├── Scripting/               # MoonSharp Integration
│   │   ├── LuaEngine.cs         # Sandbox Setup, Script Loading
│   │   ├── LuaAPI.cs            # Exponierte API-Funktionen
│   │   ├── ScriptManager.cs     # Hot-Reload, Dateiwatcher
│   │   └── ScriptSandbox.cs     # Sicherheit, Timeouts
│   ├── Combat/                  # Kampflogik
│   │   ├── CombatSystem.cs
│   │   ├── Ability.cs
│   │   ├── AbilityDatabase.cs
│   │   └── DamageCalculator.cs
│   ├── Items/                   # Loot & Inventar
│   │   ├── Item.cs
│   │   ├── LootGenerator.cs
│   │   ├── Inventory.cs
│   │   └── ItemDatabase.cs
│   ├── World/                   # Szenen
│   │   ├── Village.cs
│   │   ├── Dungeon.cs
│   │   ├── DungeonLevel.cs
│   │   └── Shop.cs
│   ├── Rendering/               # MonoGame Rendering
│   │   ├── SpriteRenderer.cs
│   │   ├── TileMap.cs
│   │   ├── Camera.cs
│   │   └── UI/
│   │       ├── ScriptEditor.cs
│   │       ├── InventoryUI.cs
│   │       ├── PartyUI.cs
│   │       └── DebugConsole.cs
│   └── Data/                    # Serialisierung
│       ├── SaveManager.cs
│       └── DataLoader.cs
├── content/                     # MonoGame Content Pipeline
│   ├── sprites/
│   ├── tilesets/
│   └── fonts/
├── scripts/                     # Lua Scripts (vom Spieler editierbar)
│   ├── characters/
│   │   ├── warrior_default.lua
│   │   ├── mage_default.lua
│   │   └── ...
│   ├── monsters/                # Monster-Verhalten
│   │   ├── goblin.lua
│   │   ├── skeleton.lua
│   │   └── ...
│   └── library/                 # Freigeschaltete High-Level-Bausteine
│       ├── auto_target.lua
│       └── auto_heal.lua
├── data/                        # Game Data (JSON/YAML)
│   ├── abilities.json
│   ├── items.json
│   ├── monsters.json
│   └── dungeon_levels.json
└── saves/                       # Spielstände
```

---

## 6. MVP-Scope & Meilensteine

### Meilenstein 1: Lua-Engine & Grundgerüst
- MonoGame-Projekt aufsetzen
- MoonSharp integrieren, Sandbox konfigurieren
- Tick-System implementieren
- Entity-Basisklasse mit Stats
- Lua-API: Bewegung + Wahrnehmung
- Einfaches 2D-Rendering (farbige Rechtecke als Platzhalter)

### Meilenstein 2: Kampfsystem
- Basis-Angriff (Nah/Fern)
- HP/Mana-System
- 4 Abilities: Shield Bash, Fireball, Backstab, Heal
- Cooldowns, Mana-Kosten
- Schadens- und Stun-Berechnung
- Monster mit eigenen Lua-Scripts

### Meilenstein 3: Dungeon
- Dungeon mit 10 Arena-Levels
- Gegnerskalierung pro Level
- Loot-Drops nach Kampf
- Dungeon-Fortschritt (Level für Level)

### Meilenstein 4: Dorf & Economy
- Shop: Kaufen/Verkaufen
- Inventar-Management
- Charakter-Rekrutierung
- XP/Leveling-System
- Einfacher Skill-Tree (3–4 Nodes pro Klasse)

### Meilenstein 5: Script-Editor & Polish
- In-Game Lua-Editor mit Syntax-Highlighting
- Autocomplete für API
- Debug-Konsole / Log pro Charakter
- Hot-Reload bei Dateiänderung
- Loot-Generator (Diablo-Style Rarities + Affixe)

### Meilenstein 6: High-Level-Belohnungen
- Freischaltbare Script-Bausteine
- Integration in Skill-Tree / Quest-Rewards
- Script-Sharing (Import/Export)

---

## 7. Verifizierung

Nach jedem Meilenstein:
1. **Lua-Scripts testen:** Können Characters mit Scripts gesteuert werden?
2. **Sandbox prüfen:** Können Scripts auf verbotene APIs zugreifen? (Sollte fehlschlagen)
3. **Tick-Performance:** Laufen 5+ Scripts gleichzeitig ohne Frame-Drops?
4. **Hot-Reload:** Ändert man ein Script extern, wird es im Spiel sofort übernommen?
5. **Kampf E2E:** Party von 4 Charakteren kämpft gegen Gegnergruppe — alles per Lua gesteuert
