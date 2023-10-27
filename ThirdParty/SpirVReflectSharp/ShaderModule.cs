using System;
using System.Text;
using Silk.NET.Core.Native;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public class ShaderModule : IDisposable
{
	/// <summary>
	/// The compiler that generated this SPIR-V module
	/// </summary>
	public Generator Generator;

	public string EntryPointName;
	public uint EntryPointId;
	public uint EntryPointCount;
	public ReflectEntryPoint[] EntryPoints;

	public SourceLanguage SourceLanguage;
	public uint SourceLanguageVersion;

	public string SourceFile;
	public string SourceSource;

	public ExecutionModel SPIRVExecutionModel;
	public ShaderStageFlagBits ShaderStage;

	public unsafe ReflectInterfaceVariable[] EnumerateInputVariables()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			uint var_count = 0;
			var result = _api.EnumerateInputVariables(inmodule, &var_count, null);

			SpirvReflectUtils.Assert(result is Result.Success);

			var input_vars =
				stackalloc InterfaceVariable*[(int)(var_count * sizeof(InterfaceVariable))];

			result = _api.EnumerateInputVariables(inmodule, &var_count, input_vars);
			SpirvReflectUtils.Assert(result == Result.Success);

			// Convert to managed
			return ReflectInterfaceVariable.ToManaged(input_vars, var_count);
		}
	}
		
	public unsafe ReflectInterfaceVariable[] EnumerateOutputVariables()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			uint var_count = 0;
			var result = _api.EnumerateOutputVariables(inmodule, &var_count, null);

			SpirvReflectUtils.Assert(result is Result.Success);

			var output_vars =
				stackalloc InterfaceVariable*[(int)(var_count * sizeof(InterfaceVariable))];

			result = _api.EnumerateOutputVariables(inmodule, &var_count, output_vars);
			SpirvReflectUtils.Assert(result is Result.Success);

			// Convert to managed
			return ReflectInterfaceVariable.ToManaged(output_vars, var_count);
		}
	}
		
	public unsafe ReflectInterfaceVariable[] EnumerateInterfaceVariables()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			uint var_count = 0;
			var result = _api.EnumerateInterfaceVariables(inmodule, &var_count, null);

			SpirvReflectUtils.Assert(result is Result.Success);

			var interface_vars = stackalloc InterfaceVariable*[(int)(var_count * sizeof(InterfaceVariable))];

			result = _api.EnumerateInterfaceVariables(inmodule, &var_count, interface_vars);
			SpirvReflectUtils.Assert(result is Result.Success);

			// Convert to managed
			return ReflectInterfaceVariable.ToManaged(interface_vars, var_count);
		}
	}

	public unsafe ReflectBlockVariable[] EnumeratePushConstants()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			uint var_count = 0;
			var result = _api.EnumeratePushConstants(inmodule, &var_count, null);

			SpirvReflectUtils.Assert(result is Result.Success);

			var push_consts = stackalloc BlockVariable*[(int)(var_count * sizeof(BlockVariable))];

			result = _api.EnumeratePushConstants(inmodule, &var_count, push_consts);
			SpirvReflectUtils.Assert(result is Result.Success);

			// Convert to managed
			return ReflectBlockVariable.ToManaged(push_consts, var_count);
		}
	}

	public unsafe uint GetCodeSize()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			return _api.GetCodeSize(inmodule);
		}
	}
		
	public unsafe string GetCode()
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			return SilkMarshal.PtrToString((nint)_api.GetCode(inmodule))!;
		}
	}

	public unsafe ReflectInterfaceVariable GetInputVariable(uint location)
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			var reflt = new ReflectInterfaceVariable();
			var result = Result.NotReady;
			var nativeOut = _api.GetInputVariable(inmodule, location, &result);
			SpirvReflectUtils.Assert(result is Result.Success);
			ReflectInterfaceVariable.PopulateReflectInterfaceVariable(ref *nativeOut, ref reflt);
			return reflt;
		}
	}
		
	public unsafe ReflectInterfaceVariable GetInputVariableByLocation(uint location)
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			var reflt = new ReflectInterfaceVariable();
			var result = Result.NotReady;
			var nativeOut = _api.GetInputVariableByLocation(inmodule, location, &result);
			SpirvReflectUtils.Assert(result is Result.Success);
			ReflectInterfaceVariable.PopulateReflectInterfaceVariable(ref *nativeOut, ref reflt);
			return reflt;
		}
	}
		
	public unsafe ReflectInterfaceVariable GetInputVariableBySemantic(string semantic)
	{
		fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
		{
			var reflt = new ReflectInterfaceVariable();
			var result = Result.NotReady;



				var nativeOut = _api.GetInputVariableBySemantic(inmodule, semantic, &result);
				SpirvReflectUtils.Assert(result is Result.Success);
				ReflectInterfaceVariable.PopulateReflectInterfaceVariable(ref *nativeOut, ref reflt);
				return reflt;
			
		}
	}

	#region Unmanaged

	internal unsafe ShaderModule(Reflect api, ReflectShaderModule module)
	{
		_api = api;
		NativeShaderModule = module;

		// Convert to managed
		Generator = module.Generator;
		EntryPointName = SilkMarshal.PtrToString((nint)module.EntryPointName)!;
		EntryPointId = module.EntryPointId;
		SourceLanguage = module.SourceLanguage;
		SourceLanguageVersion = module.SourceLanguageVersion;
		SPIRVExecutionModel = module.SpirvExecutionModel;
		ShaderStage = module.ShaderStage;
		SourceFile = SilkMarshal.PtrToString((nint)module.SourceFile)!;
		SourceSource = SilkMarshal.PtrToString((nint)module.SourceSource)!;

		// Entry point extraction
		EntryPoints = new ReflectEntryPoint[module.EntryPointCount];
		for (var i = 0; i < module.EntryPointCount; i++)
		{
			EntryPoints[i] = new()
			{
				Id = module.EntryPoints[i].Id,
				Name = SilkMarshal.PtrToString((nint)module.EntryPoints[i].Name)!,
				ShaderStage = module.EntryPoints[i].ShaderStage,
				SpirvExecutionModel = module.EntryPoints[i].SpirvExecutionModel,

				UsedPushConstants = new uint[module.EntryPoints[i].UsedPushConstantCount],
				UsedUniforms = new uint[module.EntryPoints[i].UsedUniformCount],

				DescriptorSets = new ReflectDescriptorSet[module.EntryPoints[i].DescriptorSetCount]
			};
			// Enumerate used push constants
			for (var j = 0; j < module.EntryPoints[i].UsedPushConstantCount; j++)
			{
				EntryPoints[i].UsedPushConstants[j] = module.EntryPoints[i].UsedPushConstants[j];
			}
			// Enumerate used uniforms
			for (var j = 0; j < module.EntryPoints[i].UsedUniformCount; j++)
			{
				EntryPoints[i].UsedUniforms[j] = module.EntryPoints[i].UsedUniforms[j];
			}
			// Enumerate descriptor sets
			for (var j = 0; j < module.EntryPoints[i].DescriptorSetCount; j++)
			{
				var desc = module.EntryPoints[i].DescriptorSets[j];
				EntryPoints[i].DescriptorSets[j].Set = desc.Set;
				EntryPoints[i].DescriptorSets[j].Bindings = new ReflectDescriptorBinding[desc.BindingCount];

				for (var k = 0; k < desc.BindingCount; k++)
				{
					EntryPoints[i].DescriptorSets[j].Bindings[k] = new(*desc.Bindings[k]);
				}
			}

		}
	}

	private readonly Reflect _api;

	/// <summary>
	/// The native shader module
	/// </summary>
	public ReflectShaderModule NativeShaderModule;
	public bool Disposed;

	public unsafe void Dispose(bool freeManaged)
	{
		if (!Disposed)
		{
			fixed (ReflectShaderModule* inmodule = &NativeShaderModule)
			{
				_api.DestroyShaderModule(inmodule);
			}
			Disposed = true;
		}
	}

	public void Dispose()
	{
		Dispose(true);

		GC.SuppressFinalize(this);
	}

	~ShaderModule()
	{
		Dispose(false);
	}

	#endregion
}