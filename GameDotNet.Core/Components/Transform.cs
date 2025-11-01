using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using Serilog;

namespace GameDotNet.Core.Components;

public record struct Transform
{
    public Transform() : this(Translation.Zero, Rotation.Identity, Scale.One)
    { }

    public Transform(in Translation translation, in  Rotation rotation, in Scale scale)
    {
        Translation = translation;
        Rotation = rotation;
        Scale = scale;
    }

    public Transform(in Matrix4x4 matrix)
    {
        this = FromMatrix(matrix);
    }

    public Translation Translation { get; set; }
    public Rotation Rotation { get; set; }
    public Scale Scale { get; set; }

    public static implicit operator Scale(in Transform t) => t.Scale;
    public static implicit operator Rotation(in Transform t) => t.Rotation;
    public static implicit operator Translation(in Transform t) => t.Translation;

    public static Transform operator *(in Transform a, in Transform b)
    {
        return new(a.ToMatrix() * b.ToMatrix());
    }

    public readonly Scale ToScale() => this;
    public readonly Rotation ToRotation() => this;
    public readonly Translation ToTranslation() => this;

    public readonly Matrix4x4 ToMatrix()
    {
        return ToMatrix(Translation, Rotation, Scale);
    }


    public static Matrix4x4 ToMatrix(in Translation translation, in Rotation rotation, in Scale scale)
        => Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) *
           Matrix4x4.CreateTranslation(translation);

    public static Transform FromMatrix(in Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out var scale, out var rotation, out var translation))
            Log.Warning("Failed to try decomposing matrix {Matrix}", matrix);

        return new(translation, rotation, scale);
    }

    public static Transform? FromEntity(Entity entity)
    {
        ref readonly var t = ref entity.TryGetRef<Translation>(out var traExists);
        ref readonly var r = ref entity.TryGetRef<Rotation>(out var rotExists);
        ref readonly var s = ref entity.TryGetRef<Scale>(out var scaExists);

        return new(traExists ? t : Translation.Zero,
                   rotExists ? r : Rotation.Identity,
                   scaExists ? s : Scale.One
                  );
    }
}