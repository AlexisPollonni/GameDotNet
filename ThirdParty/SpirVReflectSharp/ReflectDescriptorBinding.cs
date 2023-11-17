using Silk.NET.Core.Native;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct ReflectDescriptorBinding
{
	public uint SpirvId;
	public string Name;
	public uint Binding;
	public uint InputAttachmentIndex;
	public uint Set;
	public DescriptorType DescriptorType;
	public ResourceType ResourceType;
	public ReflectImageTraits Image;
	public ReflectBlockVariable Block;
	public ReflectBindingArrayTraits Array;
	public uint Count;
	public uint Accessed;
	public uint UavCounterId;
	// Removed because recursive struct :/
	//public ReflectDescriptorBinding UavCounterBinding;
	public ReflectTypeDescription TypeDescription;

	public DescriptorBinding Native;

	public override string ToString()
	{
		return "ReflectDescriptorBinding {" + Name + "; Type: " + DescriptorType + "}";
	}

	internal unsafe ReflectDescriptorBinding(DescriptorBinding binding)
	{
		Native = binding;

		Set = binding.Set;
		Accessed = binding.Accessed;
		Name = SilkMarshal.PtrToString((nint)binding.Name)!;
		Binding = binding.Binding;
		SpirvId = binding.SpirvId;
		Count = binding.Count;
		ResourceType = binding.ResourceType;
		UavCounterId = binding.UavCounterId;
		InputAttachmentIndex = binding.InputAttachmentIndex;
		Image = new(binding.Image);
		Array = new(binding.Array);
		DescriptorType = binding.DescriptorType;
		Block = new();
		ReflectBlockVariable.PopulateReflectBlockVariable(ref binding.Block, ref Block);
		TypeDescription = ReflectTypeDescription.GetManaged(ref *binding.TypeDescription);

		//UavCounterBinding = new ReflectDescriptorBinding(*binding.uav_counter_binding);
	}
}