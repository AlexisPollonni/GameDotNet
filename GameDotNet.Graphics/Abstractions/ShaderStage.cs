using Intellenum;
using ShaderSlang.Net.Bindings.Generated;

namespace GameDotNet.Graphics.Abstractions;

[Intellenum<uint>]
[Member("Vertex", Stage.Vertex)]
[Member("Fragment", Stage.Fragment)]
[Member("Compute", Stage.Compute)]
[Member("Geometry", Stage.Geometry)]
public partial class ShaderStage
{
    public static implicit operator Stage(ShaderStage stage) => stage.ToSlangStage();

    public static implicit operator ShaderStage(Stage stage) => FromSlangStage(stage);
    
    public Stage ToSlangStage() => (Stage)Value;

    public static ShaderStage FromSlangStage(Stage stage)
    {
        if(TryFromValue((uint)stage, out var shaderStage)) return shaderStage;

        throw new ArgumentOutOfRangeException(nameof(stage), $"Slang stage {stage} is not supported");
    }
}