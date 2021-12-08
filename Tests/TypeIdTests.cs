using System;
using Core.ECS;
using Core.Physics;
using NUnit.Framework;

namespace Tests;

[TestFixture]
public class TypeIdTests
{
    [Test]
    public void TypeTest()
    {
        var t = TypeId.Get<TestComponent>();
        var tt = TypeId<TestComponent>.Get;
        var t2 = TypeId<Transform3DComponent>.Get;

        Console.WriteLine($"TypeId #1: {t.Id} - {t.Name}");
        
        Assert.AreEqual(t, tt);
        
        Assert.AreNotEqual(t, t2);
        Assert.AreNotEqual(tt, t2);
    }
}