using SlangShaderSharp;

namespace GameDotNet.Graphics.Vulkan.Services;

/// <summary>
/// Manages the Slang global session and provides shader compilation to SPIR-V.
/// Register as a singleton; the global session lives for the application lifetime.
/// </summary>
public sealed class SlangShaderService : IDisposable
{
    private readonly IGlobalSession _globalSession;
    private bool _disposed;

    public SlangShaderService()
    {
        var result = Slang.CreateGlobalSession(Slang.ApiVersion, out _globalSession);
        if (result.Failed)
            throw new InvalidOperationException($"Failed to create Slang global session (result={result})");
    }

    /// <summary>
    /// Compiles a Slang source module to SPIR-V and returns the compiled program with reflection data.
    /// All entry points declared in the module are compiled and included.
    /// </summary>
    public CompiledShaderProgram Compile(string moduleName, string slangSource)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var targetDesc = new TargetDesc { Format = SlangCompileTarget.Spirv };
        var sessionDesc = new SessionDesc { Targets = [targetDesc] };

        var result = _globalSession.CreateSession(in sessionDesc, out var session);
        if (result.Failed)
            throw new InvalidOperationException($"Slang CreateSession failed (result={result})");

        var module = session.LoadModuleFromSourceString(
            moduleName, $"{moduleName}.slang", slangSource, out var loadDiag);
        if (module is null)
        {
            var diagMsg = loadDiag?.AsString ?? "(no diagnostics)";
            throw new ShaderCompilationException(moduleName, diagMsg);
        }

        // Collect all declared entry points
        int epCount = module.GetDefinedEntryPointCount();
        var entryPoints = new IEntryPoint[epCount];
        for (int i = 0; i < epCount; i++)
        {
            var epResult = module.GetDefinedEntryPoint(i, out entryPoints[i]);
            if (epResult.Failed)
                throw new ShaderCompilationException(moduleName,
                    $"GetDefinedEntryPoint({i}) failed (result={epResult})");
        }

        // Build composite: [module] + all entry points
        var components = new IComponentType[epCount + 1];
        components[0] = module;
        for (int i = 0; i < epCount; i++)
            components[i + 1] = entryPoints[i];

        var compositeResult = session.CreateCompositeComponentType(
            components, out var composite, out var compositeDiag);
        if (compositeResult.Failed)
        {
            var diagMsg = compositeDiag?.AsString ?? "(no diagnostics)";
            throw new ShaderCompilationException(moduleName,
                $"CreateCompositeComponentType failed: {diagMsg}");
        }

        var linkResult = composite.Link(out var linked, out var linkDiag);
        if (linkResult.Failed)
        {
            var diagMsg = linkDiag?.AsString ?? "(no diagnostics)";
            throw new ShaderCompilationException(moduleName, $"Link failed: {diagMsg}");
        }

        var reflection = linked.GetLayout(0, out _);
        if (reflection == ShaderReflection.Null)
            throw new ShaderCompilationException(moduleName, "GetLayout returned null reflection");

        var spirvPerStage = new Dictionary<SlangStage, ReadOnlyMemory<byte>>(epCount);
        for (uint i = 0; i < reflection.EntryPointCount; i++)
        {
            var ep = reflection.GetEntryPointByIndex(i);
            var codeResult = linked.GetEntryPointCode((nint)i, 0, out var code, out _);
            if (codeResult.Failed)
                throw new ShaderCompilationException(moduleName,
                    $"GetEntryPointCode({i}) failed (result={codeResult})");

            spirvPerStage[ep.Stage] = code.Buffer.ToArray();
        }

        return new CompiledShaderProgram(linked, reflection, spirvPerStage);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // IGlobalSession is COM-ref-counted; released by the GC finalizer
    }
}

public sealed class ShaderCompilationException : Exception
{
    public string ModuleName { get; }
    public string Diagnostics { get; }

    public ShaderCompilationException(string moduleName, string diagnostics)
        : base($"Slang shader compilation failed for '{moduleName}':\n{diagnostics}")
    {
        ModuleName = moduleName;
        Diagnostics = diagnostics;
    }
}

public sealed record CompiledShaderProgram(
    IComponentType Linked,
    ShaderReflection Reflection,
    IReadOnlyDictionary<SlangStage, ReadOnlyMemory<byte>> SpirvPerStage);
