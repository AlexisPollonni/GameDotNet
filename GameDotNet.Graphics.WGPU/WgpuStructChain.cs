using System.Runtime.InteropServices;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using Silk.NET.Core.Native;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace GameDotNet.Graphics.WGPU;

public sealed class WgpuStructChain : IDisposable
{
	private readonly List<GlobalMemory> _pointers = new();
	private readonly DisposableList _trackedAllocatedData = new();

	public unsafe ChainedStruct* Ptr { get; private set; } = null;


	public WgpuStructChain AddPrimitiveDepthClipControl(bool unclippedDepth = default)
	{
		AddStruct(new PrimitiveDepthClipControl
		{
			Chain = new() { SType = SType.PrimitiveDepthClipControl },
			UnclippedDepth = unclippedDepth
		});

		return this;
	}

	public unsafe WgpuStructChain AddShaderModuleSPIRVDescriptor(byte[] code)
	{
		AddStruct(new ShaderModuleSPIRVDescriptor
		{
			Chain = new() { SType = SType.ShaderModuleSpirvDescriptor },
			Code = code.ToGlobalMemory().DisposeWith(_trackedAllocatedData).AsPtr<uint>(),
			CodeSize = (uint)code.Length
		});

		return this;
	}

	public unsafe WgpuStructChain AddShaderModuleWGSLDescriptor(string code)
	{
		AddStruct(new ShaderModuleWGSLDescriptor
		{
			Chain = new() { SType = SType.ShaderModuleWgslDescriptor },
			Code = code.ToPtr(_trackedAllocatedData)
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromAndroidNativeWindow(void* window = default)
	{
		AddStruct(new SurfaceDescriptorFromAndroidNativeWindow
		{
			Chain = new () { SType = SType.SurfaceDescriptorFromAndroidNativeWindow },
			Window = window
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromCanvasHTMLSelector(string selector = "")
	{
		AddStruct(new SurfaceDescriptorFromCanvasHTMLSelector
		{
			Chain = new() { SType = SType.SurfaceDescriptorFromCanvasHtmlSelector },
			Selector = selector.ToPtr(_trackedAllocatedData)
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromMetalLayer(void* layer = default)
	{
		AddStruct(new SurfaceDescriptorFromMetalLayer
		{
			Chain = new() { SType = SType.SurfaceDescriptorFromMetalLayer },
			Layer = layer
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromWaylandSurface(void* display = default, void* surface = default)
	{
		AddStruct(new SurfaceDescriptorFromWaylandSurface
		{
			Chain = new() { SType = SType.SurfaceDescriptorFromWaylandSurface },
			Display = display,
			Surface = surface
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromWindowsHWND(void* hinstance = default, void* hwnd = default)
	{
		AddStruct(new SurfaceDescriptorFromWindowsHWND
		{
			Chain = new () { SType = SType.SurfaceDescriptorFromWindowsHwnd },
			Hinstance = hinstance,
			Hwnd = hwnd
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromXcbWindow(void* connection = default, uint window = default)
	{
		AddStruct(new SurfaceDescriptorFromXcbWindow
		{
			Chain = new() { SType = SType.SurfaceDescriptorFromXcbWindow },
			Connection = connection,
			Window = window
		});

		return this;
	}

	public unsafe WgpuStructChain AddSurfaceDescriptorFromXlibWindow(void* display = default, uint window = default)
	{
		AddStruct(new SurfaceDescriptorFromXlibWindow
		{
			Chain = new() { SType = SType.SurfaceDescriptorFromXlibWindow },
			Display = display,
			Window = window
		});

		return this;
	}

	public unsafe WgpuStructChain AddDeviceExtras(string tracePath = default)
	{
		AddStruct(new DeviceExtras
		{
			Chain = new() { SType = (SType)NativeSType.STypeDeviceExtras },
			TracePath = tracePath.ToPtr(_trackedAllocatedData)
		});

		return this;
	}

	public WgpuStructChain AddRequiredLimitsExtras(uint maxPushConstantSize = default)
	{
		AddStruct(new RequiredLimitsExtras
		{
			Chain = new() { SType = (SType)NativeSType.STypeRequiredLimitsExtras },
			MaxPushConstantSize = maxPushConstantSize
		});

		return this;
	}

	public unsafe WgpuStructChain AddPipelineLayoutExtras(PushConstantRange[] pushConstantRanges)
	{
		AddStruct(new PipelineLayoutExtras
		{
			Chain = new() { SType = (SType)NativeSType.STypePipelineLayoutExtras },
			PushConstantRangeCount = (uint)pushConstantRanges.Length,
			PushConstantRanges = pushConstantRanges.ToGlobalMemory().DisposeWith(_trackedAllocatedData).AsPtr<PushConstantRange>()
		});

		return this;
	}

	private unsafe void AddStruct<T>(T structure)
		where T : struct
	{
		var mem = GlobalMemory.Allocate(Marshal.SizeOf(structure));
			
		if(Ptr is null)
			Ptr = mem.AsPtr<ChainedStruct>();
            
		if(_pointers.Count!=0)
		{
			//write this struct into the "next" field of the last struct
			//this only works because next is guaranteed to be the first field of every ChainedStruct
			Marshal.StructureToPtr(mem.Handle, _pointers[^1], false);
		}
	}

	public unsafe void Dispose()
	{
		foreach (var mem in _pointers)
			mem.Dispose();
            
		_trackedAllocatedData.Dispose();
            
		_pointers.Clear();
	}
}