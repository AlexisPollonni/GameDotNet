using System.Numerics;
using Vogen;

namespace GameDotNet.Core.Physics.Components;

[ValueObject<Quaternion>(fromPrimitiveCasting: CastOperator.Implicit,
                         toPrimitiveCasting: CastOperator.Implicit,
                         primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods)]
public readonly partial record struct Rotation
{
    public static readonly Rotation Identity = new(Quaternion.Identity);
}