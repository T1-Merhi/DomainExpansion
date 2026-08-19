public enum ModifierOp
{
    /// <summary>Summed with other Add modifiers, applied before Mult.</summary>
    Add,

    /// <summary>Treated as a fraction: 0.5 means +50%. Multiplicative with other Mult modifiers.</summary>
    Mult,
}

public readonly record struct Modifier(StatId Stat, ModifierOp Op, float Value)
{
    public static Modifier Add(StatId stat, float value) => new(stat, ModifierOp.Add, value);
    public static Modifier Mult(StatId stat, float value) => new(stat, ModifierOp.Mult, value);
}
