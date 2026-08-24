using GameDotNet.Graphics.ILGPU.Models;
using ILGPU;

namespace GameDotNet.Graphics.ILGPU.Abstractions;

public interface IFrameGraphContext
{
    /// <summary>
    /// At execution time, converts the logical handle into a physical ILGPU ArrayView
    /// </summary>
    /// <param name="handle"></param>
    /// <typeparam name="T">Type of elements in the buffer</typeparam>
    /// <typeparam name="TIndex">Type of the extent index</typeparam>
    /// <typeparam name="TStride">Type of the stride</typeparam>
    /// <typeparam name="TArrayView">Type of the array view to return. Should match the type specified in CreateBuffer. If types mismatch an exception will be raised</typeparam>
    /// <returns>Allocated array view handled by the frame graph</returns>
    TArrayView GetView<T, TIndex, TStride, TArrayView>(BufferHandle<T, TIndex, TStride> handle)
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : struct, IStridedArrayView<T, TIndex, TStride>;

    /// <summary>
    /// Allows setting frame graph typed global state. Is persistent across executions and resets.
    /// Can be used to fetch global state in passes set before execution or outside the frame graph.
    /// Data is keyed on the type. Data with different types can be stored and retrieved simultaneously.
    /// </summary>
    /// <remarks>Not thread safe</remarks>
    /// <typeparam name="TData">Type of the global data to set</typeparam>
    /// <returns>The previously set data</returns>
    // Fetch dynamic, per-frame data without rebuilding the graph
    TData GetGlobalData<TData>()
        where TData : class;
}
