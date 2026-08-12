using System;
using Warwrought.Battle.Model;
using Warwrought.Battle.Transcript;

namespace Warwrought.Battle.Simulation;

/// <summary>
/// The narrow M1 handoff at the battle commitment boundary. One immutable committed definition
/// is resolved once; skip and watch are consumers of the retained resolution pair.
/// </summary>
public sealed class CommittedBattleResolution
{
    private readonly BattleResolution _resolution;

    private CommittedBattleResolution(BattleDefinition definition)
    {
        Definition = definition;

        // This is deliberately the only resolver call owned by the committed-resolution path.
        // The immutable result/transcript pair is retained for every later presentation choice.
        _resolution = AuthoritativeBattleResolver.Resolve(definition);
    }

    public BattleDefinition Definition { get; }

    public BattleResolution Resolution => _resolution;

    public static CommittedBattleResolution Commit(BattleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new CommittedBattleResolution(definition);
    }

    /// <summary>
    /// Consumes the already-retained authoritative result without constructing playback state.
    /// </summary>
    public BattleResult SkipToResult() => _resolution.Result;

    /// <summary>
    /// Hands the exact retained resolution identity to watched playback.
    /// </summary>
    public BattleResolution WatchResolution => _resolution;
}
