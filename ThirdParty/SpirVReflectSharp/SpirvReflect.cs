using System;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public unsafe class SpirvReflect
{
    /// <summary>
    /// Creates a <see cref="ShaderModule"/> given SPIR-V bytecode
    /// </summary>
    /// <param name="shaderBytes">Compiled SPIR-V bytecode</param>
    /// <returns>A <see cref="ShaderModule"/></returns>
    public static ShaderModule ReflectCreateShaderModule(ReadOnlySpan<byte> shaderBytes)
    {
        var api = Reflect.GetApi();
        fixed (void* shdrBytecode = shaderBytes)
        {
            ReflectShaderModule module;
            var result =
                api.CreateShaderModule((nuint)shaderBytes.Length, shdrBytecode, &module);

            if (result is Result.Success)
                return new(api, module);

            throw new SpirvReflectException(result);
        }
    }
}