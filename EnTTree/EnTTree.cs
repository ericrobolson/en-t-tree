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
    internal const int MaxEntities = 1 << IndexBits;
    internal const int MaxGenerations = 1 << GenerationBits;

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
    public bool IsAlive(Entity e) => _generations[e.Index] == e.Generation;

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

    /// <summary>Creates a view over all entities with component types A and B.</summary>
    public View<A, B> View<A, B>() where A : struct where B : struct
        => new(GetPool<A>(), GetPool<B>(), _generations);

    /// <summary>Creates a view over all entities with component types A, B, and C.</summary>
    public View<A, B, C> View<A, B, C>() where A : struct where B : struct where C : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), _generations);

    /// <summary>Creates a view over all entities with component types A through D.</summary>
    public View<A, B, C, D> View<A, B, C, D>() where A : struct where B : struct where C : struct where D : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), GetPool<D>(), _generations);

    /// <summary>Creates a view over all entities with component types A through E.</summary>
    public View<A, B, C, D, E> View<A, B, C, D, E>() where A : struct where B : struct where C : struct where D : struct where E : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), GetPool<D>(), GetPool<E>(), _generations);

    /// <summary>Creates a view over all entities with component types A through F.</summary>
    public View<A, B, C, D, E, F> View<A, B, C, D, E, F>() where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), GetPool<D>(), GetPool<E>(), GetPool<F>(), _generations);

    /// <summary>Creates a view over all entities with component types A through G.</summary>
    public View<A, B, C, D, E, F, G> View<A, B, C, D, E, F, G>() where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct where G : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), GetPool<D>(), GetPool<E>(), GetPool<F>(), GetPool<G>(), _generations);

    /// <summary>Creates a view over all entities with component types A through H.</summary>
    public View<A, B, C, D, E, F, G, H> View<A, B, C, D, E, F, G, H>() where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct where G : struct where H : struct
        => new(GetPool<A>(), GetPool<B>(), GetPool<C>(), GetPool<D>(), GetPool<E>(), GetPool<F>(), GetPool<G>(), GetPool<H>(), _generations);

    private ComponentPool<T> GetPool<T>() where T : struct
    {
        int id = ComponentId<T>.Value;
        if (id >= _pools.Length || _pools[id] == null)
            throw new InvalidOperationException($"{typeof(T).Name} has not been registered");
        return (ComponentPool<T>)_pools[id];
    }

    private void AssertAlive(Entity e)
    {
        if (_generations[e.Index] != e.Generation)
            throw new InvalidOperationException($"{e} is stale (current generation: {_generations[e.Index]})");
    }
}

// ---------------------------------------------------------------------------
// Internal infrastructure
// ---------------------------------------------------------------------------

/// <summary>Assigns a unique integer ID to each component type on first access.</summary>
internal static class ComponentId<T> where T : struct
{
    /// <summary>The unique integer ID for component type T.</summary>
    public static readonly int Value = ComponentIdCounter.Next();
}

/// <summary>Thread-safe counter backing <see cref="ComponentId{T}"/>.</summary>
internal static class ComponentIdCounter
{
    static int _next;

    /// <summary>Returns the next available component ID.</summary>
    public static int Next() => Interlocked.Increment(ref _next) - 1;

    /// <summary>Returns the total number of IDs assigned so far.</summary>
    public static int Count => _next;
}

/// <summary>Non-generic interface for component pools, used by Registry.Destroy and View driver selection.</summary>
internal interface IComponentPool
{
    /// <summary>The number of live components in this pool.</summary>
    int Count { get; }

    /// <summary>Removes the component for the given entity. No-op if absent.</summary>
    void Remove(Entity e);

    /// <summary>Returns whether the entity has a component in this pool.</summary>
    bool Has(Entity e);

    /// <summary>Returns whether the given raw index has a component in this pool.</summary>
    bool HasByIndex(int index);

    /// <summary>Returns the entity index at the given dense array position.</summary>
    int GetEntityIndex(int denseIndex);
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

    /// <inheritdoc />
    public int Count => _count;

    public ComponentPool()
    {
        Array.Fill(_sparse, -1);
    }

    /// <inheritdoc />
    public int GetEntityIndex(int denseIndex) => _denseEntities[denseIndex];

    /// <summary>Returns the component at the given dense array position.</summary>
    public T GetByDenseIndex(int denseIndex) => _dense[denseIndex];

    /// <inheritdoc />
    public bool HasByIndex(int index)
    {
        var value = _sparse[index];
        return value >= 0 && value < _count;
    }

    /// <summary>Returns the component for the given raw index. Caller must ensure HasByIndex is true.</summary>
    public T GetByIndex(int index) => _dense[_sparse[index]];

    /// <inheritdoc />
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

    /// <inheritdoc />
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

// ---------------------------------------------------------------------------
// View helpers
// ---------------------------------------------------------------------------

/// <summary>Shared utility for view driver selection.</summary>
internal static class ViewHelper
{
    /// <summary>Returns the index of the pool with the fewest entities.</summary>
    public static int SmallestPool(ReadOnlySpan<IComponentPool> pools)
    {
        int min = 0;
        for (int i = 1; i < pools.Length; i++)
        {
            if (pools[i].Count < pools[min].Count)
                min = i;
        }
        return min;
    }
}

// ---------------------------------------------------------------------------
// Views (arities 2-8)
// ---------------------------------------------------------------------------

/// <summary>Iterates all entities that have component types A and B.</summary>
public readonly struct View<A, B> where A : struct where B : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly int[] _generations;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, int[] generations)
    {
        _poolA = poolA;
        _poolB = poolB;
        _generations = generations;
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _generations);

    /// <summary>Stack-allocated enumerator for View&lt;A, B&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly int[] _generations;
        readonly int _driverCount;
        readonly bool _aIsDriver;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, int[] generations)
        {
            _poolA = poolA;
            _poolB = poolB;
            _generations = generations;
            _aIsDriver = poolA.Count <= poolB.Count;
            _driverCount = _aIsDriver ? poolA.Count : poolB.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B) tuple.</summary>
        public (Entity Entity, A A, B B) Current { get; private set; }

        /// <summary>Advances to the next entity that has both components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                if (_aIsDriver)
                {
                    int ei = _poolA.GetEntityIndex(_denseIndex);
                    if (!_poolB.HasByIndex(ei)) continue;
                    Current = (new Entity(ei, _generations[ei]), _poolA.GetByDenseIndex(_denseIndex), _poolB.GetByIndex(ei));
                    return true;
                }
                else
                {
                    int ei = _poolB.GetEntityIndex(_denseIndex);
                    if (!_poolA.HasByIndex(ei)) continue;
                    Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByDenseIndex(_denseIndex));
                    return true;
                }
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A, B, and C.</summary>
public readonly struct View<A, B, C> where A : struct where B : struct where C : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, int[] generations)
    {
        _poolA = poolA;
        _poolB = poolB;
        _poolC = poolC;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, int[] generations, int driverIndex)
        {
            _poolA = poolA;
            _poolB = poolB;
            _poolC = poolC;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C) tuple.</summary>
        public (Entity Entity, A A, B B, C C) Current { get; private set; }

        /// <summary>Advances to the next entity that has all three components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A through D.</summary>
public readonly struct View<A, B, C, D> where A : struct where B : struct where C : struct where D : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly ComponentPool<D> _poolD;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, int[] generations)
    {
        _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC, poolD]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C, D) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _poolD, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C, D&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly ComponentPool<D> _poolD;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, int[] generations, int driverIndex)
        {
            _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC, poolD];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C, D) tuple.</summary>
        public (Entity Entity, A A, B B, C C, D D) Current { get; private set; }

        /// <summary>Advances to the next entity that has all four components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei) || !_poolD.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei), _poolD.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A through E.</summary>
public readonly struct View<A, B, C, D, E> where A : struct where B : struct where C : struct where D : struct where E : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly ComponentPool<D> _poolD;
    readonly ComponentPool<E> _poolE;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, int[] generations)
    {
        _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC, poolD, poolE]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C, D, E) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _poolD, _poolE, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C, D, E&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly ComponentPool<D> _poolD;
        readonly ComponentPool<E> _poolE;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, int[] generations, int driverIndex)
        {
            _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC, poolD, poolE];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C, D, E) tuple.</summary>
        public (Entity Entity, A A, B B, C C, D D, E E) Current { get; private set; }

        /// <summary>Advances to the next entity that has all five components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei) || !_poolD.HasByIndex(ei) || !_poolE.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei), _poolD.GetByIndex(ei), _poolE.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A through F.</summary>
public readonly struct View<A, B, C, D, E, F> where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly ComponentPool<D> _poolD;
    readonly ComponentPool<E> _poolE;
    readonly ComponentPool<F> _poolF;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, int[] generations)
    {
        _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC, poolD, poolE, poolF]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C, D, E, F) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _poolD, _poolE, _poolF, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C, D, E, F&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly ComponentPool<D> _poolD;
        readonly ComponentPool<E> _poolE;
        readonly ComponentPool<F> _poolF;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, int[] generations, int driverIndex)
        {
            _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC, poolD, poolE, poolF];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C, D, E, F) tuple.</summary>
        public (Entity Entity, A A, B B, C C, D D, E E, F F) Current { get; private set; }

        /// <summary>Advances to the next entity that has all six components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei) || !_poolD.HasByIndex(ei) || !_poolE.HasByIndex(ei) || !_poolF.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei), _poolD.GetByIndex(ei), _poolE.GetByIndex(ei), _poolF.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A through G.</summary>
public readonly struct View<A, B, C, D, E, F, G> where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct where G : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly ComponentPool<D> _poolD;
    readonly ComponentPool<E> _poolE;
    readonly ComponentPool<F> _poolF;
    readonly ComponentPool<G> _poolG;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, ComponentPool<G> poolG, int[] generations)
    {
        _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF; _poolG = poolG;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC, poolD, poolE, poolF, poolG]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C, D, E, F, G) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _poolD, _poolE, _poolF, _poolG, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C, D, E, F, G&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly ComponentPool<D> _poolD;
        readonly ComponentPool<E> _poolE;
        readonly ComponentPool<F> _poolF;
        readonly ComponentPool<G> _poolG;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, ComponentPool<G> poolG, int[] generations, int driverIndex)
        {
            _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF; _poolG = poolG;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC, poolD, poolE, poolF, poolG];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C, D, E, F, G) tuple.</summary>
        public (Entity Entity, A A, B B, C C, D D, E E, F F, G G) Current { get; private set; }

        /// <summary>Advances to the next entity that has all seven components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei) || !_poolD.HasByIndex(ei) || !_poolE.HasByIndex(ei) || !_poolF.HasByIndex(ei) || !_poolG.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei), _poolD.GetByIndex(ei), _poolE.GetByIndex(ei), _poolF.GetByIndex(ei), _poolG.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}

/// <summary>Iterates all entities that have component types A through H.</summary>
public readonly struct View<A, B, C, D, E, F, G, H> where A : struct where B : struct where C : struct where D : struct where E : struct where F : struct where G : struct where H : struct
{
    readonly ComponentPool<A> _poolA;
    readonly ComponentPool<B> _poolB;
    readonly ComponentPool<C> _poolC;
    readonly ComponentPool<D> _poolD;
    readonly ComponentPool<E> _poolE;
    readonly ComponentPool<F> _poolF;
    readonly ComponentPool<G> _poolG;
    readonly ComponentPool<H> _poolH;
    readonly int[] _generations;
    readonly int _driverIndex;

    internal View(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, ComponentPool<G> poolG, ComponentPool<H> poolH, int[] generations)
    {
        _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF; _poolG = poolG; _poolH = poolH;
        _generations = generations;
        _driverIndex = ViewHelper.SmallestPool([poolA, poolB, poolC, poolD, poolE, poolF, poolG, poolH]);
    }

    /// <summary>Returns an enumerator that yields (Entity, A, B, C, D, E, F, G, H) tuples.</summary>
    public Enumerator GetEnumerator() => new(_poolA, _poolB, _poolC, _poolD, _poolE, _poolF, _poolG, _poolH, _generations, _driverIndex);

    /// <summary>Stack-allocated enumerator for View&lt;A, B, C, D, E, F, G, H&gt;.</summary>
    public struct Enumerator
    {
        readonly ComponentPool<A> _poolA;
        readonly ComponentPool<B> _poolB;
        readonly ComponentPool<C> _poolC;
        readonly ComponentPool<D> _poolD;
        readonly ComponentPool<E> _poolE;
        readonly ComponentPool<F> _poolF;
        readonly ComponentPool<G> _poolG;
        readonly ComponentPool<H> _poolH;
        readonly int[] _generations;
        readonly IComponentPool _driver;
        readonly int _driverCount;
        int _denseIndex;

        internal Enumerator(ComponentPool<A> poolA, ComponentPool<B> poolB, ComponentPool<C> poolC, ComponentPool<D> poolD, ComponentPool<E> poolE, ComponentPool<F> poolF, ComponentPool<G> poolG, ComponentPool<H> poolH, int[] generations, int driverIndex)
        {
            _poolA = poolA; _poolB = poolB; _poolC = poolC; _poolD = poolD; _poolE = poolE; _poolF = poolF; _poolG = poolG; _poolH = poolH;
            _generations = generations;
            IComponentPool[] pools = [poolA, poolB, poolC, poolD, poolE, poolF, poolG, poolH];
            _driver = pools[driverIndex];
            _driverCount = _driver.Count;
            _denseIndex = -1;
        }

        /// <summary>The current (Entity, A, B, C, D, E, F, G, H) tuple.</summary>
        public (Entity Entity, A A, B B, C C, D D, E E, F F, G G, H H) Current { get; private set; }

        /// <summary>Advances to the next entity that has all eight components.</summary>
        public bool MoveNext()
        {
            while (++_denseIndex < _driverCount)
            {
                int ei = _driver.GetEntityIndex(_denseIndex);
                if (!_poolA.HasByIndex(ei) || !_poolB.HasByIndex(ei) || !_poolC.HasByIndex(ei) || !_poolD.HasByIndex(ei) || !_poolE.HasByIndex(ei) || !_poolF.HasByIndex(ei) || !_poolG.HasByIndex(ei) || !_poolH.HasByIndex(ei))
                    continue;
                Current = (new Entity(ei, _generations[ei]), _poolA.GetByIndex(ei), _poolB.GetByIndex(ei), _poolC.GetByIndex(ei), _poolD.GetByIndex(ei), _poolE.GetByIndex(ei), _poolF.GetByIndex(ei), _poolG.GetByIndex(ei), _poolH.GetByIndex(ei));
                return true;
            }
            return false;
        }
    }
}
