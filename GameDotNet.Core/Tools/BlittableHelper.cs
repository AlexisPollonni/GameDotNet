using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace GameDotNet.Core.Tools;

public static class BlittableHelper
{
    //Avoids allocating handle for every types
    private static GCHandle _gch;

    static BlittableHelper()
    {
        _gch = GCHandle.Alloc(null, GCHandleType.Pinned);
    }

    public static bool IsBlittable<T>()
    {
        return IsBlittableCache<T>.Value;
    }

    private static bool IsBlittable(Type type)
    {
        if (type.IsArray)
        {
            var elem = type.GetElementType();
            return (elem?.IsValueType ?? false) && IsBlittable(elem);
        }

        try
        {
            var instance = FormatterServices.GetUninitializedObject(type);

            _gch.Target = instance; //TODO: Test this construct
            //GCHandle.Alloc(instance, GCHandleType.Pinned).Free();

            return true;
        }
        catch
        {
            return false;
        }
    }

    //From https://stackoverflow.com/a/31485271
    private static class IsBlittableCache<T>
    {
        internal static readonly bool Value = IsBlittable(typeof(T));
    }
}