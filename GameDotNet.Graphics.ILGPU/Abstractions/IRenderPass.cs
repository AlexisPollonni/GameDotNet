using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Abstractions;

/// <summary>
/// In the frame graph, a render pass execute one or more kernels and is scheduled by the frame graph to maximize memory aliasing and parallelism.
/// The setup phase runs before the execute phase, when the graph is compiled.
/// </summary>
/// <typeparam name="TInput"></typeparam>
/// <typeparam name="TOutput"></typeparam>
public interface IRenderPass<in TInput, TOutput>
{
    /// <summary>
    /// Phase 1: Declare dependencies and outputs. Runs once per frame on the CPU.
    /// Implementation should create the TOutput data type, fill it with the output ressources and return it.
    /// </summary>
    /// <param name="builder">Builder used the construct dependencies on resources</param>
    /// <param name="input">User provided input, should contain the passe's dependencies like buffer handles and other data to give to the kernel on execution</param>
    /// <returns>Output as defined by the pass. Helps define at compile time what artifacts the pass will produce. Can contain buffer handles, or other data.</returns>
    TOutput Setup(IFrameGraphBuilder builder, TInput input);

    /// <summary>
    /// Phase 2: Execute the ILGPU kernels. Runs after the graph allocates memory.
    /// </summary>
    /// <param name="accelerator">Accelerator the graph is running on. Use it when launching the kernels.</param>
    /// <param name="stream">Stream this specific pass is scheduled on. Use it when launching the kernels.</param>
    /// <param name="context">Context provided by the frame graph. Use it to exchange buffer handles in input and output with real array views or fetch frame graph global data.</param>
    /// <param name="input">Inputs given at setup time.</param>
    /// <param name="output">Outputs created at setup time.</param>
    void Execute(
        Accelerator accelerator,
        AcceleratorStream stream,
        IFrameGraphContext context,
        TInput input,
        TOutput output
    );
}
