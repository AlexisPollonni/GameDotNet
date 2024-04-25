using Silk.NET.Core.Native;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct ReflectBlockVariable
{
	public uint SpirvId;
	public string Name;
	public uint Offset;
	public uint AbsoluteOffset;
	public uint Size;
	public uint PaddedSize;
	public DecorationFlagBits DecorationFlags;
	public ReflectNumericTraits Numeric;
	public ReflectArrayTraits Array;
	public VariableFlagBits Flags;
	public ReflectBlockVariable[] Members;
	public ReflectTypeDescription TypeDescription;

	public override string ToString()
	{
		return "ReflectBlockVariable {" + Name + "} [" + Members.Length + "]";
	}

	internal static unsafe ReflectBlockVariable[] ToManaged(BlockVariable** push_consts, uint var_count)
	{
		var blockVars = new ReflectBlockVariable[var_count];

		for (var i = 0; i < var_count; i++)
		{
			var blockVarNative = push_consts[i];
			var block = *blockVarNative;
			var variable = new ReflectBlockVariable();

			PopulateReflectBlockVariable(ref block, ref variable);
			variable.Members = ToManagedArray(block.Members, block.MemberCount);

			blockVars[i] = variable;
		}

		return blockVars;
	}

	private static unsafe ReflectBlockVariable[] ToManagedArray(BlockVariable* push_consts, uint var_count)
	{
		var blockVars = new ReflectBlockVariable[var_count];

		for (var i = 0; i < var_count; i++)
		{
			var block = push_consts[i];
			var variable = new ReflectBlockVariable();

			PopulateReflectBlockVariable(ref block, ref variable);
			variable.Members = ToManagedArray(block.Members, block.MemberCount);

			blockVars[i] = variable;
		}

		return blockVars;
	}

	internal static unsafe void PopulateReflectBlockVariable(
		ref BlockVariable block,
		ref ReflectBlockVariable variable)
	{
		variable.SpirvId = block.SpirvId;
		variable.Name = SilkMarshal.PtrToString((nint)block.Name)!;
		variable.Offset = block.Offset;
		variable.AbsoluteOffset = block.AbsoluteOffset;
		variable.Size = block.Size;
		variable.PaddedSize = block.PaddedSize;
		variable.DecorationFlags = (DecorationFlagBits)block.DecorationFlags;
		variable.Flags = (VariableFlagBits)block.Flags;
		if (block.TypeDescription is not null)
		{
			variable.TypeDescription = ReflectTypeDescription.GetManaged(ref *block.TypeDescription);
		}

		variable.Array = new(block.Array);
		variable.Numeric = new(block.Numeric);
	}
}