namespace GameDotNet.Graphics.Abstractions;

[Flags]
public enum ShaderStage
{
    Vertex = 1,
    Geometry = 8,
    Fragment = 16,
    Compute = 32
}