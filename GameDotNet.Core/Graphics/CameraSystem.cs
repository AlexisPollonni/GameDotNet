using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GameDotNet.Core.ECS;
using GameDotNet.Core.ECS.Components;
using GameDotNet.Core.Physics.Components;
using Serilog;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Query = GameDotNet.Core.ECS.Query;

namespace GameDotNet.Core.Graphics;

// Tag component for cameras
public struct Camera
{ }

public sealed class CameraSystem : SystemBase, IDisposable
{
    public float Acceleration = 50;
    public float AccSprintMultiplier = 4;
    public float LookSensitivity = 1;
    public float DampingCoefficient = 5;

    private readonly IView _view;
    private IInputContext? _input;
    private EntityReference _camera;
    private IKeyboard _keyboard = null!;
    private IMouse _mouse = null!;

    private Vector3 _velocity = default;
    private Vector2 _lastMousePos;

    public CameraSystem(IView view) : base(Query.All(typeof(Translation), typeof(Camera)))
    {
        _view = view;
    }

    public override bool Initialize()
    {
        _input = _view.CreateInput();
        _keyboard = _input.Keyboards[0];
        _mouse = _input.Mice[0];

        _lastMousePos = _mouse.Position;


        Matrix4x4.Decompose(Matrix4x4.CreateLookAt(new(0, 0, -3), Vector3.Zero, -Vector3.UnitY),
                            out _, out var rot, out var pos);

        _camera = ParentWorld.Create(new Tag("Camera"),
                                     new Camera(),
                                     new Translation(pos),
                                     new Rotation(rot))
                             .Reference();

        return true;
    }

    public override void Update(TimeSpan delta)
    {
        if (_input is null) return;

        ref var camPos = ref _camera.Entity.Get<Translation>();

        UpdateInput(delta);

        _velocity = Vector3.Lerp(_velocity, Vector3.Zero, (float)(DampingCoefficient * delta.TotalSeconds));
        camPos.Value += _velocity * (float)delta.TotalSeconds;

        Log.Verbose("Camera position update : {Position}, {Rotation}", camPos.Value,
                    _camera.Entity.Get<Rotation>().Value);
    }

    public void Dispose()
    {
        _input?.Dispose();
    }

    private void UpdateInput(TimeSpan delta)
    {
        _velocity += GetAccelerationVector() * (float)delta.TotalSeconds;

        var mouseDeltaPixels = _mouse.Position - _lastMousePos;
        var mouseDelta = LookSensitivity * new Vector2(mouseDeltaPixels.X / _view.FramebufferSize.X,
                                                       mouseDeltaPixels.Y / _view.FramebufferSize.Y);

        ref var rotation = ref _camera.Entity.Get<Rotation>();

        var horiz = Quaternion.CreateFromAxisAngle(Vector3.UnitY, mouseDelta.X);
        var vert = Quaternion.CreateFromAxisAngle(Vector3.UnitX, mouseDelta.Y);

        rotation.Value = horiz * rotation.Value * vert;
    }

    private Vector3 GetAccelerationVector()
    {
        var moveInput = new Vector3();

        void AddMovement(Key key, Vector3 dir)
        {
            if (_keyboard.IsKeyPressed(key))
                moveInput += dir;
        }

        AddMovement(Key.Z, Vector3.UnitZ);
        AddMovement(Key.S, -Vector3.UnitZ);
        AddMovement(Key.D, Vector3.UnitX);
        AddMovement(Key.A, -Vector3.UnitX);
        AddMovement(Key.Space, -Vector3.UnitY);
        AddMovement(Key.Space, Vector3.UnitY);

        if (moveInput == Vector3.Zero)
            return Vector3.Zero;

        var direction = Vector3.Normalize(moveInput);

        if (_keyboard.IsKeyPressed(Key.ShiftLeft))
            return direction * (Acceleration * AccSprintMultiplier);

        return direction * Acceleration;
    }
}