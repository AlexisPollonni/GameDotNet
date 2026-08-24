using ManagedCuda;
using ManagedCuda.BasicTypes;

namespace GameDotNet.Graphics.ILGPU.Tools;

internal static class CudaExtensions
{
    extension(CUResult res)
    {
        public void EnsureOk()
        {
            if (res != CUResult.Success)
            {
                throw new CudaException(res);
            }
        }
    }
}
