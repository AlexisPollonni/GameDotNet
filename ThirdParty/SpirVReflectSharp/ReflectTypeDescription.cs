using Silk.NET.Core.Native;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct ReflectTypeDescription
{
	public uint Id;
	public Op Op;
	public string TypeName;
	public string StructMemberName;
	public StorageClass StorageClass;
	public TypeFlagBits TypeFlags;
	public DecorationFlagBits DecorationFlags;
	public Traits Traits;
	public ReflectTypeDescription[] Members;

	public override string ToString()
	{
		return "ReflectTypeDescription {" + StructMemberName + " " + TypeFlags + "} [" + Members.Length + "]";
	}

	internal static unsafe ReflectTypeDescription GetManaged(ref TypeDescription type_description)
	{
		var desc = new ReflectTypeDescription();

		PopulateReflectTypeDescription(ref type_description, ref desc);
		desc.Members = ToManagedArray(type_description.Members, type_description.MemberCount);

		return desc;
	}

	private static unsafe ReflectTypeDescription[] ToManagedArray(TypeDescription* type_description, uint member_count)
	{
		var intf_vars = new ReflectTypeDescription[member_count];

		for (var i = 0; i < member_count; i++)
		{
			var typedesc = type_description[i];
			var variable = new ReflectTypeDescription();

			PopulateReflectTypeDescription(ref typedesc, ref variable);
			variable.Members = ToManagedArray(typedesc.Members, typedesc.MemberCount);

			intf_vars[i] = variable;
		}

		return intf_vars;
	}

	private static unsafe void PopulateReflectTypeDescription(
		ref TypeDescription type_description,
		ref ReflectTypeDescription desc)
	{
		desc.Id = type_description.Id;
		desc.Op = type_description.Op;
		desc.TypeName = SilkMarshal.PtrToString((nint)type_description.TypeName)!;
		desc.StructMemberName = SilkMarshal.PtrToString((nint)type_description.StructMemberName)!;
		desc.StorageClass = type_description.StorageClass;
		desc.TypeFlags = (TypeFlagBits)type_description.TypeFlags;
		desc.DecorationFlags = (DecorationFlagBits)type_description.DecorationFlags;
		desc.Traits = new(type_description.Traits);
	}
}