using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Warwrought.Battle.Model;
using Warwrought.Battle.Simulation;

namespace Warwrought.Battle.Transcript;

/// <summary>
/// Small inspectable JSON artifact support for M1 debugging. This is not replay persistence:
/// it writes the already-created in-memory resolution, performs no simulation, and adds no
/// compression, seeking, or save-game policy.
/// </summary>
public static class BattleDebugSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(BattleResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return JsonSerializer.Serialize(BattleDebugDocument.Create(resolution), SerializerOptions);
    }

    public static BattleDebugDocument Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = JsonSerializer.Deserialize<BattleDebugDocument>(json, SerializerOptions);
        if (document is null)
        {
            throw new InvalidDataException("The battle debug JSON did not contain a document.");
        }

        document.ValidateIdentity();
        return document;
    }

    public static void WriteJson(string path, BattleResolution resolution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, Serialize(resolution));
    }

    public static BattleDebugDocument ReadJson(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Deserialize(File.ReadAllText(path));
    }
}

/// <summary>
/// Human-readable DTO for the complete M1 result/transcript debug contract.
/// String IDs and primitive coordinates keep the artifact easy to inspect and independent
/// from JSON formatting for authoritative digest purposes.
/// </summary>
public sealed class BattleDebugDocument
{
    public const string DocumentIdentity = "warwrought.battle-debug-artifact.m1.v1";

    public int SchemaVersion { get; init; } = 1;

    public string Identity { get; init; } = DocumentIdentity;

    public string BattleId { get; init; } = string.Empty;

    public int SimulationVersionMajor { get; init; }

    public int SimulationVersionMinor { get; init; }

    public ulong Seed { get; init; }

    public string CanonicalInputDigest { get; init; } = string.Empty;

    public int TicksPerSecond { get; init; }

    public int KeyframeIntervalTicks { get; init; }

    public int MaximumSimulationTicks { get; init; }

    public List<BattleDebugEvent> Events { get; init; } = new();

    public List<BattleDebugKeyframe> Keyframes { get; init; } = new();

    public BattleDebugResult Result { get; init; } = new();

    public string TranscriptDigest { get; init; } = string.Empty;

    public string CanonicalDigest { get; init; } = string.Empty;

    internal static BattleDebugDocument Create(BattleResolution resolution)
    {
        var header = resolution.Transcript.Header;
        var document = new BattleDebugDocument
        {
            BattleId = header.BattleId.Value,
            SimulationVersionMajor = header.SimulationVersion.Major,
            SimulationVersionMinor = header.SimulationVersion.Minor,
            Seed = header.Seed,
            CanonicalInputDigest = header.CanonicalInputDigest,
            TicksPerSecond = header.TicksPerSecond,
            KeyframeIntervalTicks = header.KeyframeIntervalTicks,
            MaximumSimulationTicks = M1BattleSettings.MaximumSimulationTicks,
            TranscriptDigest = resolution.Transcript.CanonicalDigest,
            CanonicalDigest = resolution.CanonicalDigest,
            Result = BattleDebugResult.Create(resolution.Result),
        };

        for (var index = 0; index < resolution.Transcript.Events.Count; index++)
        {
            document.Events.Add(BattleDebugEvent.Create(resolution.Transcript.Events[index]));
        }

        for (var index = 0; index < resolution.Transcript.Keyframes.Count; index++)
        {
            document.Keyframes.Add(BattleDebugKeyframe.Create(resolution.Transcript.Keyframes[index]));
        }

        return document;
    }

    internal void ValidateIdentity()
    {
        if (SchemaVersion != 1 || !StringComparer.Ordinal.Equals(Identity, DocumentIdentity))
        {
            throw new InvalidDataException($"Unsupported battle debug artifact identity/version: {Identity} v{SchemaVersion}.");
        }
    }
}

public sealed class BattleDebugEvent
{
    public int Sequence { get; init; }

    public long Tick { get; init; }

    public BattleEventType Type { get; init; }

    public string SideId { get; init; } = string.Empty;

    public bool HasSideId { get; init; }

    public string SquadId { get; init; } = string.Empty;

    public bool HasSquadId { get; init; }

    public string OtherSideId { get; init; } = string.Empty;

    public bool HasOtherSideId { get; init; }

    public string OtherSquadId { get; init; } = string.Empty;

    public bool HasOtherSquadId { get; init; }

    public string SourceUnitId { get; init; } = string.Empty;

    public bool HasSourceUnitId { get; init; }

    public string TargetUnitId { get; init; } = string.Empty;

    public bool HasTargetUnitId { get; init; }

    public int Amount { get; init; }

    public int Value { get; init; }

    public int SecondaryValue { get; init; }

    public string Reason { get; init; } = string.Empty;

    public bool HasPosition { get; init; }

    public int PositionX { get; init; }

    public int PositionZ { get; init; }

    public bool HasFacing { get; init; }

    public FormationFacing Facing { get; init; }

    public bool IsTerminal { get; init; }

    internal static BattleDebugEvent Create(BattleSemanticEvent @event)
    {
        return new BattleDebugEvent
        {
            Sequence = @event.Sequence,
            Tick = @event.Tick.Value,
            Type = @event.Type,
            SideId = @event.SideId?.Value ?? string.Empty,
            HasSideId = @event.SideId.HasValue,
            SquadId = @event.SquadId?.Value ?? string.Empty,
            HasSquadId = @event.SquadId.HasValue,
            OtherSideId = @event.OtherSideId?.Value ?? string.Empty,
            HasOtherSideId = @event.OtherSideId.HasValue,
            OtherSquadId = @event.OtherSquadId?.Value ?? string.Empty,
            HasOtherSquadId = @event.OtherSquadId.HasValue,
            SourceUnitId = @event.SourceUnitId?.Value ?? string.Empty,
            HasSourceUnitId = @event.SourceUnitId.HasValue,
            TargetUnitId = @event.TargetUnitId?.Value ?? string.Empty,
            HasTargetUnitId = @event.TargetUnitId.HasValue,
            Amount = @event.Amount,
            Value = @event.Value,
            SecondaryValue = @event.SecondaryValue,
            Reason = @event.Reason,
            HasPosition = @event.Position.HasValue,
            PositionX = @event.Position?.X ?? 0,
            PositionZ = @event.Position?.Z ?? 0,
            HasFacing = @event.Facing.HasValue,
            Facing = @event.Facing ?? default,
            IsTerminal = @event.IsTerminal,
        };
    }
}

public sealed class BattleDebugKeyframe
{
    public long Tick { get; init; }

    public string SideId { get; init; } = string.Empty;

    public string SquadId { get; init; } = string.Empty;

    public int AnchorX { get; init; }

    public int AnchorZ { get; init; }

    public FormationFacing Facing { get; init; }

    public BattleFormationState State { get; init; }

    public int Morale { get; init; }

    public List<BattleDebugMember> Members { get; init; } = new();

    internal static BattleDebugKeyframe Create(BattleFormationKeyframe keyframe)
    {
        var result = new BattleDebugKeyframe
        {
            Tick = keyframe.Tick.Value,
            SideId = keyframe.SideId.Value,
            SquadId = keyframe.SquadId.Value,
            AnchorX = keyframe.Anchor.X,
            AnchorZ = keyframe.Anchor.Z,
            Facing = keyframe.Facing,
            State = keyframe.State,
            Morale = keyframe.Morale,
        };

        for (var index = 0; index < keyframe.Members.Count; index++)
        {
            result.Members.Add(BattleDebugMember.Create(keyframe.Members[index]));
        }

        return result;
    }
}

public sealed class BattleDebugMember
{
    public string UnitId { get; init; } = string.Empty;

    public int SlotIndex { get; init; }

    public int Rank { get; init; }

    public int File { get; init; }

    public int PositionX { get; init; }

    public int PositionZ { get; init; }

    public BattleUnitState State { get; init; }

    public bool IsAlive { get; init; }

    internal static BattleDebugMember Create(BattleMemberKeyframe member)
    {
        return new BattleDebugMember
        {
            UnitId = member.UnitId.Value,
            SlotIndex = member.SlotIndex,
            Rank = member.Rank,
            File = member.File,
            PositionX = member.Position.X,
            PositionZ = member.Position.Z,
            State = member.State,
            IsAlive = member.IsAlive,
        };
    }
}

public sealed class BattleDebugResult
{
    public string BattleId { get; init; } = string.Empty;

    public int SimulationVersionMajor { get; init; }

    public int SimulationVersionMinor { get; init; }

    public ulong Seed { get; init; }

    public BattleResultType ResultType { get; init; }

    public string WinnerSideId { get; init; } = string.Empty;

    public bool HasWinnerSideId { get; init; }

    public long TerminalTick { get; init; }

    public bool IsTerminal { get; init; }

    public string DiagnosticReason { get; init; } = string.Empty;

    public List<BattleDebugSurvivor> Survivors { get; init; } = new();

    public List<BattleDebugCasualty> Casualties { get; init; } = new();

    public string TranscriptDigest { get; init; } = string.Empty;

    public string CanonicalDigest { get; init; } = string.Empty;

    internal static BattleDebugResult Create(BattleResult result)
    {
        var debugResult = new BattleDebugResult
        {
            BattleId = result.BattleId.Value,
            SimulationVersionMajor = result.SimulationVersion.Major,
            SimulationVersionMinor = result.SimulationVersion.Minor,
            Seed = result.Seed,
            ResultType = result.ResultType,
            WinnerSideId = result.WinnerSideId?.Value ?? string.Empty,
            HasWinnerSideId = result.WinnerSideId.HasValue,
            TerminalTick = result.TerminalTick.Value,
            IsTerminal = result.IsTerminal,
            DiagnosticReason = result.DiagnosticReason,
            TranscriptDigest = result.TranscriptDigest,
            CanonicalDigest = result.CanonicalDigest,
        };

        for (var index = 0; index < result.Survivors.Count; index++)
        {
            var survivor = result.Survivors[index];
            debugResult.Survivors.Add(new BattleDebugSurvivor
            {
                UnitId = survivor.UnitId.Value,
                SideId = survivor.SideId.Value,
                SquadId = survivor.SquadId.Value,
                State = survivor.State,
                RemainingHealth = survivor.RemainingHealth,
                FinalMorale = survivor.FinalMorale,
            });
        }

        for (var index = 0; index < result.Casualties.Count; index++)
        {
            var casualty = result.Casualties[index];
            debugResult.Casualties.Add(new BattleDebugCasualty
            {
                UnitId = casualty.UnitId.Value,
                SideId = casualty.SideId.Value,
                SquadId = casualty.SquadId.Value,
                KilledAtTick = casualty.KilledAtTick.Value,
                KilledByUnitId = casualty.KilledByUnitId?.Value ?? string.Empty,
                HasKilledByUnitId = casualty.KilledByUnitId.HasValue,
            });
        }

        return debugResult;
    }
}

public sealed class BattleDebugSurvivor
{
    public string UnitId { get; init; } = string.Empty;

    public string SideId { get; init; } = string.Empty;

    public string SquadId { get; init; } = string.Empty;

    public BattleUnitState State { get; init; }

    public int RemainingHealth { get; init; }

    public int FinalMorale { get; init; }
}

public sealed class BattleDebugCasualty
{
    public string UnitId { get; init; } = string.Empty;

    public string SideId { get; init; } = string.Empty;

    public string SquadId { get; init; } = string.Empty;

    public long KilledAtTick { get; init; }

    public string KilledByUnitId { get; init; } = string.Empty;

    public bool HasKilledByUnitId { get; init; }
}
