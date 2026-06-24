# EnTTree

A single-file, zero-dependency Entity Component System for C#. Inspired by [EnTT](https://github.com/skypjack/entt).

Copy `EnTTree.cs` into your project and go. Works with .NET, Unity, and Godot.

## Quick Start

```csharp
using EnTTree;

// Define components as readonly structs
public readonly struct Position
{
    public readonly float X, Y;
    public Position(float x, float y) { X = x; Y = y; }
}

public readonly struct Velocity
{
    public readonly float DX, DY;
    public Velocity(float dx, float dy) { DX = dx; DY = dy; }
}

// Create a registry and register component types upfront
var registry = new Registry();
registry.Register<Position>();
registry.Register<Velocity>();

// Create entities and attach components
Entity player = registry.Create();
registry.Set(player, new Position(0, 0));
registry.Set(player, new Velocity(1, 0));

// Query and update
foreach (var (entity, pos, vel) in registry.View<Position, Velocity>())
{
    registry.Set(entity, new Position(pos.X + vel.DX, pos.Y + vel.DY));
}

// Clean up
registry.Destroy(player);
```

## API

### Registry

| Method | Description |
|---|---|
| `Register<T>()` | Register a component type. Must be called before use. |
| `Create()` | Create a new entity. Reuses recycled indices. |
| `Destroy(entity)` | Destroy an entity and remove all its components. |
| `IsAlive(entity)` | Check if an entity handle is still valid. |
| `Set<T>(entity, component)` | Add or update a component. |
| `Get<T>(entity)` | Get a component. Returns `default` if absent. |
| `Has<T>(entity)` | Check if an entity has a component. |
| `Remove<T>(entity)` | Remove a component from an entity. |
| `View<A, B, ...>()` | Iterate all entities with the given component types (2-8 type params). |

### Entity

A `readonly struct` packing a 20-bit index and 12-bit generation counter into a single `uint`. Supports equality, comparison, and `ToString()`.

- Max ~1M concurrent entities
- Generation counter detects stale handles after destroy/reuse

### Views

Views iterate the smallest pool and check membership in the others. No cached entity lists, no allocations. Supports 2-8 component types.

```csharp
foreach (var (entity, pos, vel) in registry.View<Position, Velocity>())
{
    // pos and vel are copies — mutate via registry.Set()
}
```

## Components

Components must be `struct`. The `where T : struct` constraint is enforced at compile time. Using `readonly struct` is recommended to prevent accidental mutation of copies.

```csharp
public readonly struct Health
{
    public readonly int Value;
    public Health(int value) { Value = value; }
}
```

Mutation follows a get-then-set pattern:

```csharp
var hp = registry.Get<Health>(entity);
registry.Set(entity, new Health(hp.Value - 10));
```

## Best Practices

### Registration

Register all component types at startup. Using an unregistered type throws `InvalidOperationException`.

### Deletion

Component removal and entity destruction are immediate. There is no deferred deletion.

Do not destroy entities or remove components while iterating a view. Collect first, destroy after:

```csharp
var toDestroy = new List<Entity>();
foreach (var (entity, hp) in registry.View<Entity, Health>())
{
    if (hp.Value <= 0)
        toDestroy.Add(entity);
}
foreach (var e in toDestroy)
    registry.Destroy(e);
```

### Stale Handles

After `Destroy()`, any old `Entity` handle is stale. Calling `Get`, `Set`, `Has`, `Remove`, or `Destroy` with a stale handle throws `InvalidOperationException`. Use `IsAlive()` to check.

## Architecture

- **Sparse set storage**: O(1) add, remove, lookup. Dense arrays are cache-friendly for iteration.
- **Generational indices**: 20-bit index + 12-bit generation packed into `uint32`. Detects use-after-destroy.
- **Static generic type IDs**: `ComponentId<T>` assigns a unique `int` per type at first access. Pool lookup is an array index, not a dictionary hash.
- **Zero heap allocation on iteration**: Views and enumerators are structs. Component tuples are stack-allocated.

## License

MIT
