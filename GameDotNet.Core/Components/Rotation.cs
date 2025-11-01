using System.Numerics;
using GameDotNet.Core.Abstractions;
using Vogen;

namespace GameDotNet.Core.Physics.Components;

[ValueObject<Quaternion>(fromPrimitiveCasting: CastOperator.Implicit,
                         toPrimitiveCasting: CastOperator.Implicit,
                         primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods)]
public readonly partial record struct Rotation : ISceneComponent
{
    public static readonly Rotation Identity = new(Quaternion.Identity);
}