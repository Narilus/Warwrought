using Godot;
using Warwrought.Battle.Model;
using Warwrought.Battle.Transcript;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Lightweight presentation state for one transcript member. It has no process callback and no
/// tactical behaviour; BattlePlaybackController owns every update.
/// </summary>
internal sealed class BattleUnitView
{
    public BattleUnitView(
        BattleUnitId unitId,
        BattleSideId sideId,
        BattleSquadId squadId,
        Sprite3D sprite,
        Color factionColor)
    {
        UnitId = unitId;
        SideId = sideId;
        SquadId = squadId;
        Sprite = sprite;
        FactionColor = factionColor;
        LastAuthoritativeState = BattleUnitState.Active;
    }

    public BattleUnitId UnitId { get; }

    public BattleSideId SideId { get; }

    public BattleSquadId SquadId { get; }

    public Sprite3D Sprite { get; }

    public Color FactionColor { get; }

    public BattleUnitState LastAuthoritativeState { get; set; }

    public bool IsRouted { get; set; }

    public bool IsDeadPresentation { get; set; }

    public bool RemainsSpawned { get; set; }

    public double BumpRemainingSeconds { get; set; }

    public Vector3 BumpDirection { get; set; }

    public double FlashRemainingSeconds { get; set; }

    public double DeathRemainingSeconds { get; set; }

    public double DeathElapsedSeconds { get; set; }

    public float FallRotationRadians { get; set; }
}
