using System.Numerics;
using Vogen;

namespace GameDotNet.Core.Physics.Components;

[ValueObject<Vector3>(fromPrimitiveCasting: CastOperator.Implicit,
                      toPrimitiveCasting: CastOperator.Implicit,
                      primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods)]
public readonly partial record struct Translation
{
    public static readonly Translation Zero = new(Vector3.Zero);
    public static readonly Translation One = new(Vector3.One);
}