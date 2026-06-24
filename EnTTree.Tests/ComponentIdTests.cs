namespace EnTTree.Tests;

using EnTTree;

public readonly struct CompA { }
public readonly struct CompB { }
public readonly struct CompC { }

public class ComponentIdTests
{
    [Fact]
    public void Value_IsNonNegative()
    {
        Assert.True(ComponentId<CompA>.Value >= 0);
    }

    [Fact]
    public void Value_IsDeterministic()
    {
        var first = ComponentId<CompA>.Value;
        var second = ComponentId<CompA>.Value;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Value_DifferentTypes_GetDifferentIds()
    {
        var a = ComponentId<CompA>.Value;
        var b = ComponentId<CompB>.Value;
        var c = ComponentId<CompC>.Value;
        Assert.NotEqual(a, b);
        Assert.NotEqual(b, c);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Counter_Count_ReflectsAssignedIds()
    {
        _ = ComponentId<CompA>.Value;
        _ = ComponentId<CompB>.Value;
        Assert.True(ComponentIdCounter.Count >= 2);
    }
}
