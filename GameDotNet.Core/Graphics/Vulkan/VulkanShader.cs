using GameDotNet.Core.Tools.Extensions;
using Microsoft.Toolkit.HighPerformance;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace GameDotNet.Core.Graphics.Vulkan;

public sealed class VulkanShader : IDisposable
{
    private readonly Vk _vk;
    private readonly VulkanDevice _device;

    private readonly GlobalMemory _entryPointMem;
    private readonly ShaderStageFlags _stage;
    private readonly ShaderModule _module;

    public VulkanShader(Vk vk, VulkanDevice device, ShaderStageFlags stage, ReadOnlySpan<byte> bytecode,
                        string entryPoint = "main")
    {
        _vk = vk;
        _stage = stage;
        _device = device;

        _module = CreateShaderModule(bytecode);
        _entryPointMem = entryPoint.ToGlobalMemory();
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
        _entryPointMem.Dispose();
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();

        GC.SuppressFinalize(this);
    }

    ~VulkanShader()
    {
        ReleaseUnmanagedResources();
    }
}