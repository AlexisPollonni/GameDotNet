using System.Runtime.InteropServices;
using GameDotNet.Core.Tools.Extensions;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace GameDotNet.Graphics.Vulkan.Bootstrap;

public unsafe struct GenericFeaturesNextNode : IStructuredType
{
    public const int FieldCapacity = 256;

    public StructureType sType = 0;
    public void* pNext = null;

    private fixed uint _fields[FieldCapacity];

    private Span<Bool32> FieldsSpan
    {
        get
        {
            fixed (uint* b = _fields)
                return new(b, FieldCapacity);
        }
    }

    public GenericFeaturesNextNode()
    {
        FieldsSpan.AsBytes().Fill(byte.MaxValue);
    }

    public static GenericFeaturesNextNode FromFeature<T>(ref T feature) where T : unmanaged, IStructuredType
    {
        var node = new GenericFeaturesNextNode();
        var nSpan = node.AsSpan().AsBytes();

        MemoryMarshal.Write(nSpan, in feature);

        return node;
    }

    public StructureType StructureType() => sType;

    public static bool Match(in GenericFeaturesNextNode requested, in GenericFeaturesNextNode supported)
    {
        if (requested.sType != supported.sType)
        {
            throw new ArgumentException("Non-matching sTypes in features nodes !");
        }

        var sRequest = requested.FieldsSpan;
        var sSupport = supported.FieldsSpan;

        for (var i = 0; i < FieldCapacity; i++)
        {
            if (sRequest[i] && !sSupport[i])
            {
                return false;
            }
        }

        return true;
    }
}