using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Abstractions;
using Microsoft.Extensions.Logging;
using Vortice.ShaderCompiler;

namespace GameDotNet.Graphics;

public sealed class SpirVShader : IShader
{
    public SpirVShader(ReadOnlySpan<uint> code, ShaderDescription description)
    {
        Code = code.ToArray();
        Description = description;
    }

    public Task SaveToFile(string path, CancellationToken token = default) 
        => File.WriteAllBytesAsync(path, Code.AsSpan().AsBytes().ToArray(),token);

    public void Dispose()
    { }

    public uint[] Code { get; }
    public ShaderDescription Description { get; }
}

public class ShaderCompiler(ILogger<ShaderCompiler> logger)
{
    public async Task<SpirVShader?> TranslateGlsl(string path, string includePath = ".",
                                                  CancellationToken token = default)
    {
        return await Task.Run(async () =>
        {
            var ext = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(ext)) return null;

            var stage = FileExtensionToStage(ext);
            if (stage is null) return null;

            var source = await File.ReadAllTextAsync(path, token);

            using var compiler = new Compiler();
            var opt = compiler.Options;

            opt.SetargetSpirv(SpirVVersion.Version_1_0);
            opt.SetSourceLanguage(SourceLanguage.GLSL);
            opt.SetGenerateDebugInfo();

            compiler.Includer = new Includer(includePath);

            using var res = compiler.Compile(source, Path.GetFileName(path), StageToShaderKind(stage.Value));

            if (res.Status != CompilationStatus.Success)
            {
                logger.LogError("Compilation FAIL: {ShaderName}, Status = {Status}, {ErrNber} errors, Msg = {ErrMsg}",
                                 Path.GetFileName(path), res.Status, res.ErrorsCount, res.ErrorMessage);
                return null;
            }

            logger.LogInformation("Compilation SUCCESS: {ShaderName}, {WarnNber} warnings", Path.GetFileName(path),
                                   res.WarningsCount);
            
            return new SpirVShader(res.GetBytecode().Cast<byte, uint>(), new()
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Stage = stage.Value,
                EntryPoint = "main"
            });
        }, token);
    }

    private static ShaderStage? FileExtensionToStage(string ext) => ext switch
    {
        ".vert" => ShaderStage.Vertex,
        ".frag" => ShaderStage.Fragment,
        _ => null
    };

    private static ShaderKind StageToShaderKind(ShaderStage stage) => stage switch
    {
        ShaderStage.Vertex => ShaderKind.VertexShader,
        ShaderStage.Geometry => ShaderKind.GeometryShader,
        ShaderStage.Fragment => ShaderKind.FragmentShader,
        ShaderStage.Compute => ShaderKind.ComputeShader,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };
}