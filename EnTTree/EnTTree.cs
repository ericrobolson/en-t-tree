namespace EnTTree;

/// <summary>
/// A generational entity identifier packed into a single 32-bit value.
/// The lower 20 bits store the index and the upper 12 bits store the generation.
/// </summary>
public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
{
    const int IndexBits = 20;
    const int GenerationBits = 12;
    const uint IndexMask = (1u << IndexBits) - 1;
    const uint GenerationMask = (1u << GenerationBits) - 1;

    /// <summary>The raw packed identifier.</summary>
    public readonly uint Id;

    /// <summary>Creates an entity from an index and generation.</summary>
    public Entity(int index, int generation)
    {
        Id = ((uint)generation << IndexBits) | (uint)index;
    }

    /// <summary>The slot index (lower 20 bits, range 0..1_048_575).</summary>
    public int Index => (int)(Id & IndexMask);

    /// <summary>The generation counter (upper 12 bits, range 0..4095).</summary>
    public int Generation => (int)((Id >> IndexBits) & GenerationMask);

    /// <summary>True when the index is at its maximum representable value.</summary>
    public bool IsMaxIndex => (uint)Index == IndexMask;

    /// <summary>True when the generation is at its maximum representable value.</summary>
    public bool IsMaxGeneration => (uint)Generation == GenerationMask;

    /// <summary>
    /// Returns the next entity in sequence. Increments the index by one;
    /// when the index wraps, the generation advances. When both are at max, both wrap to zero.
    /// </summary>
    public Entity Next()
    {
        var idx = Index + 1;
        var gen = Generation;

        if (IsMaxIndex)
        {
            idx = 0;
            gen += 1;

            if (IsMaxGeneration)
            {
                gen = 0;
            }
        }

        return new Entity(idx, gen);
    }

    /// <inheritdoc />
    public bool Equals(Entity other) => Id == other.Id;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj != null && obj is Entity e && Equals(e);

    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(Entity other) => Id.CompareTo(other.Id);

    /// <inheritdoc />
    public override string ToString() => $"Entity({Index}, {Generation})";

    public static bool operator ==(Entity a, Entity b) => a.Id == b.Id;
    public static bool operator !=(Entity a, Entity b) => a.Id != b.Id;
    public static bool operator <(Entity a, Entity b) => a.Id < b.Id;
    public static bool operator >(Entity a, Entity b) => a.Id > b.Id;
    public static bool operator <=(Entity a, Entity b) => a.Id <= b.Id;
    public static bool operator >=(Entity a, Entity b) => a.Id >= b.Id;
}

public class Registry
{

}
