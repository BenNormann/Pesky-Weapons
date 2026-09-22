using System;
using System.Diagnostics;

namespace Pesky.Session
{
    /// <summary>
    /// Room time: milliseconds since session start on the host's Stopwatch.
    /// The host reads its watch directly; a client holds an offset from its
    /// own watch to host time, estimated from CLOCK_PING/CLOCK_PONG (median
    /// of the last eight RTT-corrected samples) and coarse-checked against
    /// TIME_SYNC. Wall-clock, never Time.time, so a throttled tab catches up
    /// instead of drifting. Owns nothing but time; the tick is derived here.
    /// </summary>
    public sealed class RoomClock
    {
        public const int SampleCount = 8;

        /// <summary>A TIME_SYNC this far from the estimate throws the samples away and re-seeds from it.</summary>
        public const long CoarseToleranceMs = 1000;

        readonly Func<long> _localMs;
        readonly Stopwatch _watch;
        readonly long[] _samples = new long[SampleCount];
        readonly long[] _sorted = new long[SampleCount];
        int _sampleCount;
        int _sampleNext;
        long _offsetMs;

        public bool IsHost { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>True once a CLOCK_PONG or a coarse TIME_SYNC has given a client an estimate; always true on the host.</summary>
        public bool HasEstimate { get; private set; }

        /// <summary>Host room ms minus local ms.</summary>
        public long OffsetMs => _offsetMs;

        public int Samples => _sampleCount;

        /// <summary>The round trip of the last CLOCK_PING, milliseconds; -1 before the first pong. Always -1 on the host.</summary>
        public long LastRttMs { get; private set; } = -1;

        /// <summary>Default: a Stopwatch. Tests pass their own millisecond source.</summary>
        public RoomClock(Func<long> localMsSource = null)
        {
            if (localMsSource != null)
            {
                _localMs = localMsSource;
            }
            else
            {
                _watch = Stopwatch.StartNew();
                _localMs = ReadWatch;
            }
        }

        long ReadWatch() => _watch.ElapsedMilliseconds;

        /// <summary>The local monotonic clock, before any offset.</summary>
        public long LocalMs => _localMs();

        /// <summary>Room milliseconds; zero until started.</summary>
        public long NowMs => IsRunning ? _localMs() + _offsetMs : 0;

        public uint Tick => Protocol.Tick.FromMs(NowMs);

        /// <summary>Host: room time starts at zero now.</summary>
        public void StartHost()
        {
            IsHost = true;
            IsRunning = true;
            HasEstimate = true;
            _offsetMs = -_localMs();
            ResetSamples();
        }

        /// <summary>Client: runs from zero on the local watch until the first estimate arrives.</summary>
        public void StartClient()
        {
            IsHost = false;
            IsRunning = true;
            HasEstimate = false;
            _offsetMs = -_localMs();
            ResetSamples();
        }

        /// <summary>Makes room time equal roomMs right now. The host uses it after a long stall; clients re-estimate via TIME_SYNC.</summary>
        public void JumpTo(long roomMs)
        {
            _offsetMs = roomMs - _localMs();
        }

        /// <summary>
        /// A CLOCK_PONG: when the client sent the ping (its local ms), the host
        /// room ms at receipt, and the client's local ms when the pong landed.
        /// Symmetric latency is assumed: offset = hostRoomMs + rtt / 2 - recv.
        /// </summary>
        public void AddSample(long clientSendMs, long hostRoomMs, long clientRecvMs)
        {
            if (IsHost) return;
            var rtt = clientRecvMs - clientSendMs;
            if (rtt < 0) rtt = 0;
            LastRttMs = rtt;
            var offset = hostRoomMs + rtt / 2 - clientRecvMs;
            _samples[_sampleNext] = offset;
            _sampleNext = (_sampleNext + 1) % SampleCount;
            if (_sampleCount < SampleCount) _sampleCount++;
            _offsetMs = Median();
            HasEstimate = true;
        }

        /// <summary>
        /// A TIME_SYNC (or SESSION_INFO's tick): seeds the estimate when there is
        /// none, or resets it when the fine estimate has drifted past the tolerance.
        /// </summary>
        public void CoarseCheck(long hostRoomMs, long localRecvMs)
        {
            if (IsHost) return;
            var coarse = hostRoomMs - localRecvMs;
            if (HasEstimate && Math.Abs(coarse - _offsetMs) <= CoarseToleranceMs) return;
            ResetSamples();
            _offsetMs = coarse;
            HasEstimate = true;
        }

        void ResetSamples()
        {
            _sampleCount = 0;
            _sampleNext = 0;
        }

        long Median()
        {
            var n = _sampleCount;
            for (var i = 0; i < n; i++) _sorted[i] = _samples[i];
            for (var i = 1; i < n; i++)
            {
                var v = _sorted[i];
                var j = i - 1;
                while (j >= 0 && _sorted[j] > v)
                {
                    _sorted[j + 1] = _sorted[j];
                    j--;
                }
                _sorted[j + 1] = v;
            }
            if (n == 0) return _offsetMs;
            return (n & 1) == 1 ? _sorted[n / 2] : (_sorted[n / 2 - 1] + _sorted[n / 2]) / 2;
        }
    }
}
