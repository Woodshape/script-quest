using System.Collections.Generic;

namespace ScriptQuest.Combat;

public class CombatLog
{
    private readonly List<string> _entries = new();
    private readonly int _maxEntries;

    public int CurrentTick { get; set; }
    public IReadOnlyList<string> Recent => _entries;

    public CombatLog(int maxEntries = 24)
    {
        _maxEntries = maxEntries;
    }

    public void Add(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _entries.Add($"[T{CurrentTick:0000}] {message}");

        if (_entries.Count > _maxEntries)
            _entries.RemoveAt(0);
    }

    public void Clear()
    {
        _entries.Clear();
    }
}
