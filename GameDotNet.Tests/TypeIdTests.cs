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
        var t1 = TypeId.Get<TestComponent>();
        var t2 = TypeId.Get<Translation>();

        Console.WriteLine($"TypeId #1: {t1}, TypeId #2: {t2}");

        Assert.AreNotEqual(t1, t2);
    }
}