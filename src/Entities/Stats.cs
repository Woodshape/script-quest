namespace ScriptQuest.Entities;

public class Stats
{
    public int MaxHp { get; set; } = 100;
    public int Hp { get; set; } = 100;
    public int MaxMana { get; set; } = 50;
    public int Mana { get; set; } = 50;
    public int Strength { get; set; } = 10;
    public int Intelligence { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Armor { get; set; } = 5;
    public float Speed { get; set; } = 2.0f; // Tiles per second
    public float AttackRange { get; set; } = 1.5f;
    public int AttackDamage { get; set; } = 10;
    public int ManaRegenPerTick { get; set; } = 1;
}
