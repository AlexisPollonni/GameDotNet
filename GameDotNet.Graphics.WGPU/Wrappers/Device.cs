using System.Drawing;
using System.Runtime.CompilerServices;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.WGPU.Extensions;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.Dawn;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Device : IDisposable
{
    public Queue Queue { get; private set; }

    private readonly WebGPU _api;
    private Dawn? _dawn;
    private unsafe Silk.NET.WebGPU.Device* _handle;

    public unsafe Silk.NET.WebGPU.Device* Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(Device));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe Device(WebGPU api, Silk.NET.WebGPU.Device* handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Device));

        _api = api;
        _handle = handle;
        Queue = new(_api, _api.DeviceGetQueue(handle));
    }

    public unsafe BindGroup CreateBindGroup(string label, BindGroupLayout layout, ReadOnlySpan<BindGroupEntry> entries)
    {
        Span<Silk.NET.WebGPU.BindGroupEntry> entriesInner = stackalloc Silk.NET.WebGPU.BindGroupEntry[entries.Length];

        for (var i = 0; i < entries.Length; i++)
        {
            var entryOuter = entries[i];
            entriesInner[i] = new()
            {
                Binding = entryOuter.Binding,
                Buffer = entryOuter.Buffer is null ? null : entryOuter.Buffer.Handle,
                Offset = entryOuter.Offset,
                Size = entryOuter.Size,
                Sampler = entryOuter.Sampler is null ? null : entryOuter.Sampler.Handle,
                TextureView = entryOuter.TextureView is null ? null : entryOuter.TextureView.Handle
            };
        }

        Silk.NET.WebGPU.BindGroup* bd;
        using var mem = label.ToGlobalMemory();
        fixed (Silk.NET.WebGPU.BindGroupEntry* entPtr = entriesInner)
        {
            bd = _api.DeviceCreateBindGroup(_handle, new BindGroupDescriptor
            {
                Label = mem.AsPtr<byte>(),
                Layout = layout.Handle,
                Entries = entPtr,
                EntryCount = (nuint)entries.Length
            });
        }

        return new(_api, bd);
    }

    public unsafe void SetLoggingCallback(LoggingCallback proc)
    {
        _api.TryGetDeviceExtension(_handle, out Dawn d);

        d.DeviceSetLoggingCallback(_handle, new((lvl, msg, _) => proc(lvl, SilkMarshal.PtrToString((nint)msg)!)), null);
    }


    public unsafe SwapChain CreateSwapchain(Surface surface, Size size, TextureFormat fmt, TextureUsage usage,
                                            PresentMode presentMode, string? label = null)
    {
        _dawn ??= _api.GetDawnExtension() ?? throw new PlatformException("Dawn not found");
        
        var mem = label?.ToGlobalMemory();
        
        var swDesc = new SwapChainDescriptor
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
            Format = fmt,
            Usage = usage,
            PresentMode = presentMode,
            Label = mem is null ? null : mem.AsPtr<byte>()
        };
        var swPtr = _dawn.DeviceCreateSwapChain(_handle, surface.Handle, &swDesc);

        if (swPtr is null) throw new ResourceCreationError("swapchain");
        
        return new(_api, _dawn, swPtr);
    }
    
    
    public unsafe BindGroupLayout CreateBindgroupLayout(string label, BindGroupLayoutEntry[] entries)
    {
        using var mem = label.ToGlobalMemory();
        fixed (BindGroupLayoutEntry* entriesPtr = entries)
        {
            return new(_api,
                       _api.DeviceCreateBindGroupLayout(_handle, new BindGroupLayoutDescriptor
                       {
                           Label = mem.AsPtr<byte>(),
                           Entries = entriesPtr,
                           EntryCount = (uint)entries.Length
                       })
                      );
        }
    }

    public unsafe Buffer CreateBuffer(string label, bool mappedAtCreation, ulong size, BufferUsage usage)
    {
        using var mem = label.ToGlobalMemory();
        var desc = new BufferDescriptor
        {
            Label = mem.AsPtr<byte>(),
            MappedAtCreation = mappedAtCreation,
            Size = size,
            Usage = usage
        };

        return new(_api, _api.DeviceCreateBuffer(_handle, desc), desc);
    }

    public unsafe CommandEncoder CreateCommandEncoder(string label)
    {
        using var mem = label.ToGlobalMemory();
        return new(_api, _api.DeviceCreateCommandEncoder(_handle, new CommandEncoderDescriptor
                   {
                       Label = mem.AsPtr<byte>()
                   })
                  );
    }

    public unsafe ComputePipeline CreateComputePipeline(string label, ProgrammableStageDescriptor compute)
    {
        using var mem = label.ToGlobalMemory();
        using var mem2 = compute.EntryPoint.ToGlobalMemory();
        
        return new(_api, _api.DeviceCreateComputePipeline(_handle, new ComputePipelineDescriptor
                   {
                       Label = mem.AsPtr<byte>(),
                       Compute = new()
                       {
                           Module = compute.Module.Handle,
                           EntryPoint = mem2.AsPtr<byte>()
                       }
                   })
                  );
    }

    public unsafe void CreateComputePipelineAsync(string label, CreateComputePipelineAsyncCallback callback,
                                                  ProgrammableStageDescriptor compute)
    {
        using var mem = label.ToGlobalMemory();
        using var mem2 = compute.EntryPoint.ToGlobalMemory();
        _api.DeviceCreateComputePipelineAsync(_handle, new ComputePipelineDescriptor
                                         {
                                             Label = mem.AsPtr<byte>(),
                                             Compute = new()
                                             {
                                                 Module = compute.Module.Handle,
                                                 EntryPoint = mem2.AsPtr<byte>()
                                             }
                                         }, new((s, p, m, _) => callback(s, new(_api, p), SilkMarshal.PtrToString((nint)m)!)), 
                                              null
                                        );
    }

    public unsafe PipelineLayout CreatePipelineLayout(string label, BindGroupLayout[] bindGroupLayouts)
    {
        using var mem = label.ToGlobalMemory();
        Span<nint> bindGroupLayoutsInner = stackalloc nint[bindGroupLayouts.Length];

        for (var i = 0; i < bindGroupLayouts.Length; i++)
            bindGroupLayoutsInner[i] = (nint)bindGroupLayouts[i].Handle;

        return new(_api, _api.DeviceCreatePipelineLayout(_handle, new PipelineLayoutDescriptor
                   {
                       Label = mem.AsPtr<byte>(),
                       BindGroupLayouts = 
                           (Silk.NET.WebGPU.BindGroupLayout**)Unsafe.AsPointer(ref bindGroupLayoutsInner
                                                                                   .GetPinnableReference()),
                       BindGroupLayoutCount = (nuint)bindGroupLayouts.Length
                   })
                  );
    }

    public unsafe QuerySet CreateQuerySet(string label, QueryType queryType, uint count,
                                   PipelineStatisticName[] pipelineStatistics)
    {
        using var mem = label.ToGlobalMemory();
        fixed (PipelineStatisticName* pipelineStatisticsPtr = pipelineStatistics)
        {
            return new(_api, _api.DeviceCreateQuerySet(_handle, new QuerySetDescriptor
                       {
                           Label = mem.AsPtr<byte>(),
                           Type = queryType,
                           Count = count,
                           PipelineStatistics = pipelineStatisticsPtr,
                           PipelineStatisticCount = (nuint)pipelineStatistics.Length
                       })
                      );
        }
    }

    public unsafe RenderBundleEncoder CreateRenderBundleEncoder(string label, TextureFormat[] colorFormats,
                                                         TextureFormat depthStencilFormat,
                                                         uint sampleCount, bool depthReadOnly, bool stencilReadOnly)
    {
        using var mem = label.ToGlobalMemory();
        fixed (TextureFormat* colorFormatsPtr = colorFormats)
        {
            return new(_api, _api.DeviceCreateRenderBundleEncoder(_handle,
                                                                  new RenderBundleEncoderDescriptor
                                                                  {
                                                                      Label = mem.AsPtr<byte>(),
                                                                      ColorFormats = colorFormatsPtr,
                                                                      ColorFormatCount = (nuint)colorFormats.Length,
                                                                      DepthStencilFormat = depthStencilFormat,
                                                                      SampleCount = sampleCount,
                                                                      DepthReadOnly = depthReadOnly,
                                                                      StencilReadOnly = stencilReadOnly
                                                                  })
                      );
        }
    }

    public unsafe RenderPipeline CreateRenderPipeline(string label,
                                                      VertexState vertexState, 
                                                      PrimitiveState primitiveState,
                                                      MultisampleState multisampleState,
                                                      PipelineLayout? layout = null,
                                                      DepthStencilState? depthStencilState = null,
                                                      FragmentState? fragmentState = null)
    {
        using var d = new DisposableList();
        var desc = CreateRenderPipelineDescriptor(label, layout, vertexState, primitiveState,
                                                  multisampleState, depthStencilState,
                                                  fragmentState, d);
        var pipelineImpl = _api.DeviceCreateRenderPipeline(_handle, desc);
        
        return new(_api, pipelineImpl);
    }

    public unsafe void CreateRenderPipelineAsync(string label, CreateRenderPipelineAsyncCallback callback,
                                                 VertexState vertexState, 
                                                 PrimitiveState primitiveState,
                                                 MultisampleState multisampleState,
                                                 PipelineLayout? layout = null,
                                                 DepthStencilState? depthStencilState = null,
                                                 FragmentState? fragmentState = null)
    {
        using var d = new DisposableList();
        var desc = CreateRenderPipelineDescriptor(label, layout, vertexState, primitiveState,
                                                  multisampleState, depthStencilState,
                                                  fragmentState, d);
        _api.DeviceCreateRenderPipelineAsync(_handle, desc, new((s, p, m, _) =>
        {
            RenderPipeline? pipeline = null;
            if(p is not null) 
                pipeline = new(_api, p);
            callback(s, pipeline, SilkMarshal.PtrToString((nint)m)!);
        }), null);
    }

    public async ValueTask<RenderPipeline> CreateRenderPipelineAsync(string label,
                                                                            VertexState vertexState,
                                                                            PrimitiveState primitiveState,
                                                                            MultisampleState multisampleState,
                                                                            PipelineLayout? layout = null,
                                                                            DepthStencilState? depthStencilState = null,
                                                                            FragmentState? fragmentState = null,
                                                                            CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<RenderPipeline>();

        token.ThrowIfCancellationRequested();
        CreateRenderPipelineAsync(label, (status, pipeline, message) =>
                                  {
                                      token.ThrowIfCancellationRequested();
                                      if (status is not CreatePipelineAsyncStatus.Success)
                                      {
                                          tcs.SetException(new PlatformException($"Failed to create render pipeline {message}"));
                                      }
                                      
                                      tcs.SetResult(pipeline);
                                  },
                                  vertexState, primitiveState, multisampleState, layout, depthStencilState, fragmentState);

        return await tcs.Task;
    }

    public delegate void CreateRenderPipelineAsyncCallback(CreatePipelineAsyncStatus status, 
                                                           RenderPipeline? pipeline,
                                                           string message);

    private static unsafe RenderPipelineDescriptor CreateRenderPipelineDescriptor(
        string label, 
        PipelineLayout? layout, 
        VertexState vertexState,
        PrimitiveState primitiveState, 
        MultisampleState multisampleState, 
        DepthStencilState? depthStencilState,
        FragmentState? fragmentState,
        ICompositeDisposable dispose)
    {
        var vBuffLayouts = vertexState.BufferLayouts?.Select(x => new Silk.NET.WebGPU.VertexBufferLayout
        {
            ArrayStride = x.ArrayStride,
            StepMode = x.StepMode,
            Attributes = x.Attributes.AsPtr(dispose),
            AttributeCount = (nuint)x.Attributes.Length
        }).ToArray();

        var vConstLayouts = vertexState.ConstantEntries?.Select(e => new Silk.NET.WebGPU.ConstantEntry
        {
            Key = e.Key.ToPtr(dispose),
            Value = e.Value
        }).ToArray();

        var fragTargets = fragmentState?.ColorTargets
                                       .Select(x => new Silk.NET.WebGPU.ColorTargetState
                                       {
                                           Format = x.Format,
                                           Blend = x.BlendState.ToPtrPinned(dispose),
                                           WriteMask = x.WriteMask
                                       }).ToArray();

        var fConstLayouts = fragmentState?.ConstantEntries?.Select(e => new Silk.NET.WebGPU.ConstantEntry
        {
            Key = e.Key.ToPtr(dispose),
            Value = e.Value
        }).ToArray();
        
        Silk.NET.WebGPU.FragmentState? fragState = null;
        if (fragmentState is not null)
        {
            fragState = new Silk.NET.WebGPU.FragmentState
            {
                Module = fragmentState.Value.Module.Handle,
                EntryPoint = fragmentState.Value.EntryPoint.ToPtr(dispose),
                Targets = fragTargets.AsPtr(dispose),
                TargetCount = (nuint)(fragTargets?.Length ?? 0), 
                Constants = fConstLayouts.AsPtr(dispose),
                ConstantCount = (nuint)(fConstLayouts?.Length ?? 0)
            };
        }

        return new()
        {
            Label = label.ToPtr(dispose),
            Layout = layout is null ? null : layout.Handle,
            Vertex = new()
            {
                Module = vertexState.Module.Handle,
                EntryPoint = vertexState.EntryPoint.ToPtr(dispose),
                Buffers = vBuffLayouts.AsPtr(dispose),
                BufferCount = (nuint)(vertexState.BufferLayouts?.Length ?? 0),
                Constants = vConstLayouts.AsPtr(dispose),
                ConstantCount = (nuint)(vConstLayouts?.Length ?? 0)
            },
            Primitive = primitiveState,
            DepthStencil = depthStencilState.ToPtrPinned(dispose),
            Multisample = multisampleState,
            Fragment = fragState.ToPtrPinned(dispose)
        };
    }
    

    public unsafe Sampler CreateSampler(string label, AddressMode addressModeU, AddressMode addressModeV,
                                        AddressMode addressModeW,
                                        FilterMode magFilter, FilterMode minFilter, MipmapFilterMode mipmapFilter,
                                        float lodMinClamp, float lodMaxClamp, CompareFunction compare, ushort maxAnisotropy)
    {
        using var mem = label.ToGlobalMemory();
        return new(_api, _api.DeviceCreateSampler(_handle, new SamplerDescriptor
                           {
                               Label = mem.AsPtr<byte>(),
                               AddressModeU = addressModeU,
                               AddressModeV = addressModeV,
                               AddressModeW = addressModeW,
                               MagFilter = magFilter,
                               MinFilter = minFilter,
                               MipmapFilter = mipmapFilter,
                               LodMinClamp = lodMinClamp,
                               LodMaxClamp = lodMaxClamp,
                               Compare = compare,
                               MaxAnisotropy = maxAnisotropy
                           })
                          );
    }

    public unsafe ShaderModule CreateSpirVShaderModule(string label, uint[] spirvCode)
    {
        using var d = new DisposableList();
        return new(_api, _api.DeviceCreateShaderModule(_handle, new ShaderModuleDescriptor
                                {
                                    Label = label.ToPtr(d),
                                    Hints = null,
                                    HintCount = 0,
                                    NextInChain = new WgpuStructChain()
                                                  .AddShaderModuleSPIRVDescriptor(spirvCode)
                                                  .DisposeWith(d)
                                                  .Ptr
                                })
                               );
    }

    public unsafe ShaderModule CreateWgslShaderModule(string label, string wgslCode)
    {
        using var d = new DisposableList();
        return new (_api, _api.DeviceCreateShaderModule(_handle, new ShaderModuleDescriptor
                                {
                                    Label = label.ToPtr(d),
                                    NextInChain = new WgpuStructChain()
                                                  .AddShaderModuleWGSLDescriptor(wgslCode)
                                                  .DisposeWith(d)
                                                  .Ptr
                                })
                               );
    }

    public unsafe Texture CreateTexture(string label, TextureUsage usage,
                                        TextureDimension dimension, Extent3D size, TextureFormat format,
                                        uint mipLevelCount, uint sampleCount, TextureFormat[]? viewFormats = null)
    {
        using var mem = label.ToGlobalMemory();
        using var vFmtPtr = viewFormats.AsMemory().Pin();
        var desc = new TextureDescriptor
        {
            Label = mem.AsPtr<byte>(),
            Usage = usage,
            Dimension = dimension,
            Size = size,
            Format = format,
            MipLevelCount = mipLevelCount,
            SampleCount = sampleCount,
            ViewFormats = (TextureFormat*)vFmtPtr.Pointer,
            ViewFormatCount = (nuint)(viewFormats?.Length ?? 0)
        };

        return CreateTexture(in desc);
    }

    public unsafe Texture CreateTexture(in TextureDescriptor descriptor)
    {
        return new(_api, _api.DeviceCreateTexture(_handle, descriptor), descriptor);
    }

    public unsafe FeatureName[] EnumerateFeatures()
    {
        FeatureName features = default;

        var size = _api.DeviceEnumerateFeatures(_handle, ref features);

        var featuresSpan = new Span<FeatureName>(Unsafe.AsPointer(ref features), (int)size);

        var result = new FeatureName[(int)size];

        featuresSpan.CopyTo(result);

        return result;
    }

    public unsafe bool GetLimits(out SupportedLimits limits)
    {
        limits = new SupportedLimits();

        return _api.DeviceGetLimits(_handle, ref limits);
    }

    public unsafe bool HasFeature(FeatureName feature) => _api.DeviceHasFeature(_handle, feature);

    public unsafe void PushErrorScope(ErrorFilter filter) => _api.DevicePushErrorScope(_handle, filter);

    public unsafe void PopErrorScope(ErrorCallback callback)
    {
        _api.DevicePopErrorScope(_handle,
                            new((t, m, _) => callback(t, SilkMarshal.PtrToString((nint)m)!)),
                            null);
    }

    private static readonly List<Silk.NET.WebGPU.ErrorCallback> SErrorCallbacks =
        new();

    public unsafe void SetUncapturedErrorCallback(ErrorCallback callback)
    {
        Silk.NET.WebGPU.ErrorCallback errorCallback = (t, m, _) => callback(t, SilkMarshal.PtrToString((nint)m)!);

        SErrorCallbacks.Add(errorCallback);

        _api.DeviceSetUncapturedErrorCallback(_handle,
                                         errorCallback,
                                         null);
    }

    public unsafe void Dispose()
    {
        Queue.Dispose();
        Queue = null;

        _api.DeviceRelease(_handle);
        _api.DeviceDestroy(_handle);
        _handle = null;
    }
}