using Intellenum;
using SlangShaderSharp;

namespace GameDotNet.Graphics.Models;

[Intellenum<uint>]
[Member("Vertex", SlangStage.Vertex)]
[Member("Fragment", SlangStage.Fragment)]
[Member("Compute", SlangStage.Compute)]
[Member("Geometry", SlangStage.Geometry)]
public partial class ShaderStage
{
    public static implicit operator SlangStage(ShaderStage stage) => stage.ToSlangStage();

    public static implicit operator ShaderStage(SlangStage stage) => FromSlangStage(stage);
    
    public SlangStage ToSlangStage() => (SlangStage)Value;

    public static ShaderStage FromSlangStage(SlangStage stage)
    {
        if(TryFromValue((uint)stage, out var shaderStage)) return shaderStage;

        throw new ArgumentOutOfRangeException(nameof(stage), $"Slang stage {stage} is not supported");
    }
}