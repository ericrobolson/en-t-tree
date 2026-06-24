/*
MIT License

Copyright (c) 2026 Eric Olson

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

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
    internal const int MaxEntities = (1 << IndexBits);

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

/// <summary>
/// Provides a unique integer ID for each component type, assigned on first access.
/// Uses a static generic class so each T gets its own static field.
/// </summary>
internal static class ComponentId<T> where T : struct
{
    /// <summary>The unique integer ID for component type T.</summary>
    public static readonly int Value = ComponentIdCounter.Next();
}

/// <summary>
/// Thread-safe counter backing <see cref="ComponentId{T}"/>.
/// </summary>
internal static class ComponentIdCounter
{
    static int _next;

    /// <summary>Returns the next available component ID.</summary>
    public static int Next() => Interlocked.Increment(ref _next) - 1;

    /// <summary>Returns the total number of IDs assigned so far.</summary>
    public static int Count => _next;
}

/// <summary>
/// Sparse-set storage for a single component type. Provides O(1) add, remove, lookup,
/// and cache-friendly iteration over packed dense arrays.
/// </summary>
internal class ComponentPool<T> where T : struct
{
    readonly T[] _dense = new T[Entity.MaxEntities];
    readonly int[] _denseEntities = new int[Entity.MaxEntities];
    readonly int[] _sparse = new int[Entity.MaxEntities];
    int _count;

    /// <summary>The number of live components in the pool.</summary>
    public int Count => _count;

    public ComponentPool()
    {
        Array.Fill(_sparse, -1);
    }

    /// <summary>Returns whether the entity has a component in this pool.</summary>
    public bool Has(Entity e)
    {
        var value = _sparse[e.Index];
        return value >= 0 && value < _count;
    }

    /// <summary>Returns the component for the given entity, or default if absent.</summary>
    public T Get(Entity e)
    {
        if (!Has(e))
            return default;

        return _dense[_sparse[e.Index]];
    }

    /// <summary>Adds or updates the component for the given entity.</summary>
    public void Set(Entity e, T component)
    {
        var idx = _sparse[e.Index];
        if (idx >= 0)
        {
            _dense[idx] = component;
        }
        else
        {
            _dense[_count] = component;
            _denseEntities[_count] = e.Index;
            _sparse[e.Index] = _count;
            _count++;
        }
    }

    /// <summary>Removes the component for the given entity via swap-and-pop. No-op if absent.</summary>
    public void Remove(Entity e)
    {
        if (!Has(e))
            return;

        var idx = e.Index;
        var lastIdx = _count - 1;
        var swap = _denseEntities[lastIdx];
        _dense[_sparse[idx]] = _dense[lastIdx];
        _denseEntities[_sparse[idx]] = swap;
        _sparse[swap] = _sparse[idx];
        _sparse[idx] = -1;
        _count--;
    }
}
