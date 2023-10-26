using System.Runtime.CompilerServices;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using unsafe CommandEncoderPtr = Silk.NET.WebGPU.CommandEncoder*;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public struct RenderPassColorAttachment
{
    public TextureView View;

    public TextureView? ResolveTarget;

    public LoadOp LoadOp;

    public StoreOp StoreOp;

    public Color ClearValue;
}

public struct RenderPassDepthStencilAttachment
{
    public TextureView View;

    public LoadOp DepthLoadOp;

    public StoreOp DepthStoreOp;

    public float DepthClearValue;

    public bool DepthReadOnly;

    public LoadOp StencilLoadOp;

    public StoreOp StencilStoreOp;

    public uint StencilClearValue;

    public bool StencilReadOnly;
}

public struct ImageCopyTexture
{
    public Texture Texture;

    public uint MipLevel;

    public Origin3D Origin;

    public TextureAspect Aspect;

    public static implicit operator Silk.NET.WebGPU.ImageCopyTexture(ImageCopyTexture t)
    {
        unsafe
        {
            return new()
            {
                Texture = t.Texture.Handle,
                MipLevel = t.MipLevel,
                Origin = t.Origin,
                Aspect = t.Aspect
            };
        }
    }
}

public struct ImageCopyBuffer
{
    public Buffer Buffer;

    public TextureDataLayout TextureDataLayout;

    public static implicit operator Silk.NET.WebGPU.ImageCopyBuffer(ImageCopyBuffer t)
    {
        unsafe
        {
            return new()
            {
                Layout = t.TextureDataLayout,
                Buffer = t.Buffer.Handle
            };
        }
    }
}

public sealed class CommandEncoder : IDisposable
{
    private readonly WebGPU _api;
    private unsafe CommandEncoderPtr _handle;

    internal unsafe CommandEncoderPtr Handle
    {
        get
        {
            if (_handle is null)
                throw new HandleDroppedOrDestroyedException(nameof(CommandEncoder));

            return _handle;
        }

        private set => _handle = value;
    }

    internal unsafe CommandEncoder(WebGPU api, CommandEncoderPtr handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(CommandEncoder));
        _api = api;

        Handle = handle;
    }

    public unsafe ComputePassEncoder BeginComputePass(string label)
    {
        using var mem = label.ToGlobalMemory();
        return new(_api, _api.CommandEncoderBeginComputePass(_handle, new ComputePassDescriptor
                   {
                       Label = mem.AsPtr<byte>()
                   })
                  );
    }

    public unsafe RenderPassEncoder BeginRenderPass(string label,
                                                    RenderPassColorAttachment[] colorAttachments,
                                                    RenderPassDepthStencilAttachment? depthStencilAttachment = null
    )
    {
        Silk.NET.WebGPU.RenderPassDepthStencilAttachment depthStencilAttachmentInner;

        if (depthStencilAttachment != null)
        {
            depthStencilAttachmentInner = new()
            {
                View = depthStencilAttachment.Value.View.Handle,
                DepthLoadOp = depthStencilAttachment.Value.DepthLoadOp,
                DepthStoreOp = depthStencilAttachment.Value.DepthStoreOp,
                DepthClearValue = depthStencilAttachment.Value.DepthClearValue,
                DepthReadOnly = depthStencilAttachment.Value.DepthReadOnly,
                StencilLoadOp = depthStencilAttachment.Value.StencilLoadOp,
                StencilStoreOp = depthStencilAttachment.Value.StencilStoreOp,
                StencilClearValue = depthStencilAttachment.Value.StencilClearValue,
                StencilReadOnly = depthStencilAttachment.Value.StencilReadOnly
            };
        }

        Span<Silk.NET.WebGPU.RenderPassColorAttachment> colorAttachmentsInner =
            stackalloc Silk.NET.WebGPU.RenderPassColorAttachment[colorAttachments.Length];

        for (var i = 0; i < colorAttachments.Length; i++)
        {
            var colorAttachment = colorAttachments[i];

            colorAttachmentsInner[i] = new()
            {
                View = colorAttachment.View.Handle,
                ResolveTarget = colorAttachment.ResolveTarget is null ? null : colorAttachment.ResolveTarget.Handle,
                LoadOp = colorAttachment.LoadOp,
                StoreOp = colorAttachment.StoreOp,
                ClearValue = colorAttachment.ClearValue
            };
        }

        using var mem = label.ToGlobalMemory();
        RenderPassEncoder encoder;
        fixed (Silk.NET.WebGPU.RenderPassColorAttachment* col = colorAttachmentsInner)
        {
            encoder = new(_api, _api.CommandEncoderBeginRenderPass(Handle, new RenderPassDescriptor
                          {
                              Label = mem.AsPtr<byte>(),
                              ColorAttachments = col,
                              ColorAttachmentCount = (uint)colorAttachments.Length,
                              DepthStencilAttachment =
                                  depthStencilAttachment != null
                                      ? &depthStencilAttachmentInner
                                      : null
                          })
                         );
        }

        return encoder;
    }

    public unsafe void ClearBuffer(Buffer buffer, ulong offset, ulong size)
        => _api.CommandEncoderClearBuffer(Handle, buffer.Handle, offset, size);

    public unsafe void CopyBufferToBuffer(Buffer source, ulong sourceOffset,
                                          Buffer destination, ulong destinationOffset, ulong size)
        => _api.CommandEncoderCopyBufferToBuffer(Handle, source.Handle, sourceOffset,
                                                 destination.Handle, destinationOffset, size);

    public unsafe void CopyBufferToTexture(in ImageCopyBuffer source, in ImageCopyTexture destination,
                                           in Extent3D copySize)
        => _api.CommandEncoderCopyBufferToTexture(Handle, source, destination, in copySize);

    public unsafe void CopyTextureToBuffer(in ImageCopyTexture source, in ImageCopyBuffer destination,
                                           in Extent3D copySize)
        => _api.CommandEncoderCopyTextureToBuffer(Handle, source, destination, in copySize);

    public unsafe void CopyTextureToTexture(in ImageCopyTexture source, in ImageCopyTexture destination,
                                            in Extent3D copySize)
        => _api.CommandEncoderCopyTextureToTexture(Handle, source, destination, in copySize);

    public unsafe CommandBuffer Finish(string label)
    {
        using var mem = label.ToGlobalMemory();
        return new(_api, _api.CommandEncoderFinish(Handle,
                                                   new CommandBufferDescriptor
                                                   {
                                                       Label = mem.AsPtr<byte>()
                                                   }));
    }

    public unsafe void InsertDebugMarker(string markerLabel)
        => _api.CommandEncoderInsertDebugMarker(Handle, markerLabel);

    public unsafe void PushDebugGroup(string groupLabel)
        => _api.CommandEncoderPushDebugGroup(Handle, groupLabel);

    public unsafe void PopDebugGroup() => _api.CommandEncoderPopDebugGroup(Handle);

    public unsafe void ResolveQuerySet(QuerySet querySet, uint firstQuery, uint queryCount, Buffer destination,
                                       ulong destinationOffset)
        => _api.CommandEncoderResolveQuerySet(Handle, querySet.Handle, firstQuery, queryCount, destination.Handle,
                                              destinationOffset);

    public unsafe void WriteTimestamp(QuerySet querySet, uint queryIndex)
        => _api.CommandEncoderWriteTimestamp(Handle, querySet.Handle, queryIndex);

    public unsafe void Dispose()
    {
        _api.CommandEncoderRelease(Handle);
        _handle = null;
    }
}