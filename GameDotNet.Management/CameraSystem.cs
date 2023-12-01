using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Core.Tools.Containers;
using GameDotNet.Core.Tools.Extensions;
using GameDotNet.Graphics;
using GameDotNet.Graphics.Abstractions;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using MessagePipe;
using Silk.NET.Input;
using IInputContext = GameDotNet.Input.Abstract.IInputContext;

namespace GameDotNet.Management;

// Tag component for cameras
public record struct Camera(
    float FieldOfView = 70,
    float NearPlaneDistance = 0.1f,
    float FarPlaneDistance = 5000f,
    float Acceleration = 50,
    float AccSprintMultiplier = 4,
    float LookSensitivity = 1,
    float DampingCoefficient = 5)
{
    public Camera() : this(70f)
    { }
}

public sealed class CameraSystem : SystemBase, IDisposable
{
    private readonly NativeViewManager _viewManager;
    private readonly DisposableList _disposables;
    private INativeView _view = null!;
    private IInputContext _input = null!;
    private EntityReference _camera;
    private bool _isFocused;

    private Vector3 _velocity;
    private float _yaw, _pitch;
    private Vector2 _lastMousePos;

    public CameraSystem(Universe universe, NativeViewManager viewManager) : base(universe, new(1, false))
    {
        _viewManager = viewManager;
        _disposables = new();
    }

    public override ValueTask<bool> Initialize(CancellationToken token = default)
    {
        _view = _viewManager.MainView ?? throw new NullReferenceException();
        _input = _view.Input;

        _view.FocusChanged.Subscribe(ChangeFocusState).DisposeWith(_disposables);
        _input.KeyUp.Subscribe(e =>
        {
            if (e.Key is not Key.Escape) return;

            ChangeFocusState(false);
        }).DisposeWith(_disposables);
        _lastMousePos = _input.MousePosition;

        Matrix4x4.Decompose(Matrix4x4.CreateLookAt(new(0, 0, -3), Vector3.Zero, -Vector3.UnitY),
                            out _, out var rot, out var pos);

        _camera = Universe.World.Create(new Tag("Camera"),
                                        new Camera(),
                                        new Translation(pos),
                                        new Rotation(rot))
                          .Reference();
        
        return ValueTask.FromResult(true);
    }

    public override void Update(TimeSpan delta)
    {
        ref readonly var camData = ref _camera.Entity.Get<Camera>();
        ref var camPos = ref _camera.Entity.Get<Translation>();

        if (_isFocused)
            UpdateInput(delta, in camData);
        else if (_input.IsButtonDown(MouseButton.Left))
            ChangeFocusState(true);

        _velocity = Vector3.Lerp(_velocity, Vector3.Zero, (float)(camData.DampingCoefficient * delta.TotalSeconds));
        camPos.Value += _velocity * (float)delta.TotalSeconds;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private void UpdateInput(TimeSpan delta, in Camera camData)
    {
        var mousePos = _input.MousePosition;
        var winSize = new Vector2(_view.Size.Width, _view.Size.Height);

        var mouseDeltaPixels = mousePos - _lastMousePos;
        _lastMousePos = _input.MousePosition;

        var mouseDelta = camData.LookSensitivity * mouseDeltaPixels / winSize;
        var mouseDeltaRad = -(mouseDelta * MathF.PI);

        _yaw += mouseDeltaRad.X;
        _pitch = Math.Clamp(_pitch + mouseDeltaRad.Y, -MathF.PI / 2, MathF.PI / 2);

        var finalRot = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0);
        _camera.Entity.Get<Rotation>().Value = finalRot;

        _velocity += Vector3.Transform(GetAccelerationVector(in camData) * (float)delta.TotalSeconds, finalRot);
    }

    private Vector3 GetAccelerationVector(in Camera camData)
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

        if (moveInput == Vector3.Zero)
            return Vector3.Zero;

        var direction = Vector3.Normalize(moveInput);

        if (_input.IsKeyDown(Key.ShiftLeft))
            return direction * (camData.Acceleration * camData.AccSprintMultiplier);

        return direction * camData.Acceleration;

        void AddMovement(Key key, Vector3 dir)
        {
            if (_input.IsKeyDown(key))
                moveInput += dir;
        }
    }

    private void ChangeFocusState(bool focused)
    {
        _input.CursorHidden = focused;
        _input.CursorRestricted = focused;

        _isFocused = focused;
    }
}