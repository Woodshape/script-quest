using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ScriptQuest.Core;
using ScriptQuest.Entities;
using ScriptQuest.Rendering;
using ScriptQuest.Scripting;
using ScriptQuest.World;

namespace ScriptQuest;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private TickManager _tickManager;
    private EntityManager _entityManager;
    private LuaEngine _luaEngine;
    private GameRenderer _renderer;
    private Arena _arena;

    private string _scriptsBasePath;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1024;
        _graphics.PreferredBackBufferHeight = 768;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _tickManager = new TickManager();
        _entityManager = new EntityManager();
        _luaEngine = new LuaEngine();
        _arena = new Arena(20, 15);

        _scriptsBasePath = Path.Combine(Directory.GetCurrentDirectory(), "scripts");

        // --- Player Party ---
        var warrior = new Entity("Warrior", EntityTeam.Player)
        {
            Position = new Vector2(3, 7),
            Color = Color.DodgerBlue,
            ScriptPath = Path.Combine(_scriptsBasePath, "characters", "warrior_default.lua")
        };
        warrior.Stats.MaxHp = 120;
        warrior.Stats.Hp = 120;
        warrior.Stats.AttackDamage = 15;
        warrior.Stats.Armor = 8;
        warrior.Stats.Speed = 2.5f;
        warrior.Stats.AttackRange = 1.5f;

        var mage = new Entity("Mage", EntityTeam.Player)
        {
            Position = new Vector2(2, 5),
            Color = Color.MediumPurple,
            ScriptPath = Path.Combine(_scriptsBasePath, "characters", "mage_default.lua")
        };
        mage.Stats.MaxHp = 60;
        mage.Stats.Hp = 60;
        mage.Stats.AttackDamage = 20;
        mage.Stats.Armor = 2;
        mage.Stats.Speed = 1.8f;
        mage.Stats.AttackRange = 5.0f;
        mage.Stats.Intelligence = 20;

        // --- Enemies ---
        var goblin1 = new Entity("Goblin_1", EntityTeam.Enemy)
        {
            Position = new Vector2(15, 6),
            Color = Color.OliveDrab,
            ScriptPath = Path.Combine(_scriptsBasePath, "monsters", "goblin.lua")
        };
        goblin1.Stats.MaxHp = 40;
        goblin1.Stats.Hp = 40;
        goblin1.Stats.AttackDamage = 8;
        goblin1.Stats.Speed = 3.0f;

        var goblin2 = new Entity("Goblin_2", EntityTeam.Enemy)
        {
            Position = new Vector2(16, 9),
            Color = Color.DarkOliveGreen,
            ScriptPath = Path.Combine(_scriptsBasePath, "monsters", "goblin.lua")
        };
        goblin2.Stats.MaxHp = 40;
        goblin2.Stats.Hp = 40;
        goblin2.Stats.AttackDamage = 8;
        goblin2.Stats.Speed = 3.0f;

        var goblin3 = new Entity("Goblin_3", EntityTeam.Enemy)
        {
            Position = new Vector2(17, 7),
            Color = Color.DarkGreen,
            ScriptPath = Path.Combine(_scriptsBasePath, "monsters", "goblin.lua")
        };
        goblin3.Stats.MaxHp = 50;
        goblin3.Stats.Hp = 50;
        goblin3.Stats.AttackDamage = 10;
        goblin3.Stats.Speed = 2.5f;

        _entityManager.Add(warrior);
        _entityManager.Add(mage);
        _entityManager.Add(goblin1);
        _entityManager.Add(goblin2);
        _entityManager.Add(goblin3);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        var font = Content.Load<SpriteFont>("DefaultFont");
        _renderer = new GameRenderer(_spriteBatch, GraphicsDevice, font);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
            || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        // Process tick
        if (_tickManager.Update(gameTime))
        {
            // 1. Run Lua scripts for all living entities
            foreach (var entity in _entityManager.Entities)
            {
                if (entity.IsAlive)
                {
                    _luaEngine.ExecuteScript(entity, _entityManager);
                }
            }

            // 2. Resolve all actions
            _entityManager.ResolveActions(TickManager.TickInterval);

            // 3. Clamp positions to arena
            _arena.ClampPositions(_entityManager);
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(20, 20, 30));

        _renderer.Begin();

        // Draw arena grid
        _renderer.DrawGrid(_arena.Width, _arena.Height);

        // Draw entities
        foreach (var entity in _entityManager.Entities)
        {
            _renderer.DrawEntity(entity);
        }

        // Draw HUD
        _renderer.DrawHUD(_entityManager, _tickManager.CurrentTick);

        _renderer.End();

        base.Draw(gameTime);
    }
}
