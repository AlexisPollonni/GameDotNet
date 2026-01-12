using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using AutoCtor;
using GameDotNet.Core.Abstractions;
using GameDotNet.Core.Components;
using GameDotNet.Core.Models;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Services;
using GameDotNet.Core.Tooling;
using GameDotNet.Core.Tooling.Extensions;
using Nito.Disposables;
using ZLinq;
using Key = GameDotNet.Core.Models.Key;

namespace GameDotNet.Graphics.Services;

// Tag component for cameras
public readonly record struct CameraOptions(
    float FieldOfView = 70,
    float NearPlaneDistance = 0.1f,
    float FarPlaneDistance = 5000f,
    float Acceleration = 50,
    float AccSprintMultiplier = 4,
    float LookSensitivity = 1,
    float DampingCoefficient = 5) : ISceneComponent
{
    public CameraOptions() : this(70f) { }
}

/// <summary>
/// Tag component for camera entities
/// </summary>
public readonly record struct Camera : ISceneComponent;

public readonly record struct CameraRuntimeData(
    IViewPort? AttachedViewPort,
    Vector3 Velocity,
    float Yaw,
    float Pitch,
    Vector2 LastMousePosition) : ISceneComponent;

[AutoConstruct]
[RegisterSingleton<IQueryUpdateJob>(Duplicate = DuplicateStrategy.Append)]
public sealed partial class CameraManager : SingleAsyncDisposable<EmptyStruct>, IQueryUpdateJob
{
    public bool IsStarted { get; set; }

    public JobConfiguration Options { get; } = new();

    public QueryDescription Query { get; } = new QueryDescription().WithAll<Camera>();


    private readonly CancellationTokenSource _cts = new();
    private readonly SceneInstanceManager _sceneManager;
    
    private static readonly CameraOptions DefaultCamOptions = new();
    
    [AutoPostConstruct]
    private void Configure(IEventRegistry registry)
    {
        registry.On<ViewportActiveChangedEvent>(OnViewportActiveChanged, _cts.Token);
    }

    protected override async ValueTask DisposeAsync(EmptyStruct context)
    {
        await _cts.CancelAsync();
        _cts.Dispose();
    }

    private async Task OnViewportActiveChanged(IAsyncEnumerable<ViewportActiveChangedEvent> evt, CancellationToken token)
    {
        await foreach (var viewPort in evt.Select(e => e.Current).Distinct().WithCancellation(token))
        {
            var worlds = _sceneManager.EnabledScenes
                .AsValueEnumerable()
                .Select(instance => instance.EntityWorld);
                
            var cameras = worlds.SelectMany(world => world.QueryEnumerable(Query)); ;

            if (!worlds.SelectMany(world => world.QueryEnumerable(Query.WithAll<CameraRuntimeData>()))
                    .Any(entity => entity.Get<CameraRuntimeData>().AttachedViewPort == viewPort)) { }

            foreach (var camera in cameras)
            {
                if (!camera.TryGet<CameraRuntimeData>(out var camData))
                {
                    camData = new(viewPort, Vector3.Zero, 0, 0, viewPort.Input.MousePosition);
                }

                if (camData.AttachedViewPort != viewPort)
                {
                    continue;
                }

                camera.Set(camData);
            }
        }
    }

    private Entity GetOrCreateMainCameraEntity(World world)
    {
        var mainCam = world.QueryEnumerable(Query).FirstOrDefault(entity => entity.Label == "MainCamera", Entity.Null);

        if (mainCam != Entity.Null)
        {
            return mainCam;
        }

        var lookAtMatrix = Matrix4x4.CreateLookAt(new(0, 0, -3), Vector3.Zero, -Vector3.UnitY);
        Matrix4x4.Decompose(lookAtMatrix, out _, out var rot, out var pos);

        return world.Create(Label.From("MainCamera"),
                            DefaultCamOptions,
                            new Camera(),
                            Translation.From(pos),
                            Rotation.From(rot));
    }

    private void UpdateInput(TimeSpan delta, Entity camera, in CameraRuntimeData runtimeData)
    {
        if (runtimeData.AttachedViewPort is null) return;
        if (!runtimeData.AttachedViewPort.IsActive) return;

        var view = runtimeData.AttachedViewPort;
        var input = view.Input;
        var camOptions = camera.Get<CameraOptions>();

        // Calculate mouse delta in radians
        var mouseDeltaPixels = input.MousePosition - runtimeData.LastMousePosition;
        var mouseDeltaRad = -(camOptions.LookSensitivity * mouseDeltaPixels /
            new Vector2(view.Size.Width, view.Size.Height) * MathF.PI);

        // Update rotation
        var yaw = runtimeData.Yaw + mouseDeltaRad.X;
        var pitch = Math.Clamp(runtimeData.Pitch + mouseDeltaRad.Y, -MathF.PI / 2, MathF.PI / 2);
        var finalRot = Quaternion.CreateFromYawPitchRoll(yaw, pitch, 0);

        camera.Set<Rotation>(finalRot);

        // Update velocity and save runtime data
        var acceleration
            = Vector3.Transform(GetAccelerationVector(in camOptions, input) * (float)delta.TotalSeconds, finalRot);
        camera.Set(runtimeData with
        {
            LastMousePosition = input.MousePosition,
            Yaw = yaw,
            Pitch = pitch,
            Velocity = runtimeData.Velocity + acceleration
        });
    }

    private static Vector3 GetAccelerationVector(in CameraOptions camData, IInputContext input)
    {
        var moveInput = new Vector3();

        AddMovement(Key.Z, -Vector3.UnitZ);
        AddMovement(Key.W, -Vector3.UnitZ);

        AddMovement(Key.S, Vector3.UnitZ);

        AddMovement(Key.D, Vector3.UnitX);

        AddMovement(Key.A, -Vector3.UnitX);
        AddMovement(Key.Q, -Vector3.UnitX);

        AddMovement(Key.Space, Vector3.UnitY);
        AddMovement(Key.ControlLeft, -Vector3.UnitY);

        if (moveInput == Vector3.Zero) return Vector3.Zero;

        var direction = Vector3.Normalize(moveInput);

        if (input.IsKeyDown(Key.ShiftLeft)) return direction * (camData.Acceleration * camData.AccSprintMultiplier);

        return direction * camData.Acceleration;

        void AddMovement(Key key, Vector3 dir)
        {
            if (input.IsKeyDown(key)) moveInput += dir;
        }
    }

    public ValueTask OnUpdate(TimeSpan deltaTime, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public void OnUpdateQueryEntities(TimeSpan deltaTime, ReadOnlySpan<Entity> entities)
    {
        foreach (var entity in entities)
        {
            if(!entity.TryGet<CameraOptions>(out var camData)) entity.Set(DefaultCamOptions);
            if (!entity.TryGet<CameraRuntimeData>(out var runtimeData)) continue;

            UpdateInput(deltaTime, entity, in runtimeData);

            runtimeData = runtimeData with
            {
                Velocity = Vector3.Lerp(runtimeData.Velocity,
                                        Vector3.Zero,
                                        (float)(camData.DampingCoefficient * deltaTime.TotalSeconds))
            };
            
            
            entity.Set(runtimeData);
            entity.Set<Translation>(entity.Get<Translation>().Value + runtimeData.Velocity * (float)deltaTime.TotalSeconds);
        }
    }
}