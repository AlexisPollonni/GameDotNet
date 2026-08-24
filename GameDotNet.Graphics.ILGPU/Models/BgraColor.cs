using System.Drawing;
using System.Runtime.InteropServices;
using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Services;

namespace GameDotNet.Graphics.ILGPU.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public readonly record struct BgraColor(byte B, byte G, byte R, byte A) : IKernelColor
{
    public static implicit operator Color(BgraColor color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);

    public static implicit operator BgraColor(Color color) =>
        new(color.R, color.G, color.B, color.A);
};
