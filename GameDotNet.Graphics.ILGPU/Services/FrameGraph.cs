using GameDotNet.Core.Tooling;
using GameDotNet.Graphics.ILGPU.Abstractions;
using GameDotNet.Graphics.ILGPU.Models;
using GameDotNet.Graphics.ILGPU.Tools;
using ILGPU;
using ILGPU.Runtime;
using Nito.Disposables;
using QuikGraph;
using QuikGraph.Algorithms;

namespace GameDotNet.Graphics.ILGPU.Services;

[Flags]
internal enum DependencyUsage
{
    None = 0,
    Read = 0b_01,
    Write = 0b_10,
}

// --- 1. Identity & Resource Abstractions ---

internal abstract class LogicalResource;

internal sealed class ImportedResource<TArrayView>(TArrayView view) : LogicalResource
{
    public TArrayView View { get; set; } = view;
}

internal abstract class GraphResourceBase : LogicalResource
{
    public LogicalPassNode? FirstUser { get; set; }
    public LogicalPassNode? LastUser { get; set; }

    public long BufferByteSize { get; protected set; }

    public MemoryBuffer? PhysicalBuffer { get; set; }
}

internal sealed class GraphResource<T, TIndex, TStride, TArrayView> : GraphResourceBase
    where T : unmanaged
    where TStride : struct, IStride<TIndex>
    where TIndex : struct, IGenericIndex<TIndex>
    where TArrayView : struct, IStridedArrayView<T, TIndex, TStride>
{
    public Func<MemoryBuffer, TArrayView> ArrayViewFactory { get; }

    public GraphResource(
        TIndex extent,
        TStride stride,
        Func<MemoryBuffer, TArrayView> arrayViewFactory
    )
    {
        ArrayViewFactory = arrayViewFactory;
        BufferByteSize = stride.To1DStride().ComputeBufferLength(extent.Size);
    }
}

// --- 2. Nodes & Edges ---

internal class ResourceEdge(
    LogicalPassNode source,
    LogicalPassNode target,
    LogicalResource resource,
    DependencyUsage usage
) : IEdge<LogicalPassNode>
{
    public LogicalPassNode Source { get; } = source;
    public LogicalPassNode Target { get; } = target;
    public LogicalResource Resource { get; } = resource;
    public DependencyUsage Usage { get; } = usage;
}

internal abstract class LogicalPassNode
{
    public abstract void Execute(
        IFrameGraphContext context,
        Accelerator accelerator,
        AcceleratorStream stream
    );
}

internal sealed class LogicalPassNode<TInput, TOutput>(
    IRenderPass<TInput, TOutput> pass,
    TInput input
) : LogicalPassNode
{
    public IRenderPass<TInput, TOutput> Pass { get; } = pass;
    public TInput Input { get; } = input;
    public TOutput Output { get; set; } = default!;

    public override void Execute(
        IFrameGraphContext context,
        Accelerator accelerator,
        AcceleratorStream stream
    )
    {
        Pass.Execute(accelerator, stream, context, Input, Output);
    }
}

internal readonly record struct SyncPoint(IExternalSemaphore Semaphore, ulong Offset);

internal class CompiledNode(LogicalPassNode passNode, AcceleratorStream stream)
{
    public LogicalPassNode PassNode { get; } = passNode;
    public AcceleratorStream Stream { get; } = stream;
    public List<SyncPoint> Waits { get; } = [];
    public List<SyncPoint> Signals { get; } = [];

    public void Execute(IFrameGraphContext context, ulong baseTimelineValue)
    {
        foreach (var wait in Waits)
            Stream.WaitTimelineSemaphoreAsync(wait.Semaphore, baseTimelineValue + wait.Offset);

        PassNode.Execute(context, Stream.Accelerator, Stream);

        foreach (var signal in Signals)
            Stream.SignalTimelineSemaphoreAsync(
                signal.Semaphore,
                baseTimelineValue + signal.Offset
            );
    }
}

public sealed class FrameGraph(Accelerator accelerator)
    : SingleDisposable<EmptyStruct>(default),
        IFrameGraphBuilder,
        IFrameGraphContext
{
    private LogicalPassNode? _currentSetupNode;
    private ulong _currentTimelineBase = 0;
    private ulong _timelineIncrementsPerFrame = 0;

    private readonly BufferPool _bufferPool = new(accelerator);

    private readonly BidirectionalGraph<LogicalPassNode, ResourceEdge> _dag = new();
    private readonly List<CompiledNode> _executionPlan = [];

    private readonly Dictionary<LogicalResource, HashSet<LogicalPassNode>> _lastReaders = new();
    private readonly Dictionary<LogicalResource, LogicalPassNode> _lastWriters = new();
    private readonly List<GraphResourceBase> _graphResources = [];
    private readonly List<PooledBuffer> _rentedBuffers = [];
    private readonly List<AcceleratorStream> _streamPool = [];
    private readonly Dictionary<Type, object> _globalData = new();

    public TOutput AddPass<TInput, TOutput>(IRenderPass<TInput, TOutput> pass, TInput input)
    {
        var node = new LogicalPassNode<TInput, TOutput>(pass, input);
        _dag.AddVertex(node);

        _currentSetupNode = node;
        var output = pass.Setup(this, input);
        node.Output = output;

        _currentSetupNode = null;
        return output;
    }

    // --- IFrameGraphBuilder ---

    public IFrameGraphBuilder CreateBuffer<T, TIndex, TStride, TArrayView>(
        TIndex extent,
        TStride stride,
        Func<MemoryBuffer, TArrayView> arrayViewFactory,
        out BufferHandle<T, TIndex, TStride> handle
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : struct, IStridedArrayView<T, TIndex, TStride>
    {
        var resource = new GraphResource<T, TIndex, TStride, TArrayView>(
            extent,
            stride,
            arrayViewFactory
        )
        {
            // Base lifespan is the creator node. It will expand if other nodes read/write it.
            FirstUser = _currentSetupNode,
            LastUser = _currentSetupNode,
        };

        _graphResources.Add(resource);
        handle = new(resource);

        if (_currentSetupNode != null)
            _lastWriters[resource] = _currentSetupNode;

        return this;
    }

    public IFrameGraphBuilder Read<T, TIndex, TStride>(BufferHandle<T, TIndex, TStride> handle)
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
    {
        if (_currentSetupNode == null)
            return this;

        // 1. Track this node as a reader (Prevents WAR hazards for future writers)
        if (!_lastReaders.TryGetValue(handle.Resource, out var readers))
        {
            readers = [];
            _lastReaders[handle.Resource] = readers;
        }
        readers.Add(_currentSetupNode);

        // 2. Establish Read-After-Write (RAW) dependency
        if (
            _lastWriters.TryGetValue(handle.Resource, out var writer)
            && writer != _currentSetupNode
        )
        {
            _dag.AddEdge(new(writer, _currentSetupNode, handle.Resource, DependencyUsage.Read));
        }

        return this;
    }

    public IFrameGraphBuilder Write<T, TIndex, TStride>(BufferHandle<T, TIndex, TStride> handle)
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
    {
        if (_currentSetupNode == null)
            return this;

        // 1. Establish Write-After-Write (WAW) dependency
        if (
            _lastWriters.TryGetValue(handle.Resource, out var previousWriter)
            && previousWriter != _currentSetupNode
        )
        {
            _dag.AddEdge(
                new(previousWriter, _currentSetupNode, handle.Resource, DependencyUsage.Write)
            );
        }

        // 2. Establish Write-After-Read (WAR) dependency
        // We must wait for ALL previous readers to finish before we overwrite this resource!
        if (_lastReaders.TryGetValue(handle.Resource, out var previousReaders))
        {
            foreach (var reader in previousReaders.Where(reader => reader != _currentSetupNode))
            {
                _dag.AddEdge(
                    new(reader, _currentSetupNode, handle.Resource, DependencyUsage.Write)
                );
            }

            // Clear the readers, because this Write establishes a completely new "version" of the data
            previousReaders.Clear();
        }

        _lastWriters[handle.Resource] = _currentSetupNode;
        return this;
    }

    public IFrameGraphBuilder ImportExternalView<T, TIndex, TStride, TArrayView>(
        TArrayView physicalView,
        out BufferHandle<T, TIndex, TStride> handle
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : IStridedArrayView<T, TIndex, TStride>
    {
        var resource = new ImportedResource<TArrayView>(physicalView);
        handle = new(resource);
        return this;
    }

    public void UpdateExternalView<T, TIndex, TStride, TArrayView>(
        BufferHandle<T, TIndex, TStride> handle,
        TArrayView physicalView
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : IStridedArrayView<T, TIndex, TStride>
    {
        if (handle.Resource is ImportedResource<TArrayView> imported)
            imported.View = physicalView;
        else
            throw new InvalidOperationException("Cannot update view of a non-imported resource.");
    }

    // --- IFrameGraphContext ---

    public void SetGlobalData<TData>(TData data)
        where TData : class => _globalData[typeof(TData)] = data;

    public TData GetGlobalData<TData>()
        where TData : class => (TData)_globalData[typeof(TData)];

    public TArrayView GetView<T, TIndex, TStride, TArrayView>(
        BufferHandle<T, TIndex, TStride> handle
    )
        where T : unmanaged
        where TStride : struct, IStride<TIndex>
        where TIndex : struct, IGenericIndex<TIndex>
        where TArrayView : struct, IStridedArrayView<T, TIndex, TStride>
    {
        if (handle.Resource is ImportedResource<TArrayView> imported)
        {
            return imported.View;
        }

        if (handle.Resource is not GraphResource<T, TIndex, TStride, TArrayView> graphRes)
            throw new InvalidOperationException("Invalid resource type in handle.");

        return graphRes.PhysicalBuffer is null
            ? throw new InvalidOperationException(
                "Resource not allocated, did you forget to call Compile()?"
            )
            : graphRes.ArrayViewFactory(graphRes.PhysicalBuffer);
    }

    // --- Compilation & Execution ---

    public void Compile(IExternalSemaphore timelineSemaphore)
    {
        _executionPlan.Clear();
        var sortedNodes = _dag.TopologicalSort().ToList();

        AnalyzeLifespansAndAliasMemory(sortedNodes);

        var nodeToCompiled = new Dictionary<LogicalPassNode, CompiledNode>();
        var streamInherited = new Dictionary<LogicalPassNode, bool>();

        foreach (var node in sortedNodes)
        {
            AcceleratorStream? assignedStream = null;

            foreach (var inEdge in _dag.InEdges(node))
            {
                var parent = inEdge.Source;
                if (streamInherited.GetValueOrDefault(parent))
                    continue;
                assignedStream = nodeToCompiled[parent].Stream;
                streamInherited[parent] = true;
                break;
            }

            assignedStream ??= GetOrAllocateStream();
            var compiledNode = new CompiledNode(node, assignedStream);

            nodeToCompiled[node] = compiledNode;
            _executionPlan.Add(compiledNode);
        }

        ulong localTimelineCounter = 0;
        var timelineOffsets = sortedNodes.ToDictionary(n => n, n => ++localTimelineCounter);

        // Save how many steps this graph takes so we can advance the base value later
        _timelineIncrementsPerFrame = localTimelineCounter;

        foreach (var edge in _dag.Edges)
        {
            var producer = nodeToCompiled[edge.Source];
            var consumer = nodeToCompiled[edge.Target];

            if (producer.Stream == consumer.Stream)
                continue;

            var waitOffset = timelineOffsets[edge.Source];

            consumer.Waits.Add(new(timelineSemaphore, waitOffset));

            if (producer.Signals.All(s => s.Offset != waitOffset))
                producer.Signals.Add(new(timelineSemaphore, waitOffset));
        }
    }

    private void AnalyzeLifespansAndAliasMemory(List<LogicalPassNode> sortedNodes)
    {
        var nodeOrder = sortedNodes
            .Select((node, index) => (node, index))
            .ToDictionary(x => x.node, x => x.index);

        foreach (var edge in _dag.Edges)
        {
            if (edge.Resource is not GraphResourceBase res)
                continue;

            if (res.FirstUser == null || nodeOrder[edge.Source] < nodeOrder[res.FirstUser])
                res.FirstUser = edge.Source;

            if (res.LastUser == null || nodeOrder[edge.Target] > nodeOrder[res.LastUser])
                res.LastUser = edge.Target;
        }

        // key is an allocated buffer, and value is the last pass using the buffer
        var pool = new Dictionary<MemoryBuffer, LogicalPassNode>();

        foreach (
            var res in _graphResources
                .Where(r => r is { FirstUser: not null, LastUser: not null })
                .OrderBy(r => nodeOrder[r.FirstUser!])
        )
        {
            var validBuffer = pool.Where(tuple =>
                {
                    var (memoryBuffer, freeAfterNode) = tuple;
                    return memoryBuffer.LengthInBytes >= res.BufferByteSize
                        && nodeOrder[freeAfterNode] < nodeOrder[res.FirstUser!];
                })
                .Select(tuple => tuple.Key)
                .OrderBy(buffer => buffer.LengthInBytes)
                .FirstOrDefault();

            if (validBuffer is not null)
            {
                res.PhysicalBuffer = validBuffer;
                var previousUser = pool[validBuffer];

                // If we re-use memory from an unrelated pass, we MUST inject a physical DAG edge.
                // Because 'res.FirstUser' occurs strictly after 'previousUser' in nodeOrder,
                // adding this edge will never create a cycle.
                if (previousUser != res.FirstUser)
                {
                    // Injecting this edge ensures the stream scheduler (which runs AFTER this method)
                    // will automatically generate a Timeline Semaphore if they are on different streams.
                    _dag.AddEdge(
                        new ResourceEdge(previousUser, res.FirstUser!, res, DependencyUsage.Write)
                    );
                }

                pool[validBuffer] = res.LastUser!;
            }
            else
            {
                var newBuffer = _bufferPool.Rent(res.BufferByteSize);

                res.PhysicalBuffer = newBuffer.Buffer;
                _rentedBuffers.Add(newBuffer);

                pool[newBuffer.Buffer] = res.LastUser!;
            }
        }
    }

    private AcceleratorStream GetOrAllocateStream()
    {
        if (_streamPool.Count >= 4)
            return _streamPool[_executionPlan.Count % _streamPool.Count];
        var newStream = accelerator.CreateStream();
        _streamPool.Add(newStream);
        return newStream;
    }

    public void Execute()
    {
        foreach (var node in _executionPlan)
        {
            // Pass the rolling base value to calculate absolute values on the fly
            node.Execute(this, _currentTimelineBase);
        }

        _currentTimelineBase += _timelineIncrementsPerFrame;
    }

    /// <summary>
    /// Resets the logical structure of the graph.
    /// </summary>
    public void Reset()
    {
        foreach (var buffer in _rentedBuffers)
            buffer.Dispose();
        _rentedBuffers.Clear();
        _graphResources.Clear();
        _lastWriters.Clear();
        _lastReaders.Clear();
        _dag.Clear();
        _executionPlan.Clear();
        _currentSetupNode = null;
        _timelineIncrementsPerFrame = 0;
    }

    protected override void Dispose(EmptyStruct context)
    {
        Reset();
        _bufferPool.Dispose();
        foreach (var acceleratorStream in _streamPool)
        {
            acceleratorStream.Dispose();
        }
        _streamPool.Clear();
    }
}
