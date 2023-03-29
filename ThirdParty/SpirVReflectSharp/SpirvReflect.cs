using System;

namespace SpirvReflectSharp;

public unsafe class SpirvReflect
{
	/// <summary>
	/// Creates a <see cref="ShaderModule"/> given SPIR-V bytecode
	/// </summary>
	/// <param name="shaderBytes">Compiled SPIR-V bytecode</param>
	/// <returns>A <see cref="ShaderModule"/></returns>
	public static ShaderModule ReflectCreateShaderModule(ReadOnlySpan<byte> shaderBytes) {
		fixed (void* shdrBytecode = shaderBytes)
		{
			SpirvReflectNative.SpvReflectShaderModule module;
			var result = SpirvReflectNative.spvReflectCreateShaderModule(Convert.ToUInt64(shaderBytes.Length), shdrBytecode, &module);

			if (result == SpirvReflectNative.SpvReflectResult.SPV_REFLECT_RESULT_SUCCESS)
				return new(module);

			throw new SpirvReflectException(result);
		}
	}
}