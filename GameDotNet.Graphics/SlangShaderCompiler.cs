using CommunityToolkit.HighPerformance;
using GameDotNet.Graphics.Abstractions;
using Microsoft.Extensions.Logging;
using SlangNet;
using SlangNet.Bindings.Generated;

namespace GameDotNet.Graphics;

public sealed partial class SlangShaderCompiler : IDisposable
{
    private readonly ILogger<SlangShaderCompiler> _logger;
    private readonly GlobalSession _globalSession;
    private readonly Session _session;

    private readonly Dictionary<string, Module> _loadedModules = new();

    public SlangShaderCompiler(ILogger<SlangShaderCompiler> logger, IList<string> searchPaths)
    {
        _logger = logger;

        _globalSession = GlobalSession.Create();

        var desc = new SessionDescription
        {
            DefaultMatrixLayoutMode = MatrixLayoutMode.RowMajor,
            Targets =
            {
                new()
                {
                    Format = CompileTarget.Spirv,
                    Flags = TargetFlags.GenerateSPIRVDirectly,
                    Profile = _globalSession.FindProfile("spirv_1_5"u8)
                }
            }
        };

        foreach (var p in searchPaths) desc.SearchPaths.Add(p);

        _session = _globalSession.CreateSession(desc);
    }

    public bool LoadModule(string moduleName)
    {
        if (_loadedModules.ContainsKey(moduleName)) return false;

        var module = _session.LoadModule(moduleName);

        _loadedModules.Add(moduleName, module);
        return true;
    }

    public IReadOnlyList<SpirVShader> CompileAndGetShaderCode(string moduleName)
    {
        if (!_loadedModules.TryGetValue(moduleName, out var module))
        {
            throw new KeyNotFoundException($"Module {moduleName} not found");
        }

        List<SpirVShader> shaders = [];

        foreach (var entryPointLayout in module.GetLayout().EntryPoints)
        {
            using var entryPoint = module.GetEntryPointByName(entryPointLayout.Name);

            using var composite = _session.CreateCompositeComponentType([module, entryPoint]);

            using var linkedprogram = composite.Link();

            var code = linkedprogram.GetEntryPointCode(0, 0);

            shaders.Add(new(code.Memory.Span.Cast<byte, uint>(),
                            new()
                            {
                                EntryPoint = moduleName,
                                Stage = SlangStageToStage(entryPointLayout.Stage),
                                Name = entryPointLayout.Name,
                            }));
        }
        
        return shaders;
    }

    private static Stage StageToSlangStage(ShaderStage stage) =>
        stage switch
        {
            ShaderStage.Vertex => Stage.Vertex,
            ShaderStage.Geometry => Stage.Geometry,
            ShaderStage.Fragment => Stage.Fragment,
            ShaderStage.Compute => Stage.Compute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    private static ShaderStage SlangStageToStage(Stage stage) =>
        stage switch
        {
            Stage.Vertex => ShaderStage.Vertex,
            Stage.Geometry => ShaderStage.Geometry,
            Stage.Fragment => ShaderStage.Fragment,
            Stage.Compute => ShaderStage.Compute,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };

    [LoggerMessage(LogLevel.Information, Message = "Compilation SUCCESS: {ShaderName}, {WarnNber} warnings")]
    private static partial void SucceededShaderCompilation(ILogger l, string shaderName, uint warnNber);

    public void Dispose()
    {
        foreach (var module in _loadedModules.Values) module.Dispose();
        _session.Dispose();
        _globalSession.Dispose();
    }
}