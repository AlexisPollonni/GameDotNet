using System.Runtime.InteropServices;

namespace GameDotNet.Core.Tools;

//Idea from https://stackoverflow.com/a/53029501
public static class AddressHelper
{
    public static nint GetAddress(object? obj)
    {
        if (obj is null) return 0;

        var r = new ObjectReinterpreter(obj);
        var ptr = r.AsPtrField;

        return ptr;
    }

    public static unsafe void* AsPtr(object? obj) => (void*)GetAddress(obj);

    public static T? AsInstance<T>(nint address)
    {
        if (address is 0) return default;

        var r = new ObjectReinterpreter(address);
        var obj = r.AsObject;

        return (T?)obj;
    }

    [StructLayout(LayoutKind.Explicit)]
    private readonly ref struct ObjectReinterpreter
    {
        [FieldOffset(0)] public readonly nint AsPtrField;
        [FieldOffset(0)] public readonly object? AsObject;

        public ObjectReinterpreter(object obj)
        {
            AsObject = obj;
        }

        public ObjectReinterpreter(nint ptr)
        {
            AsPtrField = ptr;
        }
    }
}