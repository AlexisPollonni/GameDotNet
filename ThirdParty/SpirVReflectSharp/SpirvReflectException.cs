using System;
using System.Runtime.Serialization;
using Silk.NET.SPIRV.Reflect;

namespace SpirvReflectSharp;

public class SpirvReflectException : Exception
{
	public SpirvReflectException()
	{

	}

	public SpirvReflectException(string message)
		: base(message)
	{

	}

	public SpirvReflectException(string message, Exception innerException)
		: base(message, innerException)
	{

	}

	protected SpirvReflectException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{

	}

	internal SpirvReflectException(Result result)
		: base(result.ToString())
	{
	}
}