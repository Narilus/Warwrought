using System;
using Godot;
using Warwrought.Battle.Model;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Presentation-only orthographic BattleLab camera. It owns bounded pan/zoom/reset state and
/// normal keyboard/mouse input, but never receives or writes authoritative battle state.
/// </summary>
public partial class BattleCameraRig : Node3D
{
    public const float DefaultCameraHeight = 30.0f;
    public const float DefaultCameraDistance = 34.0f;
    public const float DefaultCameraAngleDegrees = -48.0f;
    public const float DefaultOrthographicSize = 58.0f;
    public const float MinimumOrthographicSize = 34.0f;
    public const float MaximumOrthographicSize = 88.0f;
    public const float PanStepWorldUnits = 4.0f;

    private Camera3D? _camera;
    private BattlefieldDefinition? _definition;
    private float _defaultSize;
    private Vector3 _defaultRigPosition;
    private Vector3 _defaultCameraPosition;
    private Vector3 _defaultCameraRotationDegrees;

    public int PanOperations { get; private set; }

    public int ZoomOperations { get; private set; }

    public int ResetOperations { get; private set; }

    public int ControlTransitions => PanOperations + ZoomOperations + ResetOperations;

    public bool IsOrthographic => _camera is not null && _camera.Projection == Camera3D.ProjectionType.Orthogonal;

    public float CurrentOrthographicSize => _camera?.Size ?? 0.0f;

    public override void _Ready()
    {
        _camera = GetNode<Camera3D>("Camera3D");
        _defaultRigPosition = Position;
        _defaultCameraPosition = _camera.Position;
        _defaultCameraRotationDegrees = _camera.RotationDegrees;
        _defaultSize = _camera.Size;
        if (_defaultSize <= 0.0f)
        {
            _defaultSize = DefaultOrthographicSize;
            _camera.Size = _defaultSize;
        }
    }

    public void Configure(BattlefieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
        EnsureReady();
        ResetToBattleFraming();
    }

    public void Pan(float horizontal, float depth)
    {
        EnsureReady();
        var next = Position + new Vector3(horizontal, 0.0f, depth);
        if (_definition is not null)
        {
            var bounds = _definition.Bounds;
            var horizontalLimit = (float)(bounds.WidthUnits / BattlefieldPresentationProjector.SimulationUnitsPerWorldMetre * 0.34);
            var depthLimit = (float)(bounds.DepthUnits / BattlefieldPresentationProjector.SimulationUnitsPerWorldMetre * 0.30);
            next.X = Mathf.Clamp(next.X, -horizontalLimit, horizontalLimit);
            next.Z = Mathf.Clamp(next.Z, -depthLimit, depthLimit);
        }

        Position = next;
        PanOperations++;
    }

    public void ZoomBy(float sizeDelta)
    {
        EnsureReady();
        _camera!.Size = Mathf.Clamp(_camera.Size + sizeDelta, MinimumOrthographicSize, MaximumOrthographicSize);
        ZoomOperations++;
    }

    public void ResetToBattleFraming()
    {
        EnsureReady();
        Position = _defaultRigPosition;
        _camera!.Position = _defaultCameraPosition;
        _camera.RotationDegrees = _defaultCameraRotationDegrees;
        _camera.Size = _defaultSize;
        ResetOperations++;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            switch (keyEvent.Keycode)
            {
                case Key.W:
                case Key.Up:
                    Pan(0.0f, -PanStepWorldUnits);
                    break;
                case Key.S:
                case Key.Down:
                    Pan(0.0f, PanStepWorldUnits);
                    break;
                case Key.A:
                case Key.Left:
                    Pan(-PanStepWorldUnits, 0.0f);
                    break;
                case Key.D:
                case Key.Right:
                    Pan(PanStepWorldUnits, 0.0f);
                    break;
                case Key.R:
                    ResetToBattleFraming();
                    break;
                case Key.Equal:
                case Key.Plus:
                    ZoomBy(-4.0f);
                    break;
                case Key.Minus:
                    ZoomBy(4.0f);
                    break;
            }
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
            {
                ZoomBy(-4.0f);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
            {
                ZoomBy(4.0f);
            }
        }
    }

    private void EnsureReady()
    {
        if (_camera is null)
        {
            throw new InvalidOperationException("BattleCameraRig requires its Camera3D child before configuration.");
        }
    }
}
