using Microsoft.Extensions.Logging;
using ShaderSlang.Net.ComWrappers.Gfx.Descriptions;

namespace GameDotNet.Graphics.Slang.Net;

/// <summary>
/// Manages a Slang Gfx device for rendering.
/// </summary>
public sealed class SlangGfxDevice : IDisposable
{
    private readonly ILogger<SlangGfxDevice> _logger;
    private readonly IDevice _device;
    private readonly ICommandQueue _graphicsQueue;
    private readonly ICommandQueue _computeQueue;
    private readonly ICommandQueue _transferQueue;
    private ITransientResourceHeap? _transientHeap;
    
    /// <summary>
    /// Gets the native Slang Gfx device.
    /// </summary>
    public IDevice Device => _device;
    
    /// <summary>
    /// Gets the graphics command queue.
    /// </summary>
    public ICommandQueue GraphicsQueue => _graphicsQueue;
    
    /// <summary>
    /// Gets the compute command queue.
    /// </summary>
    public ICommandQueue ComputeQueue => _computeQueue;
    
    /// <summary>
    /// Gets the transfer command queue.
    /// </summary>
    public ICommandQueue TransferQueue => _transferQueue;

    public SlangGfxDevice(ILogger<SlangGfxDevice> logger, GfxBackend preferredBackend = GfxBackend.Vulkan)
    {
        _logger = logger;
        _logger.LogInformation("Initializing Slang Gfx device with {Backend} backend", preferredBackend);
        
        var deviceDesc = new DeviceDescription
        {
            PreferredBackend = preferredBackend
        };
        
        // Create the device
        _device = GfxDevice.CreateDevice(in deviceDesc);
        
        // Create command queues
        var graphicsQueueDesc = new CommandQueueDescription 
        { 
            Type = CommandQueueType.Graphics 
        };
        _graphicsQueue = _device.CreateCommandQueue(in graphicsQueueDesc);
        
        var computeQueueDesc = new CommandQueueDescription 
        { 
            Type = CommandQueueType.Compute 
        };
        _computeQueue = _device.CreateCommandQueue(in computeQueueDesc);
        
        var transferQueueDesc = new CommandQueueDescription 
        { 
            Type = CommandQueueType.Transfer 
        };
        _transferQueue = _device.CreateCommandQueue(in transferQueueDesc);
        
        _logger.LogInformation("Slang Gfx device initialized successfully");
    }
    
    /// <summary>
    /// Creates a transient resource heap for command buffer allocations.
    /// </summary>
    /// <returns>The created transient resource heap.</returns>
    public ITransientResourceHeap CreateTransientResourceHeap()
    {
        var desc = new TransientResourceHeapDescription();
        _transientHeap = _device.CreateTransientResourceHeap(in desc);
        return _transientHeap;
    }
    
    /// <summary>
    /// Creates a swapchain for rendering to a window.
    /// </summary>
    /// <param name="windowHandle">Native window handle.</param>
    /// <param name="width">Width of the swapchain.</param>
    /// <param name="height">Height of the swapchain.</param>
    /// <returns>The created swapchain.</returns>
    public ISwapchain CreateSwapchain(IntPtr windowHandle, uint width, uint height)
    {
        var desc = new SwapchainDescription
        {
            Width = width,
            Height = height,
            ImageCount = 3,
            Format = Format.R8G8B8A8_UNORM,
            WindowHandle = windowHandle
        };
        
        return _device.CreateSwapchain(_graphicsQueue, in desc);
    }
    
    /// <summary>
    /// Creates a buffer resource.
    /// </summary>
    /// <param name="description">Buffer description.</param>
    /// <param name="initialData">Optional initial data.</param>
    /// <returns>The created buffer resource.</returns>
    public IBufferResource CreateBuffer(in IBufferResource.Description description, IntPtr initialData = default)
    {
        return _device.CreateBuffer(in description, initialData);
    }
    
    /// <summary>
    /// Creates a texture resource.
    /// </summary>
    /// <param name="description">Texture description.</param>
    /// <param name="initialData">Optional initial data.</param>
    /// <returns>The created texture resource.</returns>
    public ITextureResource CreateTexture(in ITextureResource.Description description, IntPtr initialData = default)
    {
        return _device.CreateTexture(in description, initialData);
    }
    
    /// <summary>
    /// Creates a sampler state.
    /// </summary>
    /// <param name="description">Sampler description.</param>
    /// <returns>The created sampler state.</returns>
    public ISamplerState CreateSamplerState(in ISamplerState.Description description)
    {
        return _device.CreateSamplerState(in description);
    }
    
    /// <summary>
    /// Creates a shader resource view.
    /// </summary>
    /// <param name="resource">Resource to create view for.</param>
    /// <param name="description">View description.</param>
    /// <returns>The created resource view.</returns>
    public IResourceView CreateShaderResourceView(IResource resource, in IResourceView.Description description)
    {
        return _device.CreateShaderResourceView(resource, in description);
    }
    
    /// <summary>
    /// Creates an unordered access view.
    /// </summary>
    /// <param name="resource">Resource to create view for.</param>
    /// <param name="description">View description.</param>
    /// <returns>The created resource view.</returns>
    public IResourceView CreateUnorderedAccessView(IResource resource, in IResourceView.Description description)
    {
        return _device.CreateUnorderedAccessView(resource, in description);
    }
    
    /// <summary>
    /// Creates a render target view.
    /// </summary>
    /// <param name="resource">Resource to create view for.</param>
    /// <param name="description">View description.</param>
    /// <returns>The created resource view.</returns>
    public IResourceView CreateRenderTargetView(IResource resource, in IResourceView.Description description)
    {
        return _device.CreateRenderTargetView(resource, in description);
    }
    
    /// <summary>
    /// Creates a depth-stencil view.
    /// </summary>
    /// <param name="resource">Resource to create view for.</param>
    /// <param name="description">View description.</param>
    /// <returns>The created resource view.</returns>
    public IResourceView CreateDepthStencilView(IResource resource, in IResourceView.Description description)
    {
        return _device.CreateDepthStencilView(resource, in description);
    }
    
    /// <summary>
    /// Creates a graphics pipeline state.
    /// </summary>
    /// <param name="description">Graphics pipeline state description.</param>
    /// <returns>The created graphics pipeline state.</returns>
    public IGraphicsPipelineState CreateGraphicsPipelineState(in IGraphicsPipelineState.Description description)
    {
        return _device.CreateGraphicsPipelineState(in description);
    }
    
    /// <summary>
    /// Creates a compute pipeline state.
    /// </summary>
    /// <param name="description">Compute pipeline state description.</param>
    /// <returns>The created compute pipeline state.</returns>
    public IComputePipelineState CreateComputePipelineState(in IComputePipelineState.Description description)
    {
        return _device.CreateComputePipelineState(in description);
    }
    
    /// <summary>
    /// Creates a shader object.
    /// </summary>
    /// <param name="layout">Shader object layout.</param>
    /// <param name="kernel">Optional shader object kernel.</param>
    /// <returns>The created shader object.</returns>
    public IShaderObject CreateShaderObject(IShaderObjectLayout layout, IShaderObjectKernel? kernel = null)
    {
        return _device.CreateShaderObject(layout, kernel);
    }
    
    public void Dispose()
    {
        _logger.LogInformation("Disposing Slang Gfx device resources");
        
        _transientHeap?.Dispose();
        _transferQueue.Dispose();
        _computeQueue.Dispose();
        _graphicsQueue.Dispose();
        _device.Dispose();
        
        _logger.LogInformation("Slang Gfx device resources disposed");
    }
}
