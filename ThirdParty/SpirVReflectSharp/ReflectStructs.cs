using Silk.NET.SPIRV;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public struct SpirvScalar
{
	public uint Width;
	public uint Signedness;

	internal SpirvScalar(Scalar scalar)
	{
		Width = scalar.Width;
		Signedness = scalar.Signedness;
	}
}

public struct SpirvVector
{
	public uint ComponentCount;

	internal SpirvVector(Vector vector)
	{
		ComponentCount = vector.ComponentCount;
	}
}

public struct SpirvMatrix
{
	public uint ColumnCount;
	public uint RowCount;
	public uint Stride;

	internal SpirvMatrix(Matrix matrix)
	{
		ColumnCount = matrix.ColumnCount;
		RowCount = matrix.RowCount;
		Stride = matrix.Stride;
	}
}

public struct ReflectNumericTraits
{
	public SpirvScalar Scalar;
	public SpirvVector Vector;
	public SpirvMatrix Matrix;

	internal ReflectNumericTraits(NumericTraits numeric)
	{
		Scalar = new(numeric.Scalar);
		Matrix = new(numeric.Matrix);
		Vector = new(numeric.Vector);
	}
}

public struct ReflectArrayTraits
{
	public readonly uint[] Dims;
	public uint Stride;

	internal unsafe ReflectArrayTraits(ArrayTraits array)
	{
		Dims = new uint[array.DimsCount];
		Stride = array.Stride;

		// Populate Dims
		for (var i = 0; i < array.DimsCount; i++)
		{
			Dims[i] = array.Dims[i];
		}
	}
}

public struct ReflectBindingArrayTraits
{
	public readonly uint[] Dims;

	internal unsafe ReflectBindingArrayTraits(BindingArrayTraits array)
	{
		Dims = new uint[array.DimsCount];

		// Populate Dims
		for (var i = 0; i < array.DimsCount; i++)
		{
			Dims[i] = array.Dims[i];
		}
	}
}
	
public struct ReflectImageTraits
{
	public Dim Dim;
	public uint Depth;
	public uint Arrayed;
	public uint MultiSampled;
	public uint Sampled;
	public ImageFormat ImageFormat;

	internal ReflectImageTraits(ImageTraits image)
	{
		Arrayed = image.Arrayed;
		Depth = image.Depth;
		Sampled = image.Sampled;
		MultiSampled = image.Ms;
		Dim = image.Dim;
		ImageFormat = image.ImageFormat;
	}
}

public struct Traits
{
	public ReflectNumericTraits Numeric;
	public ReflectImageTraits Image;
	public ReflectArrayTraits Array;

	internal Traits(Silk.NET.SPIRV.Reflect.Traits traits)
	{
		Array = new(traits.Array);
		Image = new(traits.Image);
		Numeric = new(traits.Numeric);
	}
}

public struct ReflectDescriptorSet
{
	public uint Set;
	public ReflectDescriptorBinding[] Bindings;
}