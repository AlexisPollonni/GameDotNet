using System.Diagnostics;
using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Services;

/// <summary>
/// Pass that draws a mandelbrot pattern on an image buffer. Pans and zooms in and out to show movement. Overwrites the buffer passed into inputs.
/// </summary>
/// <typeparam name="TColor">Color type of the image buffer to draw on.</typeparam>
public sealed class MandelbrotRenderPass<TColor>
    : IRenderPass<
        MandelbrotRenderPass<TColor>.InputOutput,
        MandelbrotRenderPass<TColor>.InputOutput
    >
    where TColor : unmanaged, IKernelColor
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public readonly record struct InputOutput(
        BufferHandle<TColor, Index2D, Stride2D.DenseX> MandelbrotBuffer
    );

    public InputOutput Setup(IFrameGraphBuilder builder, InputOutput input)
    {
        builder.Write(input.MandelbrotBuffer);
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
        var elapsed = _stopwatch.Elapsed;
        var view = context.GetView2D(input.MandelbrotBuffer);

        accelerator.LaunchAutoGrouped(MandelbrotKernel, stream, view.IntExtent, elapsed, view);
    }

    /// <summary>
    /// ILGPU kernel for Mandelbrot set, writing directly to a 2D view.
    /// </summary>
    private static void MandelbrotKernel(
        Index2D index2D,
        TimeSpan elapsedTime,
        ArrayView2D<TColor, Stride2D.DenseX> output
    )
    {
        var width = output.IntExtent.X;
        var height = output.IntExtent.Y;

        // Convert to 2D
        var imgX = index2D.X;
        var imgY = index2D.Y;

        // 1. Extract single-precision seconds directly from Ticks (Avoids double-precision slowdown)
        var time = (float)elapsedTime.Ticks / TimeSpan.TicksPerSecond;

        const int maxIterations = 100;

        // 2. Animate Zoom and Pan using the time float
        var wave = MathF.Sin(time * 0.5f);
        var zoom = 1.0f - (0.4f * wave);

        var panX = -0.7f + (0.1f * MathF.Cos(time * 0.5f));
        var panY = 0.0f + (0.1f * MathF.Sin(time * 0.25f));

        var aspect = (float)width / height;
        var normalizedX = (float)imgX / width;
        var normalizedY = (float)imgY / height;

        var x0 = panX + (normalizedX - 0.5f) * 3.0f * zoom * aspect;
        var y0 = panY + (normalizedY - 0.5f) * 3.0f * zoom;

        var x = 0.0f;
        var y = 0.0f;
        var iteration = 0;

        // 3. Mandelbrot Loop
        while ((x * x + y * y < 256.0f) && (iteration < maxIterations))
        {
            var xtemp = x * x - y * y + x0;
            y = 2.0f * x * y + y0;
            x = xtemp;
            iteration++;
        }

        // 4. Smooth Coloring
        TColor color;
        if (iteration == maxIterations)
        {
            color = new()
            {
                B = 0,
                G = 0,
                R = 0,
                A = 255,
            };
        }
        else
        {
            var logZn = MathF.Log(x * x + y * y) / 2.0f;
            var nu = MathF.Log(logZn / MathF.Log(2.0f)) / MathF.Log(2.0f);
            var smoothIteration = iteration + 1.0f - nu;

            var t = smoothIteration / maxIterations;
            var colorPhase = time * 2.0f;

            color = new()
            {
                B = (byte)(MathF.Max(0, MathF.Sin(t * 10.0f + colorPhase + 4.0f)) * 255),
                G = (byte)(MathF.Max(0, MathF.Sin(t * 10.0f + colorPhase + 2.0f)) * 255),
                R = (byte)(MathF.Max(0, MathF.Sin(t * 10.0f + colorPhase + 0.0f)) * 255),
                A = 255,
            };
        }

        output[new(imgX, imgY)] = color;
    }
}
