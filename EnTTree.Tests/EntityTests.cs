namespace EnTTree.Tests;

using EnTTree;

public class EntityTests
{
    [Fact]
    public void Constructor_StoresIndexAndGeneration()
    {
        var e = new Entity(42, 7);
        Assert.Equal(42, e.Index);
        Assert.Equal(7, e.Generation);
    }

    [Fact]
    public void Constructor_ZeroValues()
    {
        var e = new Entity(0, 0);
        Assert.Equal(0, e.Index);
        Assert.Equal(0, e.Generation);
        Assert.Equal(0u, e.Id);
    }

    [Fact]
    public void Constructor_MaxIndex()
    {
        int maxIndex = (1 << 20) - 1;
        var e = new Entity(maxIndex, 0);
        Assert.Equal(maxIndex, e.Index);
        Assert.Equal(0, e.Generation);
    }

    [Fact]
    public void Constructor_MaxGeneration()
    {
        int maxGen = (1 << 12) - 1;
        var e = new Entity(0, maxGen);
        Assert.Equal(0, e.Index);
        Assert.Equal(maxGen, e.Generation);
    }

    [Fact]
    public void Constructor_MaxBoth()
    {
        int maxIndex = (1 << 20) - 1;
        int maxGen = (1 << 12) - 1;
        var e = new Entity(maxIndex, maxGen);
        Assert.Equal(maxIndex, e.Index);
        Assert.Equal(maxGen, e.Generation);
    }

    [Fact]
    public void IsMaxIndex_TrueAtMax()
    {
        int maxIndex = (1 << 20) - 1;
        var e = new Entity(maxIndex, 0);
        Assert.True(e.IsMaxIndex);
    }

    [Fact]
    public void IsMaxIndex_FalseBelowMax()
    {
        var e = new Entity(0, 0);
        Assert.False(e.IsMaxIndex);
    }

    [Fact]
    public void IsMaxGeneration_TrueAtMax()
    {
        int maxGen = (1 << 12) - 1;
        var e = new Entity(0, maxGen);
        Assert.True(e.IsMaxGeneration);
    }

    [Fact]
    public void IsMaxGeneration_FalseBelowMax()
    {
        var e = new Entity(0, 0);
        Assert.False(e.IsMaxGeneration);
    }

    [Fact]
    public void Next_FromDefault_ReturnsSameEntity()
    {
        var e = new Entity(0, 0);
        var next = e.Next();
        Assert.Equal(1, next.Index);
        Assert.Equal(0, next.Generation);
    }

    [Fact]
    public void Next_AtMaxIndex_WrapsIndexAndIncrementsGeneration()
    {
        int maxIndex = (1 << 20) - 1;
        var e = new Entity(maxIndex, 5);
        var next = e.Next();
        Assert.Equal(0, next.Index);
        Assert.Equal(6, next.Generation);
    }

    [Fact]
    public void Next_AtMaxIndexAndMaxGeneration_WrapsGenerationToZero()
    {
        int maxIndex = (1 << 20) - 1;
        int maxGen = (1 << 12) - 1;
        var e = new Entity(maxIndex, maxGen);
        var next = e.Next();
        Assert.Equal(0, next.Index);
        Assert.Equal(0, next.Generation);
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new Entity(10, 3);
        var b = new Entity(10, 3);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentIndex_ReturnsFalse()
    {
        var a = new Entity(10, 3);
        var b = new Entity(11, 3);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_DifferentGeneration_ReturnsFalse()
    {
        var a = new Entity(10, 3);
        var b = new Entity(10, 4);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_BoxedObject_ReturnsTrue()
    {
        var a = new Entity(10, 3);
        object b = new Entity(10, 3);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void Equals_NonEntityObject_ReturnsFalse()
    {
        var a = new Entity(10, 3);
        Assert.False(a.Equals("not an entity"));
    }

    [Fact]
    public void GetHashCode_SameEntities_SameHash()
    {
        var a = new Entity(10, 3);
        var b = new Entity(10, 3);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void OperatorEqual_SameValues_ReturnsTrue()
    {
        var a = new Entity(10, 3);
        var b = new Entity(10, 3);
        Assert.True(a == b);
    }

    [Fact]
    public void OperatorEqual_DifferentValues_ReturnsFalse()
    {
        var a = new Entity(10, 3);
        var b = new Entity(11, 3);
        Assert.False(a == b);
    }

    [Fact]
    public void OperatorNotEqual_DifferentValues_ReturnsTrue()
    {
        var a = new Entity(10, 3);
        var b = new Entity(11, 3);
        Assert.True(a != b);
    }

    [Fact]
    public void OperatorNotEqual_SameValues_ReturnsFalse()
    {
        var a = new Entity(10, 3);
        var b = new Entity(10, 3);
        Assert.False(a != b);
    }

    [Fact]
    public void CompareTo_LessThan()
    {
        var a = new Entity(1, 0);
        var b = new Entity(2, 0);
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void CompareTo_GreaterThan()
    {
        var a = new Entity(2, 0);
        var b = new Entity(1, 0);
        Assert.True(a.CompareTo(b) > 0);
    }

    [Fact]
    public void CompareTo_Equal()
    {
        var a = new Entity(5, 3);
        var b = new Entity(5, 3);
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void CompareTo_HigherGenerationIsGreater()
    {
        var a = new Entity(0, 1);
        var b = new Entity(0, 0);
        Assert.True(a.CompareTo(b) > 0);
    }

    [Fact]
    public void OperatorLessThan()
    {
        var a = new Entity(1, 0);
        var b = new Entity(2, 0);
        Assert.True(a < b);
        Assert.False(b < a);
    }

    [Fact]
    public void OperatorGreaterThan()
    {
        var a = new Entity(2, 0);
        var b = new Entity(1, 0);
        Assert.True(a > b);
        Assert.False(b > a);
    }

    [Fact]
    public void OperatorLessThanOrEqual()
    {
        var a = new Entity(1, 0);
        var b = new Entity(1, 0);
        var c = new Entity(2, 0);
        Assert.True(a <= b);
        Assert.True(a <= c);
        Assert.False(c <= a);
    }

    [Fact]
    public void OperatorGreaterThanOrEqual()
    {
        var a = new Entity(2, 0);
        var b = new Entity(2, 0);
        var c = new Entity(1, 0);
        Assert.True(a >= b);
        Assert.True(a >= c);
        Assert.False(c >= a);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var e = new Entity(42, 7);
        Assert.Equal("Entity(42, 7)", e.ToString());
    }

    [Fact]
    public void ToString_ZeroValues()
    {
        var e = new Entity(0, 0);
        Assert.Equal("Entity(0, 0)", e.ToString());
    }
}
