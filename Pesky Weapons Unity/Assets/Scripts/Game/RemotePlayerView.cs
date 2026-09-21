using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Sim;
using TMPro;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// One other player, as seen from this machine. It keeps a short ring of the POSE samples the sim row
    /// took and renders 100 ms behind them: cubic Hermite on position using the streamed velocities (a body
    /// in free flight follows a curve, not a chord), slerp on rotation, and up to 250 ms of extrapolation
    /// when samples stop, then it holds. A teleport flagged pose, or a body swap, empties the ring and snaps.
    ///
    /// While the player is a free soul the pose moves this object's glowing orb. While they hold a weapon
    /// the pose moves THAT scene weapon (kinematic, colliders on, via WeaponBody.RemoteMove in FixedUpdate)
    /// and the orb hides. The name label floats above whichever it is and faces the view camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RemotePlayerView : MonoBehaviour
    {
        const float InterpDelay = 0.10f;
        const float MaxExtrapolate = 0.25f;
        const float SampleKeepSeconds = 2f;
        const float HermiteMaxSpan = 0.15f;

        struct Sample
        {
            public float time;
            public Vector3 pos;
            public Quaternion rot;
            public Vector3 vel;
        }

        [SerializeField] GameObject soulVisual;
        [SerializeField] TextMeshPro nameLabel;
        [Tooltip("How far above the body the name floats.")]
        [SerializeField] float labelHeight = 0.9f;

        readonly List<Sample> _ring = new List<Sample>(48);
        WeaponBody _weapon;
        Camera _view;
        uint _seenPose;
        bool _soulBody = true;
        bool _snap = true;
        bool _hasPose;
        Vector3 _pos;
        Quaternion _rot = Quaternion.identity;

        public byte Slot { get; private set; }

        public void Init(byte slot, string playerName, Camera viewCamera)
        {
            Slot = slot;
            _view = viewCamera;
            SetName(playerName);
            if (soulVisual != null) soulVisual.SetActive(false);
            if (nameLabel != null) nameLabel.gameObject.SetActive(false);
        }

        public void SetName(string playerName)
        {
            if (nameLabel == null) return;
            string text = string.IsNullOrEmpty(playerName) ? "Player " + (Slot + 1) : playerName;
            if (nameLabel.text != text) nameLabel.text = text;
        }

        /// <summary>Called every frame by the spawner with the slot's sim row and the weapon the authority says it drives.</summary>
        public void Feed(PlayerState row, WeaponBody weapon)
        {
            if (weapon != _weapon)
            {
                _weapon = weapon;
                _snap = true;
            }
            if (row.poseCount == _seenPose) return;
            _seenPose = row.poseCount;

            bool soul = (row.poseFlags & PoseFlags.Soul) != 0;
            if ((row.poseFlags & PoseFlags.Teleport) != 0 || soul != _soulBody)
            {
                _ring.Clear();
                _snap = true;
            }
            _soulBody = soul;

            Sample s = new Sample();
            s.time = Time.unscaledTime;
            s.pos = row.pos;
            s.rot = row.rot;
            s.vel = row.vel;
            _ring.Add(s);
            while (_ring.Count > 2 && s.time - _ring[0].time > SampleKeepSeconds) _ring.RemoveAt(0);

            if (_weapon != null && !soul) _weapon.SetRemoteState((row.poseFlags & PoseFlags.Animate) != 0, row.vel);
        }

        void Update()
        {
            _hasPose = Evaluate(Time.unscaledTime - InterpDelay, out _pos, out _rot);
            if (!_hasPose) return;

            bool showOrb = _soulBody || _weapon == null;
            if (soulVisual != null && soulVisual.activeSelf != showOrb) soulVisual.SetActive(showOrb);
            transform.position = _pos;

            if (nameLabel == null) return;
            if (!nameLabel.gameObject.activeSelf) nameLabel.gameObject.SetActive(true);
            nameLabel.transform.position = _pos + Vector3.up * labelHeight;
            if (_view != null) nameLabel.transform.rotation = _view.transform.rotation;
        }

        void FixedUpdate()
        {
            if (!_hasPose || _weapon == null || _soulBody) return;
            _weapon.RemoteMove(_pos, _rot, _snap);
            _snap = false;
        }

        bool Evaluate(float t, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero;
            rot = Quaternion.identity;
            int n = _ring.Count;
            if (n == 0) return false;

            Sample newest = _ring[n - 1];
            if (n == 1 || t >= newest.time)
            {
                // Out of samples: coast on the last velocity for a moment, then hold.
                float ahead = Mathf.Clamp(t - newest.time, 0f, MaxExtrapolate);
                pos = newest.pos + newest.vel * ahead;
                rot = newest.rot;
                return true;
            }
            if (t <= _ring[0].time)
            {
                pos = _ring[0].pos;
                rot = _ring[0].rot;
                return true;
            }

            int i = n - 2;
            while (i > 0 && _ring[i].time > t) i--;
            Sample a = _ring[i];
            Sample b = _ring[i + 1];
            float span = b.time - a.time;
            float k = span > 1e-5f ? Mathf.Clamp01((t - a.time) / span) : 1f;
            if (span > HermiteMaxSpan)
            {
                // A keepalive gap: the body was at rest, so the velocities say nothing about the path.
                pos = Vector3.Lerp(a.pos, b.pos, k);
            }
            else
            {
                float k2 = k * k, k3 = k2 * k;
                float h00 = 2f * k3 - 3f * k2 + 1f;
                float h10 = k3 - 2f * k2 + k;
                float h01 = -2f * k3 + 3f * k2;
                float h11 = k3 - k2;
                pos = h00 * a.pos + h10 * span * a.vel + h01 * b.pos + h11 * span * b.vel;
            }
            rot = Quaternion.Slerp(a.rot, b.rot, k);
            return true;
        }
    }
}
