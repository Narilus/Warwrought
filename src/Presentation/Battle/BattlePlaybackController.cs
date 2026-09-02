using System;
using System.Collections.Generic;
using Godot;
using Warwrought.Battle.Model;
using Warwrought.Battle.Playback;
using Warwrought.Battle.Transcript;
using Warwrought.Core;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// The single Godot-facing owner of M1 transcript presentation. It consumes an immutable
/// BattleResolution, advances a presentation clock, samples transcript keyframes, and applies
/// visual-only feedback to ordinary Sprite3D views. It never invokes the authoritative resolver.
/// </summary>
public partial class BattlePlaybackController : Node
{
    public const string SideATexturePath = "res://assets/sprites/m1_side_a_infantry.tres";
    public const string SideBTexturePath = "res://assets/sprites/m1_side_b_infantry.tres";
    public const string RemainsTexturePath = "res://assets/sprites/m1_humanoid_remains.tres";
    public const string HitEffectTexturePath = "res://assets/sprites/m1_hit_effect.tres";

    private const double BumpDurationSeconds = 0.18;
    private const double FlashDurationSeconds = 0.24;
    private const double DeathDurationSeconds = 0.42;
    private const double EffectDurationSeconds = 0.16;
    private const double StatusMessageDurationSeconds = 0.48;

    private readonly Dictionary<string, BattleUnitView> _unitViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Sprite3D> _remainsViews = new(StringComparer.Ordinal);
    private readonly HashSet<string> _routedSquadsShown = new(StringComparer.Ordinal);
    private readonly List<EffectView> _effects = new();

    private BattleResolution? _resolution;
    private BattleTranscriptPlayback? _playback;
    private BattlefieldPresentationProjector? _projector;
    private Node3D? _unitRoot;
    private Node3D? _remainsRoot;
    private Node3D? _effectsRoot;
    private Label? _statusLabel;
    private Label? _resultLabel;
    private Label? _counterLabel;
    private Texture2D? _sideATexture;
    private Texture2D? _sideBTexture;
    private Texture2D? _remainsTexture;
    private Texture2D? _hitEffectTexture;
    private bool _initialized;
    private bool _resultShown;
    private int _unitsSpawned;
    private int _movementKeyframesConsumed;
    private int _contactEventsPresented;
    private int _attackEventsPresented;
    private int _damageEventsPresented;
    private int _deathEventsPresented;
    private int _remainsSpawned;
    private int _effectsSpawned;
    private string? _recentStatusMessage;
    private double _recentStatusRemainingSeconds;

    public BattleResolution Resolution => _resolution ?? throw new InvalidOperationException("BattlePlaybackController has not been initialized.");

    public BattleTranscriptPlayback Playback => _playback ?? throw new InvalidOperationException("BattlePlaybackController has not been initialized.");

    public int UnitsSpawned => _unitsSpawned;

    public int MovementKeyframesConsumed => _movementKeyframesConsumed;

    public int ContactEventsPresented => _contactEventsPresented;

    public int AttackEventsPresented => _attackEventsPresented;

    public int DamageEventsPresented => _damageEventsPresented;

    public int DeathEventsPresented => _deathEventsPresented;

    public int RemainsSpawned => _remainsSpawned;

    /// <summary>
    /// Number of ordinary production unit views retained by the centralized playback owner.
    /// Dead views remain part of this count while their remains presentation is shown.
    /// </summary>
    public int ActiveUnitViewCount => _unitViews.Count;

    public int ActiveRemainsViewCount => _remainsViews.Count;

    public int ActiveEffectViewCount => _effects.Count;

    public int EffectsSpawned => _effectsSpawned;

    public int RoutedFormationsShown => _routedSquadsShown.Count;

    public int TranscriptEventsConsumed => _playback?.EventsConsumed ?? 0;

    public int TranscriptKeyframesConsumed => _playback?.KeyframesConsumed ?? 0;

    public int ControlTransitions => _playback?.Clock.ControlTransitions ?? 0;

    public bool ResultShown => _resultShown;

    public BattlefieldPresentationProjector Projector => _projector ?? throw new InvalidOperationException("BattlePlaybackController has not been initialized.");

    public int UnitProjectionCount => _projector?.GetProjectionCount(BattlefieldProjectionPurpose.Unit) ?? 0;

    public int RemainsProjectionCount => _projector?.GetProjectionCount(BattlefieldProjectionPurpose.Remains) ?? 0;

    public int EffectProjectionCount => _projector?.GetProjectionCount(BattlefieldProjectionPurpose.Effect) ?? 0;

    /// <summary>
    /// The retained resolution identity once the authoritative BattleEnded event has been
    /// presented. The result is exposed from this handoff, never reconstructed from views or HUD
    /// text.
    /// </summary>
    public BattleResolution? PresentedResolution => _resultShown ? Resolution : null;

    public BattleResult? PresentedResult => _resultShown ? Resolution.Result : null;

    public double NominalTranscriptDurationMilliseconds => Playback.NominalDurationMilliseconds;

    public bool PlaybackCompleted
    {
        get
        {
            if (!_initialized || !Playback.IsComplete || !_resultShown)
            {
                return false;
            }

            foreach (var view in _unitViews.Values)
            {
                if (view.IsDeadPresentation && !view.RemainsSpawned)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public override void _Ready()
    {
        // Initialization belongs to BattleLab after it has committed and resolved the fixture.
        // Keeping _Ready empty makes the required data hand-off explicit in the scene path.
    }

    /// <summary>
    /// Accepts the one immutable resolution produced by BattleLab. Required scene roots and
    /// source-controlled placeholder textures are resolved here and missing dependencies throw
    /// instead of being acceptance-created.
    /// </summary>
    public void Initialize(BattleResolution resolution, BattlefieldPresentationProjector projector)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(projector);
        if (_initialized)
        {
            throw new InvalidOperationException("BattlePlaybackController cannot be initialized twice.");
        }

        _resolution = resolution;
        _projector = projector;
        _unitRoot = GetNode<Node3D>("../BattlefieldRoot/UnitRoot");
        _remainsRoot = GetNode<Node3D>("../BattlefieldRoot/RemainsRoot");
        _effectsRoot = GetNode<Node3D>("../BattlefieldRoot/EffectsRoot");
        _statusLabel = GetNode<Label>("../BattleHUD/Panel/Content/Status");
        _resultLabel = GetNode<Label>("../BattleHUD/Panel/Content/Result");
        _counterLabel = GetNode<Label>("../BattleHUD/Panel/Content/Counters");

        _sideATexture = LoadRequiredTexture(SideATexturePath);
        _sideBTexture = LoadRequiredTexture(SideBTexturePath);
        _remainsTexture = LoadRequiredTexture(RemainsTexturePath);
        _hitEffectTexture = LoadRequiredTexture(HitEffectTexturePath);

        _playback = new BattleTranscriptPlayback(resolution);
        _initialized = true;
        ResetVisualsAndCursor();
        UpdateHud();
    }

    public void Play()
    {
        Playback.Clock.Play();
        UpdateHud();
    }

    public void Pause()
    {
        Playback.Clock.Pause();
        UpdateHud();
    }

    public void SetSpeed(double speed)
    {
        Playback.Clock.SetSpeed(speed);
        UpdateHud();
    }

    public void ResetPlayback()
    {
        EnsureInitialized();
        ResetVisualsAndCursor();
        UpdateHud();
    }

    public override void _Process(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        var step = Playback.Advance(delta);
        ApplyKeyframes(step.KeyframesConsumed);
        DispatchEvents(step.EventsConsumed);
        UpdateInterpolatedTransforms();
        UpdateVisualTimers(delta);
        UpdateEffects(delta);
        UpdateHud();
    }

    private void ResetVisualsAndCursor()
    {
        ClearVisualNodes();
        _routedSquadsShown.Clear();
        _unitsSpawned = 0;
        _movementKeyframesConsumed = 0;
        _contactEventsPresented = 0;
        _attackEventsPresented = 0;
        _damageEventsPresented = 0;
        _deathEventsPresented = 0;
        _remainsSpawned = 0;
        _effectsSpawned = 0;
        _resultShown = false;
        _recentStatusMessage = null;
        _recentStatusRemainingSeconds = 0.0;

        var initialStep = Playback.ResetAndConsume();
        CreateUnitViewsFromTranscriptStart();
        ApplyKeyframes(initialStep.KeyframesConsumed);
        DispatchEvents(initialStep.EventsConsumed);
        UpdateInterpolatedTransforms();
        _resultLabel!.Text = "Result: pending — transcript playback has not reached BattleEnded.";
    }

    private void ClearVisualNodes()
    {
        foreach (var view in _unitViews.Values)
        {
            view.Sprite.QueueFree();
        }

        foreach (var remains in _remainsViews.Values)
        {
            remains.QueueFree();
        }

        for (var index = 0; index < _effects.Count; index++)
        {
            _effects[index].Sprite.QueueFree();
        }

        _unitViews.Clear();
        _remainsViews.Clear();
        _effects.Clear();
    }

    private void CreateUnitViewsFromTranscriptStart()
    {
        EnsureInitialized();
        var startKeyframes = new List<BattleFormationKeyframe>();
        for (var index = 0; index < Resolution.Transcript.Keyframes.Count; index++)
        {
            var keyframe = Resolution.Transcript.Keyframes[index];
            if (keyframe.Tick.Value == 0)
            {
                startKeyframes.Add(keyframe);
            }
            else if (startKeyframes.Count > 0)
            {
                break;
            }
        }

        if (startKeyframes.Count != 2)
        {
            throw new InvalidOperationException($"BattleLab requires two transcript start keyframes; observed {startKeyframes.Count}.");
        }

        for (var keyframeIndex = 0; keyframeIndex < startKeyframes.Count; keyframeIndex++)
        {
            var keyframe = startKeyframes[keyframeIndex];
            var factionColor = keyframe.SideId.Value == "side.a"
                ? new Color(0.94f, 0.22f, 0.16f, 1.0f)
                : new Color(0.18f, 0.50f, 0.98f, 1.0f);
            var texture = keyframe.SideId.Value == "side.a" ? _sideATexture! : _sideBTexture!;

            for (var memberIndex = 0; memberIndex < keyframe.Members.Count; memberIndex++)
            {
                var member = keyframe.Members[memberIndex];
                if (_unitViews.ContainsKey(member.UnitId.Value))
                {
                    continue;
                }

                var sprite = new Sprite3D
                {
                    Name = $"UnitView_{_unitsSpawned:000}",
                    Texture = texture,
                    Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
                    PixelSize = 0.012f,
                    Modulate = factionColor,
                    Position = Projector.Project(member.Position, BattlefieldPresentationProjector.UnitVerticalOffset, BattlefieldProjectionPurpose.Unit),
                };
                _unitRoot!.AddChild(sprite);
                var view = new BattleUnitView(member.UnitId, keyframe.SideId, keyframe.SquadId, sprite, factionColor);
                _unitViews.Add(member.UnitId.Value, view);
                _unitsSpawned++;
            }
        }

        if (_unitsSpawned != Resolution.Result.Survivors.Count + Resolution.Result.Casualties.Count)
        {
            throw new InvalidOperationException(
                $"BattleLab transcript start spawned {_unitsSpawned} units but the authoritative result contains {Resolution.Result.Survivors.Count + Resolution.Result.Casualties.Count} members.");
        }
    }

    private void ApplyKeyframes(IReadOnlyList<BattleFormationKeyframe> keyframes)
    {
        for (var keyframeIndex = 0; keyframeIndex < keyframes.Count; keyframeIndex++)
        {
            var keyframe = keyframes[keyframeIndex];
            _movementKeyframesConsumed++;
            for (var memberIndex = 0; memberIndex < keyframe.Members.Count; memberIndex++)
            {
                var member = keyframe.Members[memberIndex];
                if (!_unitViews.TryGetValue(member.UnitId.Value, out var view))
                {
                    throw new InvalidOperationException($"Transcript keyframe referenced unspawned unit '{member.UnitId.Value}'.");
                }

                view.LastAuthoritativeState = member.State;
                view.IsRouted = member.State is BattleUnitState.Routed or BattleUnitState.Retreated;
            }
        }
    }

    private void DispatchEvents(IReadOnlyList<BattleSemanticEvent> events)
    {
        for (var eventIndex = 0; eventIndex < events.Count; eventIndex++)
        {
            var @event = events[eventIndex];
            switch (@event.Type)
            {
                case BattleEventType.ContactStarted:
                    _contactEventsPresented++;
                    SpawnEffect(GetEventWorldPosition(@event), new Color(1.0f, 0.86f, 0.35f, 1.0f), 0.65f);
                    SetRecentStatus("CONTACT — front ranks engaged");
                    break;
                case BattleEventType.ContactEnded:
                    SetRecentStatus("CONTACT ENDED — formation withdrawal");
                    break;
                case BattleEventType.AttackResolved:
                    _attackEventsPresented++;
                    PresentAttack(@event);
                    break;
                case BattleEventType.DamageDealt:
                    _damageEventsPresented++;
                    PresentDamage(@event);
                    break;
                case BattleEventType.UnitKilled:
                    _deathEventsPresented++;
                    PresentDeath(@event);
                    break;
                case BattleEventType.RoutStarted:
                    PresentRout(@event);
                    break;
                case BattleEventType.RetreatCompleted:
                    SetRecentStatus("RETREAT COMPLETE — formation withdrawn");
                    break;
                case BattleEventType.BattleEnded:
                    PresentBattleEnded(@event);
                    break;
                case BattleEventType.BattleStarted:
                case BattleEventType.FormationMoved:
                case BattleEventType.MoraleChanged:
                    // Movement and morale state are represented by the keyframes and the HUD;
                    // no outcome decision is reconstructed in this presentation switch.
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(events), @event.Type, "Unsupported transcript event type.");
            }
        }
    }

    private void PresentAttack(BattleSemanticEvent @event)
    {
        if (!@event.SourceUnitId.HasValue)
        {
            return;
        }

        if (!_unitViews.TryGetValue(@event.SourceUnitId.Value.Value, out var attacker))
        {
            throw new InvalidOperationException($"Attack event referenced unspawned source unit '{@event.SourceUnitId.Value.Value}'.");
        }

        var attackerPosition = attacker.Sprite.Position;
        var targetPosition = @event.TargetUnitId.HasValue && _unitViews.TryGetValue(@event.TargetUnitId.Value.Value, out var target)
            ? target.Sprite.Position
            : attackerPosition + new Vector3(0.0f, 0.0f, attacker.SideId.Value == "side.a" ? 0.5f : -0.5f);
        var direction = targetPosition - attackerPosition;
        direction.Y = 0.0f;
        var length = Math.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z));
        attacker.BumpDirection = length <= 0.0001
            ? new Vector3(0.0f, 0.0f, attacker.SideId.Value == "side.a" ? 1.0f : -1.0f)
            : new Vector3((float)(direction.X / length), 0.0f, (float)(direction.Z / length));
        attacker.BumpRemainingSeconds = BumpDurationSeconds;
        SetRecentStatus("ATTACK — front-rank bump");
        SpawnEffect(GetEventWorldPosition(@event), new Color(1.0f, 0.78f, 0.25f, 1.0f), 0.32f);
    }

    private void PresentDamage(BattleSemanticEvent @event)
    {
        if (@event.TargetUnitId.HasValue && _unitViews.TryGetValue(@event.TargetUnitId.Value.Value, out var target))
        {
            target.FlashRemainingSeconds = FlashDurationSeconds;
        }

        SetRecentStatus("DAMAGE — transient red flash");
        SpawnEffect(GetEventWorldPosition(@event), new Color(1.0f, 0.18f, 0.12f, 1.0f), 0.28f);
    }

    private void PresentDeath(BattleSemanticEvent @event)
    {
        if (!@event.TargetUnitId.HasValue || !_unitViews.TryGetValue(@event.TargetUnitId.Value.Value, out var view))
        {
            throw new InvalidOperationException("A unit-killed transcript event must identify a spawned target unit.");
        }

        if (view.IsDeadPresentation)
        {
            return;
        }

        view.IsDeadPresentation = true;
        view.DeathRemainingSeconds = DeathDurationSeconds;
        view.DeathElapsedSeconds = 0.0;
        view.FallRotationRadians = view.SideId.Value == "side.a" ? -1.25f : 1.25f;
        view.FlashRemainingSeconds = FlashDurationSeconds;
        SetRecentStatus("DEATH — fall then remains");
        SpawnEffect(GetEventWorldPosition(@event), new Color(1.0f, 0.12f, 0.08f, 1.0f), 0.36f);
    }

    private void PresentRout(BattleSemanticEvent @event)
    {
        if (!@event.SquadId.HasValue || !_routedSquadsShown.Add(@event.SquadId.Value.Value))
        {
            return;
        }

        foreach (var view in _unitViews.Values)
        {
            if (view.SquadId == @event.SquadId.Value)
            {
                view.IsRouted = true;
            }
        }

        SpawnEffect(GetEventWorldPosition(@event), new Color(1.0f, 0.58f, 0.14f, 1.0f), 0.95f);
        SetRecentStatus("ROUT — formation withdrawing");
    }

    private void PresentBattleEnded(BattleSemanticEvent @event)
    {
        _resultShown = true;
        var result = Resolution.Result;
        // BattleEnded is an authoritative transcript record. The displayed result is the stored
        // BattleResult from the handoff; no presentation state is inspected to infer an outcome.
        _resultLabel!.Text = @event.IsTerminal
            ? $"Result: {result.ResultType}  |  tick {result.TerminalTick.Value}  |  digest {result.CanonicalDigest[..12]}…"
            : $"Result: {result.ResultType} — {result.DiagnosticReason}";
        _statusLabel!.Text = @event.IsTerminal
            ? "BattleEnded consumed — authoritative result is now shown."
            : "BattleEnded consumed — authoritative resolution reported a non-terminal failure.";
    }

    private void UpdateInterpolatedTransforms()
    {
        var exactTick = Playback.Clock.CurrentTickExact;
        foreach (var view in _unitViews.Values)
        {
            var sample = Playback.SampleMemberPosition(view.UnitId, exactTick);
            var position = Projector.Project(sample.X, sample.Z, BattlefieldPresentationProjector.UnitVerticalOffset, BattlefieldProjectionPurpose.Unit);

            if (view.BumpRemainingSeconds > 0.0 && !view.IsDeadPresentation)
            {
                var progress = 1.0 - (view.BumpRemainingSeconds / BumpDurationSeconds);
                var bump = Math.Sin(Math.Clamp(progress, 0.0, 1.0) * Math.PI) * 0.16;
                position += view.BumpDirection * (float)bump;
            }

            view.Sprite.Position = position;
        }
    }

    private void UpdateVisualTimers(double delta)
    {
        foreach (var view in _unitViews.Values)
        {
            view.BumpRemainingSeconds = Math.Max(0.0, view.BumpRemainingSeconds - delta);
            view.FlashRemainingSeconds = Math.Max(0.0, view.FlashRemainingSeconds - delta);

            if (view.IsDeadPresentation && view.DeathRemainingSeconds > 0.0)
            {
                view.DeathRemainingSeconds = Math.Max(0.0, view.DeathRemainingSeconds - delta);
                view.DeathElapsedSeconds += delta;
                var progress = Math.Clamp(view.DeathElapsedSeconds / DeathDurationSeconds, 0.0, 1.0);
                view.Sprite.Rotation = new Vector3(0.0f, 0.0f, view.FallRotationRadians * (float)progress);
                if (view.DeathRemainingSeconds <= 0.0 && !view.RemainsSpawned)
                {
                    SpawnRemains(view);
                }
            }

            if (view.IsDeadPresentation && view.RemainsSpawned)
            {
                view.Sprite.Visible = false;
                continue;
            }

            if (view.FlashRemainingSeconds > 0.0)
            {
                view.Sprite.Modulate = new Color(1.0f, 0.12f, 0.10f, 1.0f);
            }
            else if (view.IsRouted)
            {
                view.Sprite.Modulate = new Color(
                    Math.Min(1.0f, view.FactionColor.R + 0.08f),
                    Math.Min(1.0f, view.FactionColor.G + 0.08f),
                    Math.Min(1.0f, view.FactionColor.B + 0.08f),
                    1.0f);
            }
            else
            {
                view.Sprite.Modulate = view.FactionColor;
            }
        }

        _recentStatusRemainingSeconds = Math.Max(0.0, _recentStatusRemainingSeconds - delta);
    }

    private void SpawnRemains(BattleUnitView view)
    {
        var currentSample = Playback.SampleMemberPosition(view.UnitId, Playback.Clock.CurrentTickExact);
        var remains = new Sprite3D
        {
            Name = $"Remains_{_remainsViews.Count:000}",
            Texture = _remainsTexture,
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            PixelSize = 0.014f,
            Modulate = view.FactionColor,
            Position = Projector.Project(
                currentSample.X,
                currentSample.Z,
                BattlefieldPresentationProjector.RemainsVerticalOffset,
                BattlefieldProjectionPurpose.Remains),
        };
        _remainsRoot!.AddChild(remains);
        _remainsViews.Add(view.UnitId.Value, remains);
        view.RemainsSpawned = true;
        _remainsSpawned++;
    }

    private void SpawnEffect(Vector3 position, Color color, float pixelScale)
    {
        var effect = new Sprite3D
        {
            Name = $"Effect_{_effects.Count:000}",
            Texture = _hitEffectTexture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = pixelScale * 0.012f,
            Modulate = color,
            Position = position,
        };
        _effectsRoot!.AddChild(effect);
        _effects.Add(new EffectView(effect, EffectDurationSeconds));
        _effectsSpawned++;
    }

    private void UpdateEffects(double delta)
    {
        for (var index = _effects.Count - 1; index >= 0; index--)
        {
            var effect = _effects[index];
            effect.RemainingSeconds -= delta;
            if (effect.RemainingSeconds <= 0.0)
            {
                effect.Sprite.QueueFree();
                _effects.RemoveAt(index);
            }
        }
    }

    private Vector3 GetEventWorldPosition(BattleSemanticEvent @event)
    {
        if (@event.Position.HasValue)
        {
            return Projector.Project(
                @event.Position.Value,
                BattlefieldPresentationProjector.EffectVerticalOffset,
                BattlefieldProjectionPurpose.Effect);
        }

        if (@event.SourceUnitId.HasValue && _unitViews.TryGetValue(@event.SourceUnitId.Value.Value, out var source))
        {
            var sample = Playback.SampleMemberPosition(source.UnitId, Playback.Clock.CurrentTickExact);
            return Projector.Project(
                sample.X,
                sample.Z,
                BattlefieldPresentationProjector.EffectVerticalOffset,
                BattlefieldProjectionPurpose.Effect);
        }

        var center = Projector.Definition.Bounds.Center;
        return Projector.Project(
            center,
            BattlefieldPresentationProjector.EffectVerticalOffset,
            BattlefieldProjectionPurpose.Effect);
    }

    private void UpdateHud()
    {
        if (!_initialized)
        {
            return;
        }

        var clock = Playback.Clock;
        var mode = clock.IsPlaying ? "PLAYING" : (clock.IsAtEnd ? "COMPLETE" : "PAUSED");
        if (!_resultShown)
        {
            _statusLabel!.Text = _recentStatusRemainingSeconds > 0.0 && !string.IsNullOrWhiteSpace(_recentStatusMessage)
                ? $"{_recentStatusMessage}  |  {mode}  |  {clock.Speed:0.#}x  |  presentation tick {clock.CurrentTick}/{clock.TerminalTick}"
                : $"{mode}  |  {clock.Speed:0.#}x  |  presentation tick {clock.CurrentTick}/{clock.TerminalTick}";
        }
        _counterLabel!.Text =
            $"Views {UnitsSpawned}  •  keyframes {MovementKeyframesConsumed}/{Resolution.Transcript.KeyframeCount}  •  " +
            $"attacks {AttackEventsPresented}  •  damage {DamageEventsPresented}  •  deaths {DeathEventsPresented}  •  remains {RemainsSpawned}  •  routs {RoutedFormationsShown}";
    }

    private void SetRecentStatus(string message)
    {
        _recentStatusMessage = message;
        _recentStatusRemainingSeconds = StatusMessageDurationSeconds;
    }

    private static Texture2D LoadRequiredTexture(string path)
    {
        var texture = GD.Load<Texture2D>(path);
        return texture ?? throw new InvalidOperationException($"BattleLab required Sprite3D texture resource is missing: {path}");
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("BattlePlaybackController has not been initialized.");
        }
    }

    private sealed class EffectView
    {
        public EffectView(Sprite3D sprite, double remainingSeconds)
        {
            Sprite = sprite;
            RemainingSeconds = remainingSeconds;
        }

        public Sprite3D Sprite { get; }

        public double RemainingSeconds { get; set; }
    }
}
