using Microsoft.Extensions.Logging;
using ShaderSlang.Net.Bindings.Generated;
using ShaderSlang.Net.ComWrappers;
using ShaderSlang.Net.ComWrappers.Gfx;
using ShaderSlang.Net.ComWrappers.Gfx.Descriptions;
using ShaderSlang.Net.ComWrappers.Tools.Extensions;
using ShaderSlang.Net.Pretty.Gfx.Tools;
using static ShaderSlang.Net.Bindings.Generated.ITransientResourceHeap.Flags.TransientResourceHeapFlagsEnum;
using ICommandQueue = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.ICommandQueue;
using IDevice = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.IDevice;
using IEntryPoint = ShaderSlang.Net.ComWrappers.Interfaces.IEntryPoint;
using ISession = ShaderSlang.Net.ComWrappers.Interfaces.ISession;
using IModule = ShaderSlang.Net.ComWrappers.Interfaces.IModule;
using ITransientResourceHeap = ShaderSlang.Net.ComWrappers.Gfx.Interfaces.ITransientResourceHeap;
using ShaderStage = GameDotNet.Graphics.Models.ShaderStage;

namespace GameDotNet.Graphics.Services;

[RegisterSingleton<SlangContext>]
public sealed class SlangContext
{
    private readonly ILogger<SlangContext> _logger;

    private readonly Dictionary<string, IModule> _loadedModules = new();
    
    public ISession Session { get; }
    public IDevice Device { get; }
    public ITransientResourceHeap TransientHeap { get; set; }
    public ICommandQueue GraphicsQueue { get; set; }


    public SlangContext(ILogger<SlangContext> logger, IEnumerable<string> searchPaths)
    {
        _logger = logger;
        var globalSession = Slang.CreateGlobalSession();

        var slangDesc = new SlangDescription
        {
            DefaultMatrixLayoutMode = MatrixLayoutMode.RowMajor,
            SearchPaths = searchPaths.ToArray(),
            GlobalSession = globalSession,
        };

        var deviceDesc = new DeviceDescription
        {
            Slang = slangDesc,
            // Force Vulkan backend for now before DX12 support is added.
            DeviceType = DeviceType.Vulkan,
        };
        
        Gfx.CreateDevice(deviceDesc, out var device).ThrowIfFailed();
        Device = device;

        Session = Device.GetSlangSessionOrThrow();
        TransientHeap = Device.CreateTransientResourceHeapOrThrow(new(AllowResizing));
        GraphicsQueue =
            Device.CreateCommandQueueOrThrow(new(ShaderSlang.Net.Bindings.Generated.ICommandQueue.QueueType.Graphics));
    }

    public bool TryLoadModule(string moduleName)
    {
        if (_loadedModules.ContainsKey(moduleName)) return true;

        var module = Session.LoadModule(moduleName, out var diagnostics);

        if (module is null)
        {
            _logger.LogError("Failed to load slang shader module {ModuleName}, diagnostics : {Diagnostics}", moduleName, diagnostics.AsString());
            return false;
        }

        _loadedModules.Add(moduleName, module);
        return true;
    }

    /// <summary>
    /// Gets a previously loaded module.
    /// </summary>
    /// <param name="moduleName">Name of the module.</param>
    /// <returns>The module, or null if not found.</returns>
    public IModule? GetModule(string moduleName)
    {
        return _loadedModules.TryGetValue(moduleName, out var module) ? module : null;
    }

    /// <summary>
    /// Gets all loaded modules.
    /// </summary>
    /// <returns>Dictionary of loaded modules.</returns>
    public IReadOnlyDictionary<string, IModule> GetLoadedModules()
    {
        return _loadedModules;
    }

    
    public IEnumerable<IEntryPoint> GetEntryPointsByStage(string moduleName, ShaderStage stage)
    {
        
        if (!TryLoadModule(moduleName)) return [];
        var module = GetModule(moduleName);

        if (module is null) return [];
        
        var moduleLayout = module.GetLayout(0, out var diag);

        if(diag is not null)
            _logger.LogWarning("Module layout had diagnostic output : {Diagnostics}", diag);
        if (moduleLayout is null)
        {
            _logger.LogError("Failed to retrieve module layout for module {ModuleName}", moduleName);
            return [];
        }

        return moduleLayout.Value.EntryPoints
                           .Where(r => r.Stage == stage.ToSlangStage())
                           .Select(r =>
                           {
                               module.FindAndCheckEntryPoint(r.Name!, stage, out var entryPoint, out var diagCheck)
                                     .ThrowIfFailed();

                               if (diagCheck is not null)
                                   _logger.LogWarning("Module find entrypoint has diagnostic output : {Diagnostics}",
                                                      diagCheck);

                               return entryPoint;
                           });
    }

    // public IInputLayout ComputeInputLayoutFromEntryPoint(IEntryPoint entryPoint)
    // {
    //     var funcReflection = entryPoint.GetFunctionReflection();
    //
    //     var entryParameter = funcReflection?.Parameters.SingleOrDefault();
    //     if (entryParameter is null)
    //     {
    //
    //     }
    //
    //     var parameterType = entryParameter?.Type;
    //     if (parameterType is null)
    //     {
    //
    //     }
    //
    //     if (parameterType?.Kind is not TypeKind.Struct)
    //     {
    //         LogMessages.ShaderEntryPointInvalidForInput();
    //     }
    //
    //     parameterType?.Fields.Select(r => )
    //
    //
    //     var inputLayoutDesc = new InputLayoutDescription( ,
    //     [new()
    //     {
    //         stride = ,
    //         slotClass = InputSlotClass.PerVertex,
    //         instanceDataStepRate = 0
    //     }]);
    //
    //     return Device.CreateInputLayoutOrThrow(inputLayoutDesc);
    // }




}

abstract class GpuPipeline(SlangContext context, IModule module)
{
    
}

interface IShaderParameterWriter
{
    void WriteTo(ShaderCursor cursor);
}