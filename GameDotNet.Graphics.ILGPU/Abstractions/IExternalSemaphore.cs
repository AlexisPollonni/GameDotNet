using ILGPU.Runtime;

namespace GameDotNet.Graphics.ILGPU.Abstractions;

/// <summary>
/// A semaphore imported from another graphics API, for now only supports vulkan
/// </summary>
public interface IExternalSemaphore : IAcceleratorObject;
