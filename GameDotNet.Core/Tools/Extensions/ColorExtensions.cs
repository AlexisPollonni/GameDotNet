using System.Drawing;
using System.Numerics;

namespace GameDotNet.Core.Tools.Extensions;

public static class ColorExtensions
{
    public static Vector4 ToVector4(this Color c) =>
        new((float)c.R / byte.MaxValue, (float)c.G / byte.MaxValue, (float)c.B / byte.MaxValue,
            (float)c.A / byte.MaxValue);

    public static Color ToColor(this Vector4 v) => Color.FromArgb((int)(v.X * byte.MaxValue),
                                                                  (int)(v.Y * byte.MaxValue),
                                                                  (int)(v.Z * byte.MaxValue),
                                                                  (int)(v.W * byte.MaxValue));
}