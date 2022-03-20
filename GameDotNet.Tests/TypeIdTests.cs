using System;
using GameDotNet.Core.ECS;
using GameDotNet.Core.Physics.Components;
using NUnit.Framework;

namespace GameDotNet.Tests;

[TestFixture]
public class TypeIdTests
{
    [Test]
    public void TypeTest()
    {
        var t = TypeId.Get<TestComponent>();
        var tt = TypeId<TestComponent>.Get;
        var t2 = TypeId<Translation>.Get;

        Console.WriteLine($"TypeId #1: {t.Id} - {t.Name}");

        Assert.AreEqual(t, tt);

        Assert.AreNotEqual(t, t2);
        Assert.AreNotEqual(tt, t2);
    }
}