using System;

namespace Warwrought.Battle.Playback;

/// <summary>
/// Presentation time for a committed transcript. This clock is deliberately separate from
/// the authoritative fixed-tick resolver: changing it can only change when transcript data is
/// presented, never the stored resolution.
/// </summary>
public sealed class BattlePlaybackClock
{
    private readonly int _ticksPerSecond;
    private readonly long _terminalTick;
    private double _elapsedSeconds;
    private bool _isPlaying;

    public BattlePlaybackClock(int ticksPerSecond, long terminalTick)
    {
        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Playback ticks per second must be positive.");
        }

        if (terminalTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalTick), terminalTick, "Playback terminal tick cannot be negative.");
        }

        _ticksPerSecond = ticksPerSecond;
        _terminalTick = terminalTick;
        Speed = 1.0;
    }

    public int TicksPerSecond => _ticksPerSecond;

    public long TerminalTick => _terminalTick;

    public double ElapsedSeconds => _elapsedSeconds;

    public double Speed { get; private set; }

    public bool IsPlaying => _isPlaying;

    public bool IsAtEnd => CurrentTick >= _terminalTick;

    public long CurrentTick
    {
        get
        {
            var exactTick = _elapsedSeconds * _ticksPerSecond;
            if (exactTick <= 0.0)
            {
                return 0;
            }

            if (exactTick >= _terminalTick)
            {
                return _terminalTick;
            }

            return (long)Math.Floor(exactTick);
        }
    }

    public double CurrentTickExact => Math.Clamp(_elapsedSeconds * _ticksPerSecond, 0.0, _terminalTick);

    /// <summary>
    /// Counts explicit playback control requests for runtime evidence. It is not authoritative
    /// state and is never included in a battle result or digest.
    /// </summary>
    public int ControlTransitions { get; private set; }

    public void Play()
    {
        ControlTransitions++;
        if (!IsAtEnd)
        {
            _isPlaying = true;
        }
    }

    public void Pause()
    {
        ControlTransitions++;
        _isPlaying = false;
    }

    public void Reset()
    {
        ControlTransitions++;
        _elapsedSeconds = 0.0;
        _isPlaying = false;
    }

    public void SetSpeed(double speed)
    {
        if (double.IsNaN(speed) || double.IsInfinity(speed) || speed <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Playback speed must be finite and positive.");
        }

        ControlTransitions++;
        Speed = speed;
    }

    public void Advance(double realDeltaSeconds)
    {
        if (!_isPlaying || realDeltaSeconds <= 0.0 || double.IsNaN(realDeltaSeconds) || double.IsInfinity(realDeltaSeconds))
        {
            return;
        }

        _elapsedSeconds += realDeltaSeconds * Speed;
        var terminalSeconds = (double)_terminalTick / _ticksPerSecond;
        if (_elapsedSeconds >= terminalSeconds)
        {
            _elapsedSeconds = terminalSeconds;
            _isPlaying = false;
        }
    }
}
