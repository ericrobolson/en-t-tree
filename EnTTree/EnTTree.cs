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
    internal const uint GenerationMask = (1u << GenerationBits) - 1;
    internal const int MaxEntities = (1 << IndexBits);
    internal const int MaxGenerations = (1 << GenerationBits);

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

/// <summary>
/// Manages entity lifecycle and component storage. Entities are created and destroyed
/// through the registry, and components are attached, queried, and removed by type.
/// </summary>
public class Registry
{
    int _nextIndex;
    readonly Stack<int> _freeList = new();
    readonly int[] _generations = new int[Entity.MaxEntities];
    IComponentPool[] _pools = [];

    /// <summary>Registers a component type for use. Must be called before any Get/Set/Has/Remove for that type.</summary>
    public void Register<T>() where T : struct
    {
        int id = ComponentId<T>.Value;
        if (id >= _pools.Length)
            Array.Resize(ref _pools, id + 1);
        _pools[id] = new ComponentPool<T>();
    }

    /// <summary>Creates a new entity, reusing a recycled index if available.</summary>
    public Entity Create()
    {
        if (_freeList.Count == 0 && _nextIndex >= Entity.MaxEntities)
            throw new InvalidOperationException("Entity limit reached");

        var idx = _nextIndex;
        if (_freeList.Count > 0)
        {
            idx = _freeList.Pop();
        }
        else
        {
            _nextIndex++;
        }

        return new Entity(idx, _generations[idx]);
    }

    /// <summary>Destroys an entity, removing it from all pools and recycling its index.</summary>
    public void Destroy(Entity e)
    {
        AssertAlive(e);
        foreach (var pool in _pools)
        {
            pool?.Remove(e);
        }

        var idx = e.Index;
        _generations[idx] = (int)((_generations[idx] + 1) & Entity.GenerationMask);
        _freeList.Push(idx);
    }

    /// <summary>Returns whether the entity handle is still valid.</summary>
    public bool IsAlive(Entity e)
    {
        return _generations[e.Index] == e.Generation;
    }

    /// <summary>Returns the component of type T for the given entity.</summary>
    public T Get<T>(Entity e) where T : struct
    {
        AssertAlive(e);
        return GetPool<T>().Get(e);
    }

    /// <summary>Adds or updates the component of type T on the given entity.</summary>
    public void Set<T>(Entity e, T component) where T : struct
    {
        AssertAlive(e);
        GetPool<T>().Set(e, component);
    }

    /// <summary>Returns whether the entity has a component of type T.</summary>
    public bool Has<T>(Entity e) where T : struct
    {
        AssertAlive(e);
        return GetPool<T>().Has(e);
    }

    /// <summary>Removes the component of type T from the given entity.</summary>
    public void Remove<T>(Entity e) where T : struct
    {
        AssertAlive(e);
        GetPool<T>().Remove(e);
    }

    /// <summary>Returns the typed pool for T, throwing if unregistered.</summary>
    private ComponentPool<T> GetPool<T>() where T : struct
    {
        int id = ComponentId<T>.Value;
        if (id >= _pools.Length || _pools[id] == null)
            throw new InvalidOperationException($"{typeof(T).Name} has not been registered");
        return (ComponentPool<T>)_pools[id];
    }

    /// <summary>Throws if the entity handle is stale.</summary>
    private void AssertAlive(Entity e)
    {
        if (_generations[e.Index] != e.Generation)
            throw new InvalidOperationException($"{e} is stale (current generation: {_generations[e.Index]})");
    }
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

internal interface IComponentPool
{
    void Remove(Entity e);
    bool Has(Entity e);
}

/// <summary>
/// Sparse-set storage for a single component type. Provides O(1) add, remove, lookup,
/// and cache-friendly iteration over packed dense arrays.
/// </summary>
internal class ComponentPool<T> : IComponentPool where T : struct
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
