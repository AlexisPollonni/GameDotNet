using System.Numerics;
using GameDotNet.Core.Abstractions;
using Vogen;

namespace GameDotNet.Core.Physics.Components;

[ValueObject<Matrix4x4>(fromPrimitiveCasting: CastOperator.Implicit,
                        toPrimitiveCasting: CastOperator.Implicit,
                        primitiveEqualityGeneration: PrimitiveEqualityGeneration.GenerateOperatorsAndMethods)]
public readonly partial struct LocalToWorld : ISceneComponent
{
    public Vector3 Right => new(Value.M11, Value.M12, Value.M13);
    public Vector3 Up => new(Value.M21, Value.M22, Value.M23);
    public Vector3 Forward => new(Value.M31, Value.M32, Value.M33);
    public Vector3 Position => Value.Translation;

    public Quaternion Rotation => Quaternion.CreateFromRotationMatrix(Value);
}