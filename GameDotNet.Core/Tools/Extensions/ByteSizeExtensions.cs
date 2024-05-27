using ByteSizeLib;

namespace GameDotNet.Core.Tools.Extensions;

public static class ByteSizeExtensions
{
    public static uint GetBytes(this ByteSize bytes)
    {
        return (uint)bytes.Bytes;
    }
}