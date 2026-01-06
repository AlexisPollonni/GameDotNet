using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ByteSizeLib;
using CommunityToolkit.HighPerformance.Buffers;
using GameDotNet.Core.Components;
using TruePath;
using ZLinq;

namespace GameDotNet.Core.Tooling.Extensions;

public static class GuidV5Extensions
{
    extension(Guid)
    {
        public static Guid NameSpaceDns => Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
        public static Guid NameSpaceUrl => Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
        public static Guid NameSpaceOid => Guid.Parse("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
        public static Guid NameSpaceX500 => Guid.Parse("6ba7b814-9dad-11d1-80b4-00c04fd430c8");
    }
    
    
    /// <summary>
    /// Generate guid v5 by sha1-hashing the nameBytes according to RFC4122.
    /// Adapted from https://stackoverflow.com/a/79586148
    /// </summary>
    /// <param name="nameBytes">The bytes, if it's from a string these should be UTF8</param>
    /// <param name="guidNamespace">You can think of it as a "seed" or "salt" for the hashing function</param>
    /// <returns></returns>
    public static Guid HashToGuidV5(this ReadOnlySpan<byte> nameBytes, Guid guidNamespace)
    {
        Span<byte> namespaceBytes = stackalloc byte[Unsafe.SizeOf<Guid>()];
        guidNamespace.TryWriteBytes(namespaceBytes);
        
        using var data = namespaceBytes.AsValueEnumerable().Concat(nameBytes.AsValueEnumerable()).ToArrayPool();
        
        Span<byte> hashSpan = stackalloc byte[SHA1.HashSizeInBytes];
        
        SHA1.HashData(data.Span, hashSpan);
        
        hashSpan[6] = (byte)((hashSpan[6] & 0x0f) | 0x50); // Version 5
        hashSpan[8] = (byte)((hashSpan[8] & 0x3f) | 0x80); // Variant 10xx
        
        return new(hashSpan[..Unsafe.SizeOf<Guid>()]);
    }

    /// <summary>
    /// Generate guid v5 by sha1-hashing a string according to RFC4122.
    /// </summary>
    /// <param name="guidName">The bytes, if it's a string these should be UTF8</param>
    /// <param name="guidNamespace">You can think of it as a "seed" or "salt" for the hashing function</param>
    public static Guid HashToGuidV5(this string guidName, Guid guidNamespace)
    {
        var utf8ByteCount = Encoding.UTF8.GetByteCount(guidName);
        
        using var utf8NameBytes = SpanOwner<byte>.Allocate(utf8ByteCount, AllocationMode.Default);
        
        Encoding.UTF8.GetBytes(guidName.AsSpan(), utf8NameBytes.Span);
        
        return utf8NameBytes.Span[..utf8ByteCount].HashToGuidV5(guidNamespace);
    }
    
    public static async ValueTask<Guid> HashToGuidV5Async(this Stream data, Guid guidNamespace, CancellationToken cancellationToken = default)
    {
        var namespaceBytes = guidNamespace.ToByteArray();
    
        using var sha1 = SHA1.Create();
        sha1.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
        
        var bufferSize = ByteSize.FromKibiBytes(80); //Default buffer size of 80 KiB similar to Stream.CopyToAsync default buffer size
        using var buffer = MemoryOwner<byte>.Allocate((int)bufferSize.GetBytes(), AllocationMode.Default);
        
        
        int bytesRead;
        while ((bytesRead = await data.ReadAsync(buffer.Memory, cancellationToken)) > 0)
        {
            sha1.TransformBlock(buffer.DangerousGetArray().Array!, 0, bytesRead, null, 0);
        }
        sha1.TransformFinalBlock([], 0, 0);
    
        var hash = sha1.Hash!;
        hash[6] = (byte)((hash[6] & 0x0f) | 0x50); // Version 5
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80); // Variant 10xx
    
        return new(hash.AsSpan()[..Unsafe.SizeOf<Guid>()]);
    }
}

public static class IdentifiableExtensions
{
    private static readonly Guid PathNamespace = "gamedotnet.core.path".HashToGuidV5(Guid.NameSpaceDns);
    
    public static Identifiable ToIdentifiable(this string name, Guid guidNamespace)
    {
        var guid = name.HashToGuidV5(guidNamespace);
        return Identifiable.From(guid);
    }
    
    public static Identifiable ToIdentifiable(this AbsolutePath path)
    {
        var guid = path.Value.HashToGuidV5(PathNamespace);
        return Identifiable.From(guid);
    }

    public static Identifiable ToIdentifiable(this LocalPath path) =>
        path.ResolveToCurrentDirectory().ToIdentifiable();


}