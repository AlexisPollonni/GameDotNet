using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Services;

/// <summary>
/// Clears an image with a specific color. Overwrites the buffer passed into inputs.
/// </summary>
/// <typeparam name="TClearColor">Type of the color pixels</typeparam>
public sealed class ClearRenderPass<TClearColor>
    : IRenderPass<
        ClearRenderPass<TClearColor>.InputOutput,
        ClearRenderPass<TClearColor>.InputOutput
    >
    where TClearColor : unmanaged, IKernelColor
{
    public readonly record struct InputOutput(
        TClearColor ClearColor,
        BufferHandle<TClearColor, Index2D, Stride2D.DenseX> BufferToClear
    );

    public InputOutput Setup(IFrameGraphBuilder builder, InputOutput input)
    {
        builder.Write(input.BufferToClear);

        return input;
    }

    public void Execute(
        Accelerator accelerator,
        AcceleratorStream stream,
        IFrameGraphContext context,
        InputOutput input,
        InputOutput output
    )
    {
        var visView = context.GetView2D(output.BufferToClear);

        accelerator.LaunchAutoGrouped(
            ClearKernel,
            stream,
            visView.IntExtent,
            visView,
            input.ClearColor
        );
    }

    private static void ClearKernel(
        Index2D index,
        ArrayView2D<TClearColor, Stride2D.DenseX> view,
        TClearColor color
    )
    {
        view[index] = color;
    }
}
