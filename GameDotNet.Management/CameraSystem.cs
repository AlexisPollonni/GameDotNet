using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.Physics.Components;
using GameDotNet.Management.ECS;
using GameDotNet.Management.ECS.Components;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Query = GameDotNet.Management.ECS.Query;

namespace GameDotNet.Management;

// Tag component for cameras
public struct Camera
{ }

public sealed class CameraSystem : SystemBase, IDisposable
{
    public float Acceleration = 50;
    public float AccSprintMultiplier = 4;
    public float LookSensitivity = 1f;
    public float DampingCoefficient = 5;

    private readonly IView _view;
    private IInputContext? _input;
    private EntityReference _camera;
    private IKeyboard _keyboard = null!;
    private IMouse? _mouse;
    private bool _isFocused;

    private Vector3 _velocity;
    private float _yaw, _pitch;
    private Vector2 _lastMousePos;

    public CameraSystem(IView view) : base(Query.All(typeof(Translation), typeof(Camera)))
    {
        _view = view;
        _view.FocusChanged += ChangeFocusState;
    }

    public override ValueTask<bool> Initialize()
    {
        _input = _view.CreateInput();
        _keyboard = _input.Keyboards[0];
        _mouse = _input.Mice[0];
        _keyboard.KeyUp += (_, key, _) =>
        {
            if (key is not Key.Escape) return;

            ChangeFocusState(false);
        };

        _lastMousePos = _mouse.Position;


        Matrix4x4.Decompose(Matrix4x4.CreateLookAt(new(0, 0, -3), Vector3.Zero, -Vector3.UnitY),
                            out _, out var rot, out var pos);

        _camera = ParentWorld.Create(new Tag("Camera"),
                                     new Camera(),
                                     new Translation(pos),
                                     new Rotation(rot))
                             .Reference();

        return ValueTask.FromResult(true);
    }

    public override void Update(TimeSpan delta)
    {
        if (_input is null) return;

        ref var camPos = ref _camera.Entity.Get<Translation>();

        if (_isFocused)
            UpdateInput(delta);
        else if (_mouse.IsButtonPressed(MouseButton.Left))
            ChangeFocusState(true);

        _velocity = Vector3.Lerp(_velocity, Vector3.Zero, (float)(DampingCoefficient * delta.TotalSeconds));
        camPos.Value += _velocity * (float)delta.TotalSeconds;
    }

    public void Dispose()
    {
        _input?.Dispose();
    }

    private void UpdateInput(TimeSpan delta)
    {
        var mousePos = _mouse.Position;
        var winSize = new Vector2(_view.FramebufferSize.X, _view.FramebufferSize.Y);

        var mouseDeltaPixels = mousePos - _lastMousePos;
        _lastMousePos = _mouse.Position;

        var mouseDelta = LookSensitivity * mouseDeltaPixels / winSize;
        var mouseDeltaRad = -(mouseDelta * MathF.PI);

        _yaw += mouseDeltaRad.X;
        _pitch = Math.Clamp(_pitch + mouseDeltaRad.Y, -MathF.PI / 2, MathF.PI / 2);

        var finalRot = Quaternion.CreateFromYawPitchRoll(_yaw, _pitch, 0);
        _camera.Entity.Get<Rotation>().Value = finalRot;

        _velocity += Vector3.Transform(GetAccelerationVector() * (float)delta.TotalSeconds, finalRot);
    }

    private Vector3 GetAccelerationVector()
    {
        var moveInput = new Vector3();

        void AddMovement(Key key, Vector3 dir)
        {
            if (_keyboard.IsKeyPressed(key))
                moveInput += dir;
        }

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

        if (_keyboard.IsKeyPressed(Key.ShiftLeft))
            return direction * (Acceleration * AccSprintMultiplier);

        return direction * Acceleration;
    }

    private void ChangeFocusState(bool focused)
    {
        if (_mouse is not null)
        {
            _mouse.Cursor.CursorMode = _isFocused switch
            {
                true when !focused => CursorMode.Normal,
                false when focused => CursorMode.Raw,
                _ => _mouse.Cursor.CursorMode
            };
        }

        _isFocused = focused;
    }
}