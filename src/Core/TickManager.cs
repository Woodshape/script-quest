using Microsoft.Xna.Framework;

namespace ScriptQuest.Core;

public class TickManager
{
    public const float TickInterval = 0.1f; // 100ms per tick
    public int CurrentTick { get; private set; }

    private float _accumulator;

    public bool Update(GameTime gameTime)
    {
        _accumulator += (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_accumulator >= TickInterval)
        {
            _accumulator -= TickInterval;
            CurrentTick++;
            return true; // A tick occurred
        }

        return false;
    }
}
