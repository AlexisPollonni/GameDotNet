using GameDotNet.Graphics.ILGPU.Models;
using ILGPU;
using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Abstractions;

/// <summary>
/// Handles the resource management side of the frame graph. Buffer handles are used to define dependencies between passes.
/// The graph handles memory allocation and aliases between passes.
/// </summary>
public interface IFrameGraphBuilder
{
    /// <summary>
    /// Creates a new logical resource in the graph
    /// </summary>
    /// <param name="extent">The extent (size) of the buffer</param>
    /// <param name="stride">The stride of the buffer</param>
    /// <param name="arrayViewFactory">A user provided factory to convert the frame graph handled buffer to the ArrayView returned by GetView at execution time</param>
    /// <param name="handle">The created buffer handle</param>
    /// <typeparam name="T">The type of elements in the buffer</typeparam>
    /// <typeparam name="TIndex">The type of the extent index</typeparam>
    /// <typeparam name="TStride">The type of the stride</typeparam>
    /// <typeparam name="TArrayView">The type of the array view</typeparam>
    /// <returns>This builder for fluent chaining</returns>
    IFrameGraphBuilder CreateBuffer<T, TIndex, TStride, TArrayView>(
        TIndex extent,
        TStride stride,
        Func<MemoryBuffer, TArrayView> arrayViewFactory,
        out BufferHandle<T, TIndex, TStride> handle
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : struct, IStridedArrayView<T, TIndex, TStride>;

    /// <summary>
    /// Registers that a pass reads from a handle (builds the DAG edges)
    /// </summary>
    /// <param name="handle">Buffer handle we created with CreateBuffer</param>
    /// <typeparam name="T">The type of elements in the buffer</typeparam>
    /// <typeparam name="TIndex">The type of the extent index</typeparam>
    /// <typeparam name="TStride">The type of the stride</typeparam>
    /// <returns>This builder for fluent chaining</returns>
    IFrameGraphBuilder Read<T, TIndex, TStride>(BufferHandle<T, TIndex, TStride> handle)
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>;

    /// <summary>
    /// Registers that a pass writes to a handle
    /// </summary>
    /// <param name="handle">Buffer handle we created with CreateBuffer</param>
    /// <typeparam name="T">The type of elements in the buffer</typeparam>
    /// <typeparam name="TIndex">The type of the extent index</typeparam>
    /// <typeparam name="TStride">The type of the stride</typeparam>
    /// <returns>This builder for fluent chaining</returns>
    IFrameGraphBuilder Write<T, TIndex, TStride>(BufferHandle<T, TIndex, TStride> handle)
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>;

    /// <summary>
    /// Registers an array view allocated outside the frame graph to use in passes
    /// </summary>
    /// <param name="physicalView">An externally allocated array view</param>
    /// <param name="handle">The created buffer handle that points the that array view</param>
    /// <typeparam name="T">The type of elements in the buffer</typeparam>
    /// <typeparam name="TIndex">The type of the extent index</typeparam>
    /// <typeparam name="TStride">The type of the stride</typeparam>
    /// <typeparam name="TArrayView">Type of the array view to import</typeparam>
    /// <returns>This builder for fluent chaining</returns>
    IFrameGraphBuilder ImportExternalView<T, TIndex, TStride, TArrayView>(
        TArrayView physicalView,
        out BufferHandle<T, TIndex, TStride> handle
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : IStridedArrayView<T, TIndex, TStride>;
}
