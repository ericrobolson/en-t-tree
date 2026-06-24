namespace EnTTree.Tests;

using EnTTree;

public readonly struct Position
{
    public readonly float X;
    public readonly float Y;
    public Position(float x, float y) { X = x; Y = y; }
}

public readonly struct Velocity
{
    public readonly float DX;
    public readonly float DY;
    public Velocity(float dx, float dy) { DX = dx; DY = dy; }
}

public readonly struct Health
{
    public readonly int Value;
    public Health(int value) { Value = value; }
}

public class RegistryTests
{
    // --- Create ---

    [Fact]
    public void Create_ReturnsEntityWithIndexZero()
    {
        var reg = new Registry();
        var e = reg.Create();
        Assert.Equal(0, e.Index);
        Assert.Equal(0, e.Generation);
    }

    [Fact]
    public void Create_SequentialEntities_IncrementIndex()
    {
        var reg = new Registry();
        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        Assert.Equal(0, a.Index);
        Assert.Equal(1, b.Index);
        Assert.Equal(2, c.Index);
    }

    [Fact]
    public void Create_AllGenerationZero_WhenNoRecycling()
    {
        var reg = new Registry();
        var a = reg.Create();
        var b = reg.Create();

        Assert.Equal(0, a.Generation);
        Assert.Equal(0, b.Generation);
    }

    [Fact]
    public void Create_ReusesDestroyedIndex()
    {
        var reg = new Registry();
        var a = reg.Create();
        reg.Destroy(a);

        var b = reg.Create();
        Assert.Equal(a.Index, b.Index);
    }

    [Fact]
    public void Create_RecycledEntity_HasBumpedGeneration()
    {
        var reg = new Registry();
        var a = reg.Create();
        Assert.Equal(0, a.Generation);

        reg.Destroy(a);
        var b = reg.Create();
        Assert.Equal(a.Index, b.Index);
        Assert.Equal(1, b.Generation);
    }

    [Fact]
    public void Create_FreeListUsedBeforeNextIndex()
    {
        var reg = new Registry();
        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        reg.Destroy(b);
        var d = reg.Create();

        Assert.Equal(b.Index, d.Index);
        Assert.Equal(1, d.Generation);
    }

    [Fact]
    public void Create_MultipleRecycles_GenerationKeepsIncrementing()
    {
        var reg = new Registry();
        var e = reg.Create();

        for (int i = 0; i < 5; i++)
        {
            reg.Destroy(e);
            e = reg.Create();
        }

        Assert.Equal(0, e.Index);
        Assert.Equal(5, e.Generation);
    }

    // --- Destroy ---

    [Fact]
    public void Destroy_MakesEntityNotAlive()
    {
        var reg = new Registry();
        var e = reg.Create();
        reg.Destroy(e);

        Assert.False(reg.IsAlive(e));
    }

    [Fact]
    public void Destroy_StaleEntity_Throws()
    {
        var reg = new Registry();
        var e = reg.Create();
        reg.Destroy(e);

        Assert.Throws<InvalidOperationException>(() => reg.Destroy(e));
    }

    [Fact]
    public void Destroy_RemovesAllComponents()
    {
        var reg = new Registry();
        reg.Register<Position>();
        reg.Register<Velocity>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Set(e, new Velocity(3, 4));

        reg.Destroy(e);

        var recycled = reg.Create();
        Assert.Equal(e.Index, recycled.Index);
        Assert.False(reg.Has<Position>(recycled));
        Assert.False(reg.Has<Velocity>(recycled));
    }

    [Fact]
    public void Destroy_DoesNotAffectOtherEntities()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Position(1, 2));
        reg.Set(b, new Position(3, 4));

        reg.Destroy(a);

        Assert.True(reg.IsAlive(b));
        Assert.Equal(3, reg.Get<Position>(b).X);
    }

    // --- IsAlive ---

    [Fact]
    public void IsAlive_NewEntity_ReturnsTrue()
    {
        var reg = new Registry();
        var e = reg.Create();
        Assert.True(reg.IsAlive(e));
    }

    [Fact]
    public void IsAlive_DestroyedEntity_ReturnsFalse()
    {
        var reg = new Registry();
        var e = reg.Create();
        reg.Destroy(e);
        Assert.False(reg.IsAlive(e));
    }

    [Fact]
    public void IsAlive_RecycledEntity_OldHandleIsDead()
    {
        var reg = new Registry();
        var old = reg.Create();
        reg.Destroy(old);
        var recycled = reg.Create();

        Assert.False(reg.IsAlive(old));
        Assert.True(reg.IsAlive(recycled));
    }

    // --- Register ---

    [Fact]
    public void Register_AllowsSetAndGet()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(5, 10));
        Assert.Equal(5, reg.Get<Position>(e).X);
    }

    [Fact]
    public void Register_CanRegisterMultipleTypes()
    {
        var reg = new Registry();
        reg.Register<Position>();
        reg.Register<Velocity>();
        reg.Register<Health>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Set(e, new Velocity(3, 4));
        reg.Set(e, new Health(100));

        Assert.Equal(1, reg.Get<Position>(e).X);
        Assert.Equal(3, reg.Get<Velocity>(e).DX);
        Assert.Equal(100, reg.Get<Health>(e).Value);
    }

    // --- Get ---

    [Fact]
    public void Get_UnregisteredType_Throws()
    {
        var reg = new Registry();
        var e = reg.Create();

        Assert.Throws<InvalidOperationException>(() => reg.Get<Health>(e));
    }

    [Fact]
    public void Get_StaleEntity_Throws()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Destroy(e);

        Assert.Throws<InvalidOperationException>(() => reg.Get<Position>(e));
    }

    [Fact]
    public void Get_EntityWithoutComponent_ReturnsDefault()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        var pos = reg.Get<Position>(e);
        Assert.Equal(0, pos.X);
        Assert.Equal(0, pos.Y);
    }

    // --- Set ---

    [Fact]
    public void Set_UnregisteredType_Throws()
    {
        var reg = new Registry();
        var e = reg.Create();

        Assert.Throws<InvalidOperationException>(() => reg.Set(e, new Health(50)));
    }

    [Fact]
    public void Set_StaleEntity_Throws()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Destroy(e);

        Assert.Throws<InvalidOperationException>(() => reg.Set(e, new Position(1, 2)));
    }

    [Fact]
    public void Set_UpdatesExistingComponent()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Set(e, new Position(10, 20));

        Assert.Equal(10, reg.Get<Position>(e).X);
        Assert.Equal(20, reg.Get<Position>(e).Y);
    }

    [Fact]
    public void Set_MultipleEntitiesSameType()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Position(1, 2));
        reg.Set(b, new Position(3, 4));

        Assert.Equal(1, reg.Get<Position>(a).X);
        Assert.Equal(3, reg.Get<Position>(b).X);
    }

    // --- Has ---

    [Fact]
    public void Has_UnregisteredType_Throws()
    {
        var reg = new Registry();
        var e = reg.Create();

        Assert.Throws<InvalidOperationException>(() => reg.Has<Health>(e));
    }

    [Fact]
    public void Has_StaleEntity_Throws()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Destroy(e);

        Assert.Throws<InvalidOperationException>(() => reg.Has<Position>(e));
    }

    [Fact]
    public void Has_WithComponent_ReturnsTrue()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));

        Assert.True(reg.Has<Position>(e));
    }

    [Fact]
    public void Has_WithoutComponent_ReturnsFalse()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        Assert.False(reg.Has<Position>(e));
    }

    [Fact]
    public void Has_AfterRemove_ReturnsFalse()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Remove<Position>(e);

        Assert.False(reg.Has<Position>(e));
    }

    // --- Remove ---

    [Fact]
    public void Remove_UnregisteredType_Throws()
    {
        var reg = new Registry();
        var e = reg.Create();

        Assert.Throws<InvalidOperationException>(() => reg.Remove<Health>(e));
    }

    [Fact]
    public void Remove_StaleEntity_Throws()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Destroy(e);

        Assert.Throws<InvalidOperationException>(() => reg.Remove<Position>(e));
    }

    [Fact]
    public void Remove_ExistingComponent()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Remove<Position>(e);

        Assert.False(reg.Has<Position>(e));
    }

    [Fact]
    public void Remove_DoesNotAffectOtherComponents()
    {
        var reg = new Registry();
        reg.Register<Position>();
        reg.Register<Velocity>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));
        reg.Set(e, new Velocity(3, 4));
        reg.Remove<Position>(e);

        Assert.False(reg.Has<Position>(e));
        Assert.True(reg.Has<Velocity>(e));
        Assert.Equal(3, reg.Get<Velocity>(e).DX);
    }

    [Fact]
    public void Remove_DoesNotAffectOtherEntities()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Position(1, 2));
        reg.Set(b, new Position(3, 4));

        reg.Remove<Position>(a);

        Assert.False(reg.Has<Position>(a));
        Assert.True(reg.Has<Position>(b));
        Assert.Equal(3, reg.Get<Position>(b).X);
    }

    // --- Recycling / generation edge cases ---

    [Fact]
    public void Recycled_EntityCanHaveNewComponents()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var a = reg.Create();
        reg.Set(a, new Position(1, 2));
        reg.Destroy(a);

        var b = reg.Create();
        Assert.Equal(a.Index, b.Index);
        Assert.False(reg.Has<Position>(b));

        reg.Set(b, new Position(10, 20));
        Assert.Equal(10, reg.Get<Position>(b).X);
    }

    [Fact]
    public void Recycled_OldHandleCannotAccessNewComponents()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var old = reg.Create();
        reg.Destroy(old);

        var recycled = reg.Create();
        reg.Set(recycled, new Position(99, 99));

        Assert.Throws<InvalidOperationException>(() => reg.Get<Position>(old));
    }

    [Fact]
    public void Destroy_ThenCreateMultiple_FreeListDrainsCorrectly()
    {
        var reg = new Registry();
        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        reg.Destroy(c);
        reg.Destroy(b);

        var d = reg.Create();
        var e = reg.Create();

        Assert.Equal(b.Index, d.Index);
        Assert.Equal(c.Index, e.Index);
    }

    [Fact]
    public void Create_AfterDestroyAndFreshAlloc_MixesCorrectly()
    {
        var reg = new Registry();
        var a = reg.Create();
        var b = reg.Create();

        reg.Destroy(a);

        var c = reg.Create();
        var d = reg.Create();

        Assert.Equal(a.Index, c.Index);
        Assert.Equal(2, d.Index);
    }

    // --- Cross-type interactions ---

    [Fact]
    public void MultipleTypes_IndependentPerEntity()
    {
        var reg = new Registry();
        reg.Register<Position>();
        reg.Register<Health>();

        var e = reg.Create();
        reg.Set(e, new Position(5, 10));

        Assert.True(reg.Has<Position>(e));
        Assert.False(reg.Has<Health>(e));
    }

    [Fact]
    public void Destroy_WithNoRegisteredPools_DoesNotThrow()
    {
        var reg = new Registry();
        var e = reg.Create();
        reg.Destroy(e);

        Assert.False(reg.IsAlive(e));
    }

    [Fact]
    public void Destroy_WithSomePoolsRegistered_CleansOnlyRelevant()
    {
        var reg = new Registry();
        reg.Register<Position>();
        reg.Register<Velocity>();

        var e = reg.Create();
        reg.Set(e, new Position(1, 2));

        reg.Destroy(e);
        var recycled = reg.Create();

        Assert.False(reg.Has<Position>(recycled));
        Assert.False(reg.Has<Velocity>(recycled));
    }

    // --- Stress / boundary ---

    [Fact]
    public void RapidCreateDestroy_ManyIterations()
    {
        var reg = new Registry();
        reg.Register<Position>();

        Entity last = default;
        for (int i = 0; i < 1000; i++)
        {
            var e = reg.Create();
            reg.Set(e, new Position(i, i));
            reg.Destroy(e);
            last = e;
        }

        Assert.False(reg.IsAlive(last));

        var final = reg.Create();
        Assert.Equal(0, final.Index);
        Assert.True(reg.IsAlive(final));
    }

    [Fact]
    public void ManyEntities_AllAlive()
    {
        var reg = new Registry();
        var entities = new Entity[500];

        for (int i = 0; i < 500; i++)
            entities[i] = reg.Create();

        for (int i = 0; i < 500; i++)
            Assert.True(reg.IsAlive(entities[i]));
    }

    [Fact]
    public void ManyEntities_DestroyOdds_EvensStillAlive()
    {
        var reg = new Registry();
        reg.Register<Position>();

        var entities = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            entities[i] = reg.Create();
            reg.Set(entities[i], new Position(i, i));
        }

        for (int i = 1; i < 100; i += 2)
            reg.Destroy(entities[i]);

        for (int i = 0; i < 100; i++)
        {
            if (i % 2 == 0)
            {
                Assert.True(reg.IsAlive(entities[i]));
                Assert.Equal(i, reg.Get<Position>(entities[i]).X);
            }
            else
            {
                Assert.False(reg.IsAlive(entities[i]));
            }
        }
    }
}
