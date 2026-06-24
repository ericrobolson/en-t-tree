namespace EnTTree.Tests;

using EnTTree;

public readonly struct Pos
{
    public readonly float X;
    public readonly float Y;
    public Pos(float x, float y) { X = x; Y = y; }
}

public readonly struct Vel
{
    public readonly float DX;
    public readonly float DY;
    public Vel(float dx, float dy) { DX = dx; DY = dy; }
}

public class ViewTests
{
    // --- Basic iteration ---

    [Fact]
    public void View_EmptyPools_YieldsNothing()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel>())
            count++;

        Assert.Equal(0, count);
    }

    [Fact]
    public void View_EntityWithBothComponents_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(e, results[0].Entity);
        Assert.Equal(1, results[0].A.X);
        Assert.Equal(3, results[0].B.DX);
    }

    [Fact]
    public void View_EntityWithOnlyA_Skipped()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));

        var results = Collect(reg);
        Assert.Empty(results);
    }

    [Fact]
    public void View_EntityWithOnlyB_Skipped()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Vel(3, 4));

        var results = Collect(reg);
        Assert.Empty(results);
    }

    [Fact]
    public void View_EntityWithNeither_Skipped()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();

        var results = Collect(reg);
        Assert.Empty(results);
    }

    // --- Multiple entities ---

    [Fact]
    public void View_MultipleMatching_YieldsAll()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(b, new Vel(20, 0));
        reg.Set(c, new Pos(3, 0));
        reg.Set(c, new Vel(30, 0));

        var results = Collect(reg);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void View_MixOfMatchingAndNon_YieldsOnlyMatching()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(c, new Vel(30, 0));

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(a, results[0].Entity);
    }

    // --- Correct component values ---

    [Fact]
    public void View_ReturnsCorrectComponentValues()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(10, 20));
        reg.Set(a, new Vel(1, 2));
        reg.Set(b, new Pos(30, 40));
        reg.Set(b, new Vel(3, 4));

        var results = Collect(reg);
        var ra = results.Find(r => r.Entity == a);
        var rb = results.Find(r => r.Entity == b);

        Assert.Equal(10, ra.A.X);
        Assert.Equal(20, ra.A.Y);
        Assert.Equal(1, ra.B.DX);
        Assert.Equal(2, ra.B.DY);
        Assert.Equal(30, rb.A.X);
        Assert.Equal(3, rb.B.DX);
    }

    // --- Destructuring ---

    [Fact]
    public void View_Destructuring_Works()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(5, 10));
        reg.Set(e, new Vel(1, 2));

        foreach (var (entity, pos, vel) in reg.View<Pos, Vel>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(5, pos.X);
            Assert.Equal(10, pos.Y);
            Assert.Equal(1, vel.DX);
            Assert.Equal(2, vel.DY);
        }
    }

    // --- Entity handle validity ---

    [Fact]
    public void View_YieldedEntityIsAlive()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));

        foreach (var (entity, _, _) in reg.View<Pos, Vel>())
        {
            Assert.True(reg.IsAlive(entity));
        }
    }

    [Fact]
    public void View_YieldedEntity_HasCorrectGeneration()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var old = reg.Create();
        reg.Destroy(old);

        var recycled = reg.Create();
        reg.Set(recycled, new Pos(1, 2));
        reg.Set(recycled, new Vel(3, 4));

        foreach (var (entity, _, _) in reg.View<Pos, Vel>())
        {
            Assert.Equal(recycled, entity);
            Assert.Equal(1, entity.Generation);
        }
    }

    [Fact]
    public void View_YieldedEntity_CanBeUsedWithRegistrySet()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(0, 0));
        reg.Set(e, new Vel(5, 10));

        foreach (var (entity, pos, vel) in reg.View<Pos, Vel>())
        {
            reg.Set(entity, new Pos(pos.X + vel.DX, pos.Y + vel.DY));
        }

        Assert.Equal(5, reg.Get<Pos>(e).X);
        Assert.Equal(10, reg.Get<Pos>(e).Y);
    }

    // --- Smallest pool drives iteration ---

    [Fact]
    public void View_SmallerPoolADrives_StillYieldsCorrectly()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        reg.Set(a, new Pos(1, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(c, new Pos(3, 0));

        reg.Set(a, new Vel(10, 0));

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(a, results[0].Entity);
    }

    [Fact]
    public void View_SmallerPoolBDrives_StillYieldsCorrectly()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Vel(20, 0));
        reg.Set(c, new Vel(30, 0));

        reg.Set(a, new Pos(1, 0));

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(a, results[0].Entity);
    }

    // --- After mutation ---

    [Fact]
    public void View_AfterComponentRemoved_ExcludesEntity()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(b, new Vel(20, 0));

        reg.Remove<Vel>(a);

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(b, results[0].Entity);
    }

    [Fact]
    public void View_AfterEntityDestroyed_ExcludesEntity()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(b, new Vel(20, 0));

        reg.Destroy(a);

        var results = Collect(reg);
        Assert.Single(results);
        Assert.Equal(b, results[0].Entity);
    }

    // --- Edge cases ---

    [Fact]
    public void View_SingleEntity_BothComponents()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(7, 8));
        reg.Set(e, new Vel(9, 10));

        var results = Collect(reg);
        Assert.Single(results);
    }

    [Fact]
    public void View_EqualSizedPools_Works()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(b, new Pos(2, 0));
        reg.Set(a, new Vel(10, 0));
        reg.Set(b, new Vel(20, 0));

        var results = Collect(reg);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void View_NoOverlap_YieldsNothing()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(b, new Vel(20, 0));

        var results = Collect(reg);
        Assert.Empty(results);
    }

    [Fact]
    public void View_ManyEntities_OnlyOverlapMatches()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var entities = new Entity[100];
        for (int i = 0; i < 100; i++)
        {
            entities[i] = reg.Create();
            reg.Set(entities[i], new Pos(i, 0));

            if (i % 10 == 0)
                reg.Set(entities[i], new Vel(i * 10, 0));
        }

        var results = Collect(reg);
        Assert.Equal(10, results.Count);

        foreach (var r in results)
            Assert.Equal(0, r.Entity.Index % 10);
    }

    [Fact]
    public void View_CanIterateMultipleTimes()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));

        var view = reg.View<Pos, Vel>();

        int count1 = 0;
        foreach (var _ in view) count1++;

        int count2 = 0;
        foreach (var _ in view) count2++;

        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    // --- Helper ---

    static List<(Entity Entity, Pos A, Vel B)> Collect(Registry reg)
    {
        var list = new List<(Entity, Pos, Vel)>();
        foreach (var item in reg.View<Pos, Vel>())
            list.Add(item);
        return list;
    }
}

public readonly struct C3 { public readonly int V; public C3(int v) { V = v; } }
public readonly struct C4 { public readonly int V; public C4(int v) { V = v; } }
public readonly struct C5 { public readonly int V; public C5(int v) { V = v; } }
public readonly struct C6 { public readonly int V; public C6(int v) { V = v; } }
public readonly struct C7 { public readonly int V; public C7(int v) { V = v; } }
public readonly struct C8 { public readonly int V; public C8(int v) { V = v; } }

public class View3Tests
{
    [Fact]
    public void View3_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));

        int count = 0;
        foreach (var (entity, pos, vel, c3) in reg.View<Pos, Vel, C3>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View3_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3>()) count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void View3_MultipleEntities_OnlyFullMatchYields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();

        var a = reg.Create();
        var b = reg.Create();
        reg.Set(a, new Pos(1, 0));
        reg.Set(a, new Vel(1, 0));
        reg.Set(a, new C3(1));
        reg.Set(b, new Pos(2, 0));
        reg.Set(b, new Vel(2, 0));

        int count = 0;
        foreach (var (entity, _, _, _) in reg.View<Pos, Vel, C3>())
        {
            Assert.Equal(a, entity);
            count++;
        }
        Assert.Equal(1, count);
    }
}

public class View4Tests
{
    [Fact]
    public void View4_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));

        int count = 0;
        foreach (var (entity, pos, vel, c3, c4) in reg.View<Pos, Vel, C3, C4>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            Assert.Equal(6, c4.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View4_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3, C4>()) count++;
        Assert.Equal(0, count);
    }
}

public class View5Tests
{
    [Fact]
    public void View5_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));

        int count = 0;
        foreach (var (entity, pos, vel, c3, c4, c5) in reg.View<Pos, Vel, C3, C4, C5>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            Assert.Equal(6, c4.V);
            Assert.Equal(7, c5.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View5_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3, C4, C5>()) count++;
        Assert.Equal(0, count);
    }
}

public class View6Tests
{
    [Fact]
    public void View6_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));
        reg.Set(e, new C6(8));

        int count = 0;
        foreach (var (entity, pos, vel, c3, c4, c5, c6) in reg.View<Pos, Vel, C3, C4, C5, C6>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            Assert.Equal(6, c4.V);
            Assert.Equal(7, c5.V);
            Assert.Equal(8, c6.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View6_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3, C4, C5, C6>()) count++;
        Assert.Equal(0, count);
    }
}

public class View7Tests
{
    [Fact]
    public void View7_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();
        reg.Register<C7>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));
        reg.Set(e, new C6(8));
        reg.Set(e, new C7(9));

        int count = 0;
        foreach (var (entity, pos, vel, c3, c4, c5, c6, c7) in reg.View<Pos, Vel, C3, C4, C5, C6, C7>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            Assert.Equal(6, c4.V);
            Assert.Equal(7, c5.V);
            Assert.Equal(8, c6.V);
            Assert.Equal(9, c7.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View7_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();
        reg.Register<C7>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));
        reg.Set(e, new C6(8));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3, C4, C5, C6, C7>()) count++;
        Assert.Equal(0, count);
    }
}

public class View8Tests
{
    [Fact]
    public void View8_MatchingEntity_Yields()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();
        reg.Register<C7>();
        reg.Register<C8>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));
        reg.Set(e, new C6(8));
        reg.Set(e, new C7(9));
        reg.Set(e, new C8(10));

        int count = 0;
        foreach (var (entity, pos, vel, c3, c4, c5, c6, c7, c8) in reg.View<Pos, Vel, C3, C4, C5, C6, C7, C8>())
        {
            Assert.Equal(e, entity);
            Assert.Equal(1, pos.X);
            Assert.Equal(3, vel.DX);
            Assert.Equal(5, c3.V);
            Assert.Equal(6, c4.V);
            Assert.Equal(7, c5.V);
            Assert.Equal(8, c6.V);
            Assert.Equal(9, c7.V);
            Assert.Equal(10, c8.V);
            count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void View8_MissingOneComponent_Skips()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();
        reg.Register<C7>();
        reg.Register<C8>();

        var e = reg.Create();
        reg.Set(e, new Pos(1, 2));
        reg.Set(e, new Vel(3, 4));
        reg.Set(e, new C3(5));
        reg.Set(e, new C4(6));
        reg.Set(e, new C5(7));
        reg.Set(e, new C6(8));
        reg.Set(e, new C7(9));

        int count = 0;
        foreach (var _ in reg.View<Pos, Vel, C3, C4, C5, C6, C7, C8>()) count++;
        Assert.Equal(0, count);
    }

    [Fact]
    public void View8_SmallestPoolDrives()
    {
        var reg = new Registry();
        reg.Register<Pos>();
        reg.Register<Vel>();
        reg.Register<C3>();
        reg.Register<C4>();
        reg.Register<C5>();
        reg.Register<C6>();
        reg.Register<C7>();
        reg.Register<C8>();

        var a = reg.Create();
        var b = reg.Create();
        var c = reg.Create();

        // All three get Pos through C7
        foreach (var e in new[] { a, b, c })
        {
            reg.Set(e, new Pos(e.Index, 0));
            reg.Set(e, new Vel(e.Index, 0));
            reg.Set(e, new C3(e.Index));
            reg.Set(e, new C4(e.Index));
            reg.Set(e, new C5(e.Index));
            reg.Set(e, new C6(e.Index));
            reg.Set(e, new C7(e.Index));
        }

        // Only entity 'a' gets C8
        reg.Set(a, new C8(99));

        int count = 0;
        foreach (var (entity, _, _, _, _, _, _, _, c8) in reg.View<Pos, Vel, C3, C4, C5, C6, C7, C8>())
        {
            Assert.Equal(a, entity);
            Assert.Equal(99, c8.V);
            count++;
        }
        Assert.Equal(1, count);
    }
}
