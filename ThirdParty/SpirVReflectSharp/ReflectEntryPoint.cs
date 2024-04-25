using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct ReflectEntryPoint
{
	public string Name;
	public uint Id;
	public ExecutionModel SpirvExecutionModel;
	public ShaderStageFlagBits ShaderStage;
	public ReflectDescriptorSet[] DescriptorSets;
	public uint[] UsedUniforms;
	public uint[] UsedPushConstants;
}