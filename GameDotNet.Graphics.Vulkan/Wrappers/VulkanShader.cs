using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Graphics.Vulkan.Tools;
using GameDotNet.Graphics.Vulkan.Tools.Extensions;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using SpirvReflectSharp;
using ShaderModule = Silk.NET.Vulkan.ShaderModule;

namespace GameDotNet.Graphics.Vulkan.Wrappers;

public sealed class VulkanShader : IDisposable
{
    public ShaderStageFlags ShaderStage => (ShaderStageFlags)_reflectModule.ShaderStage;

    private readonly Vk _vk;
    private readonly VulkanDevice _device;

    private readonly GlobalMemory _entryPointMem;
    private readonly ShaderStageFlags _stage;
    private readonly ShaderModule _module;
    private readonly SpirvReflectSharp.ShaderModule _reflectModule;

    public VulkanShader(Vk vk, VulkanDevice device, SpirVShader bytecode)
    : this(vk, device, StageToShaderStageFlags(bytecode.Description.Stage), bytecode.Code.AsMemory().AsBytes().Span, bytecode.Description.EntryPoint)
    { }
    
    public VulkanShader(Vk vk, VulkanDevice device, ShaderStageFlags stage, ReadOnlySpan<byte> bytecode,
                        string? entryPoint = null)
    {
        _vk = vk;
        _stage = stage;
        _device = device;

        _module = CreateShaderModule(bytecode);
        _reflectModule = SpirvReflect.ReflectCreateShaderModule(bytecode);

        _entryPointMem = (entryPoint ?? _reflectModule.EntryPointName).ToGlobalMemory();
    }

    public VertexInputDescription GetVertexDescription()
    {
        if (!_reflectModule.ShaderStage.HasFlag(Silk.NET.SPIRV.Reflect.ShaderStageFlagBits.VertexBit))
            throw new InvalidOperationException("Not a vertex shader, can't get vertex description");

        //we will have just 1 vertex buffer binding, with a per-vertex rate
        var bindingDesc = new VertexInputBindingDescription(0, 0, VertexInputRate.Vertex);

        var inputs = _reflectModule.EnumerateInputVariables();

        var attrDescList = inputs
                           .Select(reflVar =>
                                       new VertexInputAttributeDescription(reflVar.Location, bindingDesc.Binding,
                                                                           (Format)reflVar.Format, 0))
                           .OrderBy(desc => desc.Location)
                           .Select(attribute =>
                           {
                               var formatSize = FormatSize(attribute.Format);
                               var attribute2 = attribute with { Offset = bindingDesc.Stride };
                               bindingDesc.Stride += formatSize;
                               return attribute2;
                           })
                           .ToList();

        return new()
        {
            Bindings = new() { bindingDesc },
            Attributes = attrDescList
        };
    }

    public IEnumerable<PushConstantRange> GetPushConstantRanges()
    {
        return _reflectModule.EnumeratePushConstants()
                             .OrderBy(block => block.Offset)
                             .Select(constant => new PushConstantRange(ShaderStage, constant.Offset, constant.Size));
    }

    internal unsafe PipelineShaderStageCreateInfo GetPipelineShaderInfo() =>
        new(stage: _stage, module: _module, pName: _entryPointMem.AsPtr<byte>());

    private unsafe ShaderModule CreateShaderModule(ReadOnlySpan<byte> code)
    {
        ShaderModule module;
        fixed (uint* pBytecode = code.Cast<byte, uint>())
        {
            var shaderModuleInfo = new ShaderModuleCreateInfo(codeSize: (nuint?)code.Length, pCode: pBytecode);

            _vk.CreateShaderModule(_device, shaderModuleInfo, null, out module).ThrowOnError();
        }

        return module;
    }

    private unsafe void ReleaseUnmanagedResources()
    {
        _vk.DestroyShaderModule(_device, _module, null);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();

        _entryPointMem.Dispose();
        _reflectModule.Dispose();

        GC.SuppressFinalize(this);
    }

    ~VulkanShader()
    {
        ReleaseUnmanagedResources();
    }

    private static ShaderStageFlags StageToShaderStageFlags(ShaderStage stage) => stage switch
    {
        Abstractions.ShaderStage.Vertex => ShaderStageFlags.VertexBit,
        Abstractions.ShaderStage.Geometry => ShaderStageFlags.GeometryBit,
        Abstractions.ShaderStage.Fragment => ShaderStageFlags.FragmentBit,
        Abstractions.ShaderStage.Compute => ShaderStageFlags.ComputeBit,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };
    
    /// <summary>
    /// Returns the size in bytes of the provided VkFormat.
    /// As this is only intended for vertex attribute formats, not all VkFormats are
    /// supported.
    /// </summary>
    /// <param name="format"></param>
    /// <returns></returns>
    private static uint FormatSize(Format format)
        => format switch
        {
            Format.Undefined => 0,
            Format.R4G4UnormPack8 => 1,
            Format.R4G4B4A4UnormPack16 => 2,
            Format.B4G4R4A4UnormPack16 => 2,
            Format.R5G6B5UnormPack16 => 2,
            Format.B5G6R5UnormPack16 => 2,
            Format.R5G5B5A1UnormPack16 => 2,
            Format.B5G5R5A1UnormPack16 => 2,
            Format.A1R5G5B5UnormPack16 => 2,
            Format.R8Unorm => 1,
            Format.R8SNorm => 1,
            Format.R8Uscaled => 1,
            Format.R8Sscaled => 1,
            Format.R8Uint => 1,
            Format.R8Sint => 1,
            Format.R8Srgb => 1,
            Format.R8G8Unorm => 2,
            Format.R8G8SNorm => 2,
            Format.R8G8Uscaled => 2,
            Format.R8G8Sscaled => 2,
            Format.R8G8Uint => 2,
            Format.R8G8Sint => 2,
            Format.R8G8Srgb => 2,
            Format.R8G8B8Unorm => 3,
            Format.R8G8B8SNorm => 3,
            Format.R8G8B8Uscaled => 3,
            Format.R8G8B8Sscaled => 3,
            Format.R8G8B8Uint => 3,
            Format.R8G8B8Sint => 3,
            Format.R8G8B8Srgb => 3,
            Format.B8G8R8Unorm => 3,
            Format.B8G8R8SNorm => 3,
            Format.B8G8R8Uscaled => 3,
            Format.B8G8R8Sscaled => 3,
            Format.B8G8R8Uint => 3,
            Format.B8G8R8Sint => 3,
            Format.B8G8R8Srgb => 3,
            Format.R8G8B8A8Unorm => 4,
            Format.R8G8B8A8SNorm => 4,
            Format.R8G8B8A8Uscaled => 4,
            Format.R8G8B8A8Sscaled => 4,
            Format.R8G8B8A8Uint => 4,
            Format.R8G8B8A8Sint => 4,
            Format.R8G8B8A8Srgb => 4,
            Format.B8G8R8A8Unorm => 4,
            Format.B8G8R8A8SNorm => 4,
            Format.B8G8R8A8Uscaled => 4,
            Format.B8G8R8A8Sscaled => 4,
            Format.B8G8R8A8Uint => 4,
            Format.B8G8R8A8Sint => 4,
            Format.B8G8R8A8Srgb => 4,
            Format.A8B8G8R8UnormPack32 => 4,
            Format.A8B8G8R8SNormPack32 => 4,
            Format.A8B8G8R8UscaledPack32 => 4,
            Format.A8B8G8R8SscaledPack32 => 4,
            Format.A8B8G8R8UintPack32 => 4,
            Format.A8B8G8R8SintPack32 => 4,
            Format.A8B8G8R8SrgbPack32 => 4,
            Format.A2R10G10B10UnormPack32 => 4,
            Format.A2R10G10B10SNormPack32 => 4,
            Format.A2R10G10B10UscaledPack32 => 4,
            Format.A2R10G10B10SscaledPack32 => 4,
            Format.A2R10G10B10UintPack32 => 4,
            Format.A2R10G10B10SintPack32 => 4,
            Format.A2B10G10R10UnormPack32 => 4,
            Format.A2B10G10R10SNormPack32 => 4,
            Format.A2B10G10R10UscaledPack32 => 4,
            Format.A2B10G10R10SscaledPack32 => 4,
            Format.A2B10G10R10UintPack32 => 4,
            Format.A2B10G10R10SintPack32 => 4,
            Format.R16Unorm => 2,
            Format.R16SNorm => 2,
            Format.R16Uscaled => 2,
            Format.R16Sscaled => 2,
            Format.R16Uint => 2,
            Format.R16Sint => 2,
            Format.R16Sfloat => 2,
            Format.R16G16Unorm => 4,
            Format.R16G16SNorm => 4,
            Format.R16G16Uscaled => 4,
            Format.R16G16Sscaled => 4,
            Format.R16G16Uint => 4,
            Format.R16G16Sint => 4,
            Format.R16G16Sfloat => 4,
            Format.R16G16B16Unorm => 6,
            Format.R16G16B16SNorm => 6,
            Format.R16G16B16Uscaled => 6,
            Format.R16G16B16Sscaled => 6,
            Format.R16G16B16Uint => 6,
            Format.R16G16B16Sint => 6,
            Format.R16G16B16Sfloat => 6,
            Format.R16G16B16A16Unorm => 8,
            Format.R16G16B16A16SNorm => 8,
            Format.R16G16B16A16Uscaled => 8,
            Format.R16G16B16A16Sscaled => 8,
            Format.R16G16B16A16Uint => 8,
            Format.R16G16B16A16Sint => 8,
            Format.R16G16B16A16Sfloat => 8,
            Format.R32Uint => 4,
            Format.R32Sint => 4,
            Format.R32Sfloat => 4,
            Format.R32G32Uint => 8,
            Format.R32G32Sint => 8,
            Format.R32G32Sfloat => 8,
            Format.R32G32B32Uint => 12,
            Format.R32G32B32Sint => 12,
            Format.R32G32B32Sfloat => 12,
            Format.R32G32B32A32Uint => 16,
            Format.R32G32B32A32Sint => 16,
            Format.R32G32B32A32Sfloat => 16,
            Format.R64Uint => 8,
            Format.R64Sint => 8,
            Format.R64Sfloat => 8,
            Format.R64G64Uint => 16,
            Format.R64G64Sint => 16,
            Format.R64G64Sfloat => 16,
            Format.R64G64B64Uint => 24,
            Format.R64G64B64Sint => 24,
            Format.R64G64B64Sfloat => 24,
            Format.R64G64B64A64Uint => 32,
            Format.R64G64B64A64Sint => 32,
            Format.R64G64B64A64Sfloat => 32,
            Format.B10G11R11UfloatPack32 => 4,
            Format.E5B9G9R9UfloatPack32 => 4,
            _ => 0
        };
}