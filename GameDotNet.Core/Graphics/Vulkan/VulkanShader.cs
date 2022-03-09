using GameDotNet.Core.Tools.Extensions;
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

    public VulkanShader(Vk vk, VulkanDevice device, ShaderStageFlags stage, byte[] bytecode, string entryPoint = "main")
    {
        _vk = vk;
        _stage = stage;
        _device = device;

        _module = CreateShaderModule(bytecode);
        _entryPointMem = entryPoint.ToGlobalMemory();
    }

    internal unsafe PipelineShaderStageCreateInfo GetPipelineShaderInfo() =>
        new(stage: _stage, module: _module, pName: _entryPointMem.AsPtr<byte>());

    private unsafe ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModule module;
        fixed (byte* pBytecode = code)
        {
            var shaderModuleInfo = new ShaderModuleCreateInfo(codeSize: (nuint?)code.Length, pCode: (uint*)pBytecode);

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
        GC.SuppressFinalize(this);
    }

    ~VulkanShader()
    {
        ReleaseUnmanagedResources();
    }
}