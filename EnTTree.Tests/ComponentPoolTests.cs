namespace EnTTree.Tests;

using EnTTree;

public readonly struct FakeComponent
{
    public readonly int Value;
    public FakeComponent(int value) { Value = value; }
}

public class ComponentPoolTests
{
    [Fact]
    public void Count_EmptyPool_IsZero()
    {
        var pool = new ComponentPool<FakeComponent>();
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Has_EmptyPool_ReturnsFalse()
    {
        var pool = new ComponentPool<FakeComponent>();
        Assert.False(pool.Has(new Entity(0, 0)));
    }

    [Fact]
    public void Set_AddsComponent()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(0, 0);
        pool.Set(e, new FakeComponent(42));

        Assert.True(pool.Has(e));
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Get_ReturnsSetComponent()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(5, 0);
        pool.Set(e, new FakeComponent(99));

        Assert.Equal(99, pool.Get(e).Value);
    }

    [Fact]
    public void Get_MissingEntity_ReturnsDefault()
    {
        var pool = new ComponentPool<FakeComponent>();
        var result = pool.Get(new Entity(0, 0));
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Set_UpdatesExistingComponent()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(3, 0);
        pool.Set(e, new FakeComponent(10));
        pool.Set(e, new FakeComponent(20));

        Assert.Equal(20, pool.Get(e).Value);
        Assert.Equal(1, pool.Count);
    }

    [Fact]
    public void Set_MultipleEntities()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        var c = new Entity(2, 0);
        pool.Set(a, new FakeComponent(1));
        pool.Set(b, new FakeComponent(2));
        pool.Set(c, new FakeComponent(3));

        Assert.Equal(3, pool.Count);
        Assert.Equal(1, pool.Get(a).Value);
        Assert.Equal(2, pool.Get(b).Value);
        Assert.Equal(3, pool.Get(c).Value);
    }

    [Fact]
    public void Remove_DecreasesCount()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(0, 0);
        pool.Set(e, new FakeComponent(1));
        pool.Remove(e);

        Assert.Equal(0, pool.Count);
        Assert.False(pool.Has(e));
    }

    [Fact]
    public void Remove_MissingEntity_IsNoOp()
    {
        var pool = new ComponentPool<FakeComponent>();
        pool.Remove(new Entity(5, 0));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Remove_EmptyPool_IsNoOp()
    {
        var pool = new ComponentPool<FakeComponent>();
        pool.Remove(new Entity(0, 0));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Remove_SwapAndPop_PreservesOtherEntities()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        var c = new Entity(2, 0);
        pool.Set(a, new FakeComponent(10));
        pool.Set(b, new FakeComponent(20));
        pool.Set(c, new FakeComponent(30));

        pool.Remove(a);

        Assert.Equal(2, pool.Count);
        Assert.False(pool.Has(a));
        Assert.Equal(20, pool.Get(b).Value);
        Assert.Equal(30, pool.Get(c).Value);
    }

    [Fact]
    public void Remove_LastEntity_NoSwapNeeded()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        pool.Set(a, new FakeComponent(10));
        pool.Set(b, new FakeComponent(20));

        pool.Remove(b);

        Assert.Equal(1, pool.Count);
        Assert.True(pool.Has(a));
        Assert.Equal(10, pool.Get(a).Value);
        Assert.False(pool.Has(b));
    }

    [Fact]
    public void Remove_MiddleEntity_SwapsCorrectly()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        var c = new Entity(2, 0);
        pool.Set(a, new FakeComponent(10));
        pool.Set(b, new FakeComponent(20));
        pool.Set(c, new FakeComponent(30));

        pool.Remove(b);

        Assert.Equal(2, pool.Count);
        Assert.Equal(10, pool.Get(a).Value);
        Assert.Equal(30, pool.Get(c).Value);
    }

    [Fact]
    public void Remove_ThenReaddSameEntity()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(5, 0);
        pool.Set(e, new FakeComponent(1));
        pool.Remove(e);
        pool.Set(e, new FakeComponent(2));

        Assert.Equal(1, pool.Count);
        Assert.Equal(2, pool.Get(e).Value);
    }

    [Fact]
    public void Remove_AllEntities_PoolIsEmpty()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        pool.Set(a, new FakeComponent(10));
        pool.Set(b, new FakeComponent(20));

        pool.Remove(a);
        pool.Remove(b);

        Assert.Equal(0, pool.Count);
        Assert.False(pool.Has(a));
        Assert.False(pool.Has(b));
    }

    [Fact]
    public void Has_AfterRemove_ReturnsFalse()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(0, 0);
        pool.Set(e, new FakeComponent(1));
        pool.Remove(e);

        Assert.False(pool.Has(e));
    }

    [Fact]
    public void Get_AfterRemove_ReturnsDefault()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(0, 0);
        pool.Set(e, new FakeComponent(42));
        pool.Remove(e);

        Assert.Equal(0, pool.Get(e).Value);
    }

    [Fact]
    public void Set_EntityAtIndexZero_WorksCorrectly()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e = new Entity(0, 0);
        pool.Set(e, new FakeComponent(77));

        Assert.True(pool.Has(e));
        Assert.Equal(77, pool.Get(e).Value);
    }

    [Fact]
    public void Has_DifferentGenerationSameIndex_StillMatchesByIndex()
    {
        var pool = new ComponentPool<FakeComponent>();
        var e1 = new Entity(5, 0);
        pool.Set(e1, new FakeComponent(10));

        var e2 = new Entity(5, 1);
        Assert.True(pool.Has(e2));
    }

    [Fact]
    public void Remove_TwiceSameEntity_SecondIsNoOp()
    {
        var pool = new ComponentPool<FakeComponent>();
        var a = new Entity(0, 0);
        var b = new Entity(1, 0);
        pool.Set(a, new FakeComponent(10));
        pool.Set(b, new FakeComponent(20));

        pool.Remove(a);
        pool.Remove(a);

        Assert.Equal(1, pool.Count);
        Assert.Equal(20, pool.Get(b).Value);
    }
}
