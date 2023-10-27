using Silk.NET.Core.Native;
using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct ReflectInterfaceVariable
{
	public uint SpirvId;
	public string Name;
	public uint Location;
	public StorageClass StorageClass;
	public string Semantic;
	public DecorationFlagBits DecorationFlags;
	public BuiltIn BuiltIn;
	public ReflectNumericTraits Numeric;
	public ReflectArrayTraits Array;
	public ReflectInterfaceVariable[] Members;
	public Format Format;
	public ReflectTypeDescription TypeDescription;

	public override string ToString()
	{
		return "ReflectInterfaceVariable {" + Name + "} [" + Members.Length + "]";
	}

	internal static unsafe ReflectInterfaceVariable[] ToManaged(InterfaceVariable** input_vars, uint var_count)
	{
		var intf_vars = new ReflectInterfaceVariable[var_count];

		for (var i = 0; i < var_count; i++)
		{
			var interfaceVarNative = input_vars[i];
			var intf = *interfaceVarNative;
			var variable = new ReflectInterfaceVariable();

			PopulateReflectInterfaceVariable(ref intf, ref variable);
			variable.Members = ToManagedArray(intf.Members, intf.MemberCount);

			intf_vars[i] = variable;
		}

		return intf_vars;
	}

	private static unsafe ReflectInterfaceVariable[] ToManagedArray(InterfaceVariable* input_vars, uint var_count)
	{
		var intf_vars = new ReflectInterfaceVariable[var_count];

		for (var i = 0; i < var_count; i++)
		{
			var intf = input_vars[i];
			var variable = new ReflectInterfaceVariable();

			PopulateReflectInterfaceVariable(ref intf, ref variable);
			variable.Members = ToManagedArray(intf.Members, intf.MemberCount);

			intf_vars[i] = variable;
		}

		return intf_vars;
	}

	internal static unsafe void PopulateReflectInterfaceVariable (
		ref InterfaceVariable intf,
		ref ReflectInterfaceVariable variable)
	{
		variable.SpirvId = intf.SpirvId;
		variable.Name = SilkMarshal.PtrToString((nint)intf.Name)!;
		variable.Location = intf.Location;
		variable.StorageClass = intf.StorageClass;
		variable.Semantic = SilkMarshal.PtrToString((nint)intf.Semantic)!;
		variable.DecorationFlags = (DecorationFlagBits)intf.DecorationFlags;
		variable.BuiltIn = intf.BuiltIn;
		variable.Format = intf.Format;
		variable.TypeDescription = ReflectTypeDescription.GetManaged(ref *intf.TypeDescription);
		variable.Array = new(intf.Array);
		variable.Numeric = new(intf.Numeric);
	}
}