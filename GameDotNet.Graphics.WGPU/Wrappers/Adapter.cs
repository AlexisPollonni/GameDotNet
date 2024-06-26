﻿using System.Runtime.InteropServices;
using GameDotNet.Core.Tools.Containers;
using Silk.NET.Core;
using Silk.NET.Core.Native;

namespace GameDotNet.Graphics.WGPU.Wrappers;

public sealed class Adapter : IDisposable
{
    private readonly WebGPU _api;
    internal unsafe Silk.NET.WebGPU.Adapter* Handle { get; private set; } = null;

    internal unsafe Adapter(WebGPU api, Silk.NET.WebGPU.Adapter* handle)
    {
        if (handle is null)
            throw new ResourceCreationError(nameof(Adapter));
            
        _api = api;
        Handle = handle;
    }


    public unsafe FeatureName[] EnumerateFeatures()
    {
        FeatureName features = default;
            
        var size = _api.AdapterEnumerateFeatures(Handle, ref features);

        var featuresSpan = MemoryMarshal.CreateSpan(ref features, (int)size);

        return featuresSpan.ToArray();
    }

    public unsafe bool GetLimits(out SupportedLimits limits)
    {
        limits = new();
            
        return _api.AdapterGetLimits(Handle, ref limits);
    }

    public unsafe void GetProperties(out AdapterProperties properties)
    {
        var sProp = new SAdapterProperties();
        _api.AdapterGetProperties(Handle, ref sProp);

        properties = new(sProp.VendorID,
            SilkMarshal.PtrToString((nint)sProp.VendorName) ?? "",
            SilkMarshal.PtrToString((nint)sProp.Architecture) ?? "",
            sProp.DeviceID,
            SilkMarshal.PtrToString((nint)sProp.Name) ?? "",
            SilkMarshal.PtrToString((nint)sProp.DriverDescription) ?? "",
            sProp.AdapterType,
            sProp.BackendType);
    }

    public unsafe bool HasFeature(FeatureName feature) => _api.AdapterHasFeature(Handle, feature);

    public unsafe void RequestDevice(RequestDeviceCallback callback, string label, FeatureName[] nativeFeatures,
                                     QueueDescriptor defaultQueue = default,
                                     Limits? limits = null, WgpuStructChain.NativeLimits? requiredNativeLimits = null,
                                     DeviceExtras? deviceExtras = null, DeviceLostCallback? deviceLostCallback = null)
    {
        var d = new DisposableList();
        Silk.NET.WebGPU.RequiredLimits requiredLimits = default;
        WgpuStructChain? limitsExtrasChain = null;
        WgpuStructChain? deviceExtrasChain = null;

        if (requiredNativeLimits is not null)
        {
            limitsExtrasChain = new WgpuStructChain()
                .AddRequiredLimitsExtras(requiredNativeLimits.Value);
        }

        if (limits is not null)
        {
            requiredLimits = new()
            {
                NextInChain = requiredNativeLimits is null ? null : limitsExtrasChain!.Ptr,
                Limits = limits.Value
            };
        }

        if (deviceExtras is not null)
            deviceExtrasChain = new WgpuStructChain().AddDeviceExtras(deviceExtras.Value.TracePath);

        var cb1 = new PfnRequestDeviceCallback((s, device, b, _) =>
                                                   callback(s, new(_api, device),
                                                            SilkMarshal.PtrToString((nint)b)!));
        var cbLost = new PfnDeviceLostCallback((reason, b, _) => deviceLostCallback?.Invoke(reason, SilkMarshal.PtrToString((nint)b)!));
        fixed (FeatureName* requiredFeatures = nativeFeatures)
        {
            _api.AdapterRequestDevice(Handle, new DeviceDescriptor
            {
                DefaultQueue = defaultQueue,
                RequiredLimits = limits != null ? &requiredLimits : null,
                RequiredFeatureCount = (uint)nativeFeatures.Length,
                RequiredFeatures = requiredFeatures,
                Label = label.ToPtr(d),
                DeviceLostCallback = cbLost,
                NextInChain = deviceExtras==null ? null : deviceExtrasChain!.Ptr
            },cb1, null);
        }

        limitsExtrasChain?.Dispose();
        deviceExtrasChain?.Dispose();
    }

    public Task<Device> RequestDeviceAsync(string label,
                                           FeatureName[] nativeFeatures,
                                           QueueDescriptor defaultQueue = default,
                                           Limits? limits = null,
                                           WgpuStructChain.NativeLimits? requiredNativeLimits = null,
                                           DeviceExtras? deviceExtras = null,
                                           DeviceLostCallback? deviceLostCallback = null,
                                           CancellationToken token = default)
    {
        var tcs = new TaskCompletionSource<Device>();
        token.ThrowIfCancellationRequested();
        RequestDevice((status, device, message) =>
        {
            token.ThrowIfCancellationRequested();

            if (status is not RequestDeviceStatus.Success)
            {
                tcs.SetException(new PlatformException($"Failed to request WebGPU device : {message}"));
            }
            
            tcs.SetResult(device);
        }, label, nativeFeatures, defaultQueue, limits, requiredNativeLimits, deviceExtras, deviceLostCallback);

        return tcs.Task;
    }

    public unsafe void Dispose() => _api.AdapterRelease(Handle);
}

