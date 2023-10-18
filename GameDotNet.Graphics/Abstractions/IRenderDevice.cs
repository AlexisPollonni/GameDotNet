using System.Drawing;
using ByteSizeLib;

namespace GameDotNet.Graphics.Abstractions;

public interface IRenderDevice
{
    IDeviceBuffer CreateBuffer(BufferDescription desc);
    IDeviceTexture CreateTexture(TextureDescription desc);
    IShader CreateShader(ShaderDescription desc);
    IPipeline CreatePipeline(PipelineDescription desc);

    // Receipt SubmitWork(IDeviceContext context);
    // void WaitOnWork(Receipt receipt);
    void Present();
}

public interface IDeviceContext : IDisposable
{
    /// <summary>
    /// Starts command recording if not started already
    /// </summary>
    void Begin();

    void Reset();

    void ResourceBarrier(BarrierDescription desc);

    public class BarrierDescription
    { }
}

public interface IGraphicsContext : IDeviceContext
{
    IDisposable BeginRenderPass(IRenderPass renderPass);
    void SetPipeline(IGraphicsPipeline pipeline);
    void SetVertexBuffer(IDeviceBuffer buffer);
    void SetIndexBuffer(IDeviceBuffer buffer);
    void Draw(uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0);
}

public interface IRenderPass
{ }

public interface IPipeline
{ }

public interface IGraphicsPipeline : IPipeline
{ }

public interface IComputeContext : IDeviceContext
{
    void SetPipeline(IPipeline pipeline);
    void Dispatch();
}

public interface IUploadContext : IDeviceContext
{
    void UploadBuffer(IDeviceBuffer buffer, ReadOnlySpan<byte> data);
    void UploadTexture(IDeviceTexture buffer, ReadOnlySpan<byte> data);
}

public interface IDeviceBuffer : IDisposable
{
    BufferDescription Description { get; }
}

public interface IDeviceTexture : IDisposable
{
    TextureDescription Description { get; }
}

public interface IShader : IDisposable
{
    public byte[] Code { get; }
    ShaderDescription Description { get; }
}

public class BufferDescription
{
    public ByteSize Size { get; init; }
}

public class TextureDescription
{
    public ByteSize Size { get; init; }
    public Size PixelSize { get; init; }
    public short SampleCount { get; init; }
}

public class ShaderDescription
{
    public required string Name { get; init; }
    public string EntryPoint { get; init; } = "main";
    public required ShaderStage Stage { get; init; }
}

public class PipelineDescription
{ }