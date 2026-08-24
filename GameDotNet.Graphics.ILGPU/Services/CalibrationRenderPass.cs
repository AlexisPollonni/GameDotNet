using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Services;

/// <summary>
/// Draws a calibration pattern to test swapchain bounds, aspect ratio, and pixel mapping.
/// Launched as a 2D grid matching the image size, overwrites the buffer passed into inputs.
/// Displays as a red border around the edges of the image, a cyan perfect circle in the middle and a crosshair on a checkerboard background.
/// </summary>
public sealed class CalibrationRenderPass<TColor>
    : IRenderPass<
        CalibrationRenderPass<TColor>.InputOutput,
        CalibrationRenderPass<TColor>.InputOutput
    >
    where TColor : unmanaged, IKernelColor
{
    public readonly record struct InputOutput(
        BufferHandle<TColor, Index2D, Stride2D.DenseX> CalibrationBuffer
    );

    public InputOutput Setup(IFrameGraphBuilder builder, InputOutput input)
    {
        builder.Write(input.CalibrationBuffer);

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
        var visView = context.GetView2D(output.CalibrationBuffer);
        accelerator.LaunchAutoGrouped(CalibrationPatternKernel, stream, visView.IntExtent, visView);
    }

    private static void CalibrationPatternKernel(
        Index2D index,
        ArrayView2D<TColor, Stride2D.DenseX> output
    )
    {
        var x = index.X;
        var y = index.Y;
        var width = output.IntExtent.X;
        var height = output.IntExtent.Y;

        if (x >= width || y >= height)
            return;

        // ---------------------------------------------------------
        // TEST 1: SWAPCHAIN CROP TEST (4-pixel red border)
        // If you don't see a solid red border on all 4 sides of the image,
        // the swapchain is cropping the image.
        // ---------------------------------------------------------
        if (x < 4 || y < 4 || x >= width - 4 || y >= height - 4)
        {
            output[index] = new()
            {
                B = 0,
                G = 0,
                R = 255,
                A = 255,
            }; // Pure Red
            return;
        }

        // ---------------------------------------------------------
        // TEST 2: ASPECT RATIO TEST (Perfect Circle)
        // ---------------------------------------------------------
        var aspect = (float)width / height;

        // Map pixel coordinates to a -1.0 to 1.0 UV space
        var uvX = (x / (float)width) * 2.0f - 1.0f;
        var uvY = (y / (float)height) * 2.0f - 1.0f;

        // Multiply X by the aspect ratio.
        // This ensures our mathematical space is perfectly square.
        uvX *= aspect;

        // Calculate distance from the exact center
        var dist = MathF.Sqrt(uvX * uvX + uvY * uvY);

        // We want a circle that fills 80% of the screen height
        const float circleRadius = 0.8f;
        const float lineThickness = 0.015f;

        TColor color;

        // Draw the circle outline (Cyan)
        // If this circle looks like an oval, the texture is being stretched.
        if (MathF.Abs(dist - circleRadius) < lineThickness)
        {
            color = new()
            {
                B = 255,
                G = 255,
                R = 0,
                A = 255,
            }; // Cyan
        }
        // ---------------------------------------------------------
        // TEST 3: CENTER CROSSHAIR (Green)
        // ---------------------------------------------------------
        else if (MathF.Abs(uvX) < lineThickness / 2.0f || MathF.Abs(uvY) < lineThickness / 2.0f)
        {
            color = new()
            {
                B = 0,
                G = 255,
                R = 0,
                A = 255,
            }; // Green
        }
        // ---------------------------------------------------------
        // TEST 4: 1:1 PIXEL SCALING (Checkerboard)
        // ---------------------------------------------------------
        else
        {
            // 32x32 pixel blocks. If DPI scaling is wrong, these will look blurred.
            const int checkSize = 32;
            var isLight = ((x / checkSize) % 2 == (y / checkSize) % 2);

            color = isLight
                ? new()
                {
                    B = 80,
                    G = 80,
                    R = 80,
                    A = 255,
                } // Light Grey
                : new()
                {
                    B = 40,
                    G = 40,
                    R = 40,
                    A = 255,
                }; // Dark Grey
        }

        output[index] = color;
    }
}
