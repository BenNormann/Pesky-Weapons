using Pesky.Protocol;
using Pesky.Sim;
using UnityEngine;

namespace Pesky.Game
{
    /// <summary>
    /// The netcode half of GoblinBrain. The NavMeshAgent brain runs ONLY on the host. There it also
    /// produces its ENEMY_STATE row (NetCollect). On a client the very same prefab is a PUPPET: the agent
    /// is off, nothing is perceived or decided, and the body, state, hit points, markers and blade are
    /// drawn from the sim's enemy row, eased over one row interval so it renders about 100 ms behind.
    /// </summary>
    public sealed partial class GoblinBrain
    {
        const float PuppetLerpSeconds = 0.1f;
        const float PuppetSnapDistance = 4f;

        bool _puppet;
        bool _hasSample;
        uint _seenSamples;
        Vector3 _fromPos, _toPos;
        float _fromYaw, _toYaw, _lerpT;

        bool _sentOnce;
        Vector3 _sentPos;
        float _sentYaw, _sentHp, _sentShield, _sentTime;
        byte _sentState;
        EnemyFlags _sentFlags;
        ushort _sentTarget;

        /// <summary>True on a client: this goblin is a view of the host's row.</summary>
        public bool IsPuppet { get { return _puppet; } }

        /// <summary>Authority only, on a client, before the first Update: switch the brain off.</summary>
        public void SetPuppet(bool on)
        {
            _puppet = on;
            if (on && agent != null) agent.enabled = false;
        }

        // ---------------------------------------------------------------- host: the row

        EnemyStateMsg.Row BuildRow()
        {
            EnemyStateMsg.Row row = new EnemyStateMsg.Row();
            row.enemyId = (ushort)id;
            row.kind = (byte)(def != null ? def.id : 0);
            row.pos = transform.position;
            row.yaw = transform.eulerAngles.y;
            row.hp = _hp;
            row.shieldHp = _shieldHp;
            row.state = (byte)_state;
            EnemyFlags flags = EnemyFlags.None;
            if (_state != State.Dead) flags |= EnemyFlags.Alive;
            if (IsHostile) flags |= EnemyFlags.Hostile;
            if (_awake) flags |= EnemyFlags.Awake;
            if (_carried != null) flags |= EnemyFlags.Carrying;
            row.flags = flags;
            row.targetWeaponId = _target != null ? (ushort)_target.Id : Wire.NoId;
            return row;
        }

        /// <summary>
        /// Host only. True when this goblin has a row worth sending now: always when its state, flags,
        /// target or hit points changed; on the 10 Hz interval when it moved; and once a second regardless.
        /// </summary>
        public bool NetCollect(bool intervalDue, out EnemyStateMsg.Row row, out bool stateChanged)
        {
            row = BuildRow();
            stateChanged = !_sentOnce || row.state != _sentState || row.flags != _sentFlags
                || row.targetWeaponId != _sentTarget
                || !Mathf.Approximately(row.hp, _sentHp) || !Mathf.Approximately(row.shieldHp, _sentShield);
            bool send = stateChanged;
            if (!send && intervalDue)
            {
                bool moved = (row.pos - _sentPos).sqrMagnitude > 0.0001f
                    || Mathf.Abs(Mathf.DeltaAngle(row.yaw, _sentYaw)) > 1f;
                send = moved || Time.unscaledTime - _sentTime > 1f;
            }
            if (!send) return false;

            _sentOnce = true;
            _sentPos = row.pos;
            _sentYaw = row.yaw;
            _sentHp = row.hp;
            _sentShield = row.shieldHp;
            _sentState = row.state;
            _sentFlags = row.flags;
            _sentTarget = row.targetWeaponId;
            _sentTime = Time.unscaledTime;
            return true;
        }

        // ---------------------------------------------------------------- client: the puppet

        void PuppetUpdate()
        {
            // SetPuppet can run before this goblin's own Awake found its agent, so make sure here.
            if (agent != null && agent.enabled) agent.enabled = false;
            float dt = Time.deltaTime;
            EnemyState row = authority != null ? authority.EnemyRow(id) : null;
            if (row != null && row.samples != _seenSamples)
            {
                _seenSamples = row.samples;
                bool snap = !_hasSample
                    || (row.pos - transform.position).sqrMagnitude > PuppetSnapDistance * PuppetSnapDistance;
                _hasSample = true;
                _fromPos = snap ? row.pos : transform.position;
                _fromYaw = snap ? row.yaw : transform.eulerAngles.y;
                _toPos = row.pos;
                _toYaw = row.yaw;
                _lerpT = 0f;
                TakeRowState(row);
            }
            if (_state == State.Dead) return;

            _stateTime += dt;
            if (_hasSample)
            {
                _lerpT = Mathf.Min(1f, _lerpT + dt / PuppetLerpSeconds);
                transform.position = Vector3.Lerp(_fromPos, _toPos, _lerpT);
            }

            if (_state == State.Strike)
            {
                float half = def != null ? def.strikeHalfAngleDeg : 80f;
                float span = def != null ? def.strikeSeconds : 0.25f;
                float k = span > 0.001f ? Mathf.Clamp01(_stateTime / span) : 1f;
                _sweepAngle = Mathf.Lerp(-half, half, k) * _strikeSide;
            }

            // Same bookkeeping as the host: a hostile goblin after MY weapon keeps my IN COMBAT tag up.
            if (authority != null)
                authority.SetTargeting(this, _hostile && _target != null ? _target.Possessor as PlayerSoul : null);

            UpdateLook();
            if (_hasSample && _state != State.Strike)
                transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(_fromYaw, _toYaw, _lerpT), 0f);
        }

        void TakeRowState(EnemyState row)
        {
            _hp = row.hp;
            if (!Mathf.Approximately(_shieldHp, row.shieldHp))
            {
                _shieldHp = row.shieldHp;
                ShowShield(_shieldHp > 0f);
            }
            _hostile = row.Hostile;
            _awake = (row.flags & EnemyFlags.Awake) != 0;
            _target = authority != null ? authority.GetWeapon(row.targetWeaponId) : null;

            State next = (State)row.state;
            if (next == State.Dead)
            {
                if (authority != null) authority.SetTargeting(this, null);
                Kill();
                return;
            }
            if (next != _state)
            {
                if (_state == State.Strike) _strikeSide = -_strikeSide;
                SetState(next);
            }
            if (next == State.Curious || next == State.Fetch) ShowMarker("?", curiousColor);
            else if (next == State.Alarmed) ShowMarker("!", alarmColor);
            else if (next == State.Asleep) ShowMarker("z", sleepColor);
            else HideMarker();
        }
    }
}
