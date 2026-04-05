using Microsoft.Xna.Framework;
using ScriptQuest.Entities;

namespace ScriptQuest.World;

public class Arena
{
    public int Width { get; }
    public int Height { get; }

    public Arena(int width = 20, int height = 15)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Clamp entity positions to stay within arena bounds.
    /// </summary>
    public void ClampPositions(EntityManager entityManager)
    {
        foreach (var entity in entityManager.Entities)
        {
            var pos = entity.Position;
            pos.X = MathHelper.Clamp(pos.X, 0, Width - 1);
            pos.Y = MathHelper.Clamp(pos.Y, 0, Height - 1);
            entity.Position = pos;
        }
    }
}
