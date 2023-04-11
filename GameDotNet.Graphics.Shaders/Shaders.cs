using GameDotNet.Core.Shaders.Generated;

namespace GameDotNet.Graphics.Shaders;

//TODO: Workaround before modifying shader generator to be used across libraries
public static class Shaders
{
    public static ReadOnlySpan<byte> MeshVertexShader => CompiledShaders.MeshVertexShader;
    public static ReadOnlySpan<byte> MeshFragmentShader => CompiledShaders.MeshFragmentShader;
}