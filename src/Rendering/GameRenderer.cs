using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ScriptQuest.Combat;
using ScriptQuest.Entities;

namespace ScriptQuest.Rendering;

public class GameRenderer
{
    public const int TileSize = 32;

    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private Vector2 _cameraOffset;

    public GameRenderer(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _spriteBatch = spriteBatch;
        _font = font;

        // Create a 1x1 white pixel for drawing rectangles
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _cameraOffset = Vector2.Zero;
    }

    public void Begin()
    {
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
    }

    public void End()
    {
        _spriteBatch.End();
    }

    public void DrawGrid(int width, int height)
    {
        var gridColor = new Color(40, 40, 40);
        for (int x = 0; x <= width; x++)
        {
            DrawRect(x * TileSize + (int)_cameraOffset.X, (int)_cameraOffset.Y,
                1, height * TileSize, gridColor);
        }
        for (int y = 0; y <= height; y++)
        {
            DrawRect((int)_cameraOffset.X, y * TileSize + (int)_cameraOffset.Y,
                width * TileSize, 1, gridColor);
        }
    }

    public void DrawEntity(Entity entity)
    {
        if (!entity.IsAlive) return;

        int size = TileSize - 4;
        int x = (int)(entity.Position.X * TileSize + _cameraOffset.X) + 2;
        int y = (int)(entity.Position.Y * TileSize + _cameraOffset.Y) + 2;

        // Body
        DrawRect(x, y, size, size, entity.Color);

        // HP bar background
        int barWidth = size;
        int barHeight = 4;
        int barY = y - 8;
        DrawRect(x, barY, barWidth, barHeight, Color.DarkRed);

        // HP bar fill
        float hpRatio = (float)entity.Stats.Hp / entity.Stats.MaxHp;
        DrawRect(x, barY, (int)(barWidth * hpRatio), barHeight, Color.LimeGreen);

        // Name label
        var nameSize = _font.MeasureString(entity.Name);
        _spriteBatch.DrawString(_font, entity.Name,
            new Vector2(x + size / 2f - nameSize.X / 2f, barY - 16), Color.White);
    }

    public void DrawHUD(EntityManager entityManager, CombatLog combatLog, int tickCount)
    {
        int y = 10;
        _spriteBatch.DrawString(_font, $"Tick: {tickCount}", new Vector2(10, y), Color.Yellow);
        y += 20;

        foreach (var entity in entityManager.Entities)
        {
            var color = entity.IsAlive ? Color.White : Color.Gray;
            string status = entity.IsAlive
                ? $"{entity.Name} [{entity.Team}] HP:{entity.Stats.Hp}/{entity.Stats.MaxHp} Mana:{entity.Stats.Mana}/{entity.Stats.MaxMana} Stun:{entity.StunTicksRemaining} Pos:({entity.Position.X:F1},{entity.Position.Y:F1})"
                : $"{entity.Name} [DEAD]";
            _spriteBatch.DrawString(_font, status, new Vector2(10, y), color);
            y += 16;
        }

        DrawCombatLog(combatLog);
    }

    private void DrawCombatLog(CombatLog combatLog)
    {
        var entries = combatLog.Recent.TakeLast(10).ToList();
        if (entries.Count == 0)
            return;

        const int panelX = 10;
        const int lineHeight = 16;
        const int panelWidth = 980;
        int panelHeight = 10 + (entries.Count * lineHeight) + 10;
        int panelY = 768 - panelHeight - 10;

        DrawRect(panelX, panelY, panelWidth, panelHeight, new Color(0, 0, 0, 180));
        _spriteBatch.DrawString(_font, "Combat Log", new Vector2(panelX + 8, panelY + 6), Color.Orange);

        int textY = panelY + 24;
        foreach (var entry in entries)
        {
            _spriteBatch.DrawString(_font, entry, new Vector2(panelX + 8, textY), Color.LightGray);
            textY += lineHeight;
        }
    }

    private void DrawRect(int x, int y, int width, int height, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }
}
