using System.Numerics;
using Vogen;

namespace GameDotNet.Core.Physics.Components;

[ValueObject<Vector3>(fromPrimitiveCasting: CastOperator.Implicit,
                      toPrimitiveCasting: CastOperator.Implicit,
                      primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods)]
public readonly partial record struct Scale
{
    public static readonly Scale One = new(Vector3.One);

    public static readonly Scale Zero = new(Vector3.Zero);
}