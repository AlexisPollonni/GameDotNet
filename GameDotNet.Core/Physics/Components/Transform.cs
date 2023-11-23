using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using Serilog;

namespace GameDotNet.Core.Physics.Components;

public record struct Transform
{
    public Transform() : this(Vector3.One, Quaternion.Identity, Vector3.Zero)
    { }

    public Transform(in Vector3 scale, in Quaternion rotation, in Vector3 translation)
    {
        Scale = scale;
        Rotation = rotation;
        Translation = translation;
    }

    public Transform(in Matrix4x4 matrix)
    {
        this = FromMatrix(matrix);
    }

    public Vector3 Scale { get; set; }
    public Quaternion Rotation { get; set; }
    public Vector3 Translation { get; set; }

    public static implicit operator Scale(in Transform t) => new(t.Scale);
    public static implicit operator Rotation(in Transform t) => new(t.Rotation);
    public static implicit operator Translation(in Transform t) => new(t.Translation);

    public static Transform operator *(in Transform a, in Transform b)
    {
        return new(a.ToMatrix() * b.ToMatrix());
    }

    public readonly Scale ToScale() => this;
    public readonly Rotation ToRotation() => this;
    public readonly Translation ToTranslation() => this;

    public readonly Matrix4x4 ToMatrix()
    {
        return ToMatrix(Scale, Rotation, Translation);
    }


    public static Matrix4x4 ToMatrix(in Vector3 scale, in Quaternion rotation, in Vector3 translation)
        => Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) *
           Matrix4x4.CreateTranslation(translation);

    public static Transform FromMatrix(in Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
            Log.Warning("Failed to try decomposing matrix {Matrix}", matrix);

        return new(scale, rotation, translation);
    }

    public static Transform? FromEntity(Entity entity)
    {
        var hasTransform = entity.TryGet(out Translation translation);
        if (entity.TryGet(out Rotation rotation))
        {
            hasTransform = true;
        }
        if(entity.TryGet(out Scale scale))
        {
            hasTransform = true;
        }

        if (!hasTransform) return null;
        return new()
        {
            Translation = translation,
            Rotation = rotation,
            Scale = scale
        };
    }
}