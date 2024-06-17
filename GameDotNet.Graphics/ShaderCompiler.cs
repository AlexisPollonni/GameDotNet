using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Abstractions;
using Microsoft.Extensions.Logging;
using Vortice.ShaderCompiler;

namespace GameDotNet.Graphics;

public partial class ShaderCompiler(ILogger<ShaderCompiler> logger)
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
                FailedShaderCompilation(logger, Path.GetFileName(path), res.Status, res.ErrorsCount, res.ErrorMessage);
                return null;
            }
            
            SucceededShaderCompilation(logger, Path.GetFileName(path), res.WarningsCount);
            
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

    [LoggerMessage(LogLevel.Error,
        Message = "Compilation FAIL: {ShaderName}, Status = {Status}, {ErrNber} errors, Msg = {ErrMsg}")]
    private partial void FailedShaderCompilation(ILogger l, string shaderName, CompilationStatus status, uint errNber, string? errMsg);

    [LoggerMessage(LogLevel.Information,
        Message = "Compilation SUCCESS: {ShaderName}, {WarnNber} warnings")]
    private partial void SucceededShaderCompilation(ILogger l, string shaderName, uint warnNber);
}