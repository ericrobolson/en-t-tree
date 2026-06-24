# En-T-Tree

A C# Entity Component library inspired by [EnTT](https://github.com/skypjack/entt).

## Best Practices

### Deletion

Component removal and entity destruction are both immediate. There is no deferred deletion or garbage collection pass.

- `registry.Remove<T>(entity)` — swap-and-pops the component from the dense array, O(1).
- `registry.Destroy(entity)` — removes the entity from all pools, recycles the index into the free list, and bumps the generation counter. Any existing `Entity` handles pointing to that index are now stale.

**Do not destroy entities or remove components while iterating a view.** Swap-and-pop during iteration can cause skipped or duplicate visits. Collect first, destroy after:

```csharp
var toDestroy = new List<Entity>();
foreach (var (entity, hp) in registry.View<Health>())
{
    if (hp.Value <= 0)
        toDestroy.Add(entity);
}
foreach (var e in toDestroy)
    registry.Destroy(e);
```
