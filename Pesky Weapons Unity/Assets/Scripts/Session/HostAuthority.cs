using System.Collections.Generic;
using Pesky.Protocol;
using Pesky.Session.Rules;
using Pesky.Session.Validators;

using Pesky.Sim;

namespace Pesky.Session
{
    /// <summary>
    /// The host's decision maker: an ordered list of IHostRule run once per
    /// sim tick and a table of IIntentValidator by message id. It holds no
    /// game state of its own beyond the id counters; every outcome leaves
    /// through the EventSink. Attached to the host's NetSession only.
    /// </summary>
    public sealed class HostAuthority
    {
        readonly List<IHostRule> _rules = new List<IHostRule>(8);
        readonly Dictionary<byte, IIntentValidator> _validators = new Dictionary<byte, IIntentValidator>(16);

        public HostIds Ids { get; } = new HostIds();
        public IReadOnlyList<IHostRule> Rules => _rules;

        public void AddRule(IHostRule rule)
        {
            if (rule != null) _rules.Add(rule);
        }

        public void SetValidator(byte msgId, IIntentValidator validator)
        {
            if (validator == null) _validators.Remove(msgId);
            else _validators[msgId] = validator;
        }

        public bool HasValidator(byte msgId) => _validators.ContainsKey(msgId);

        /// <summary>Runs every rule in order for one sim tick.</summary>
        public void Tick(WorldSim sim, uint tick, EventSink events)
        {
            for (var i = 0; i < _rules.Count; i++) _rules[i].Tick(sim, tick, events);
        }

        /// <summary>Routes one intent to its validator; false when nobody handles that id.</summary>
        public bool Handle(byte fromSlot, byte[] payload, WorldSim sim, uint tick, EventSink events)
        {
            var id = MessageInfo.IdOf(payload);
            if (!_validators.TryGetValue(id, out var validator)) return false;
            validator.Handle(fromSlot, payload, sim, tick, events);
            return true;
        }

        /// <summary>
        /// The single place a game's whole host-side behaviour is declared.
        /// Stage 1 (netcode port) registers only the clock: TIME_SYNC as a
        /// rule and the CLOCK_PONG answer as the CLOCK_PING validator. Every
        /// Pesky Weapons rule and validator (enemy spawn and AI, doors and
        /// plates, pickups, damage, respawn) is added here by the stage that
        /// writes it, and the registration order is meaningful: a rule reads
        /// what the rules above it already emitted this tick.
        /// NetSession.Start calls this only when Authority is still null, so a
        /// test can inject its own set.
        /// </summary>
public static HostAuthority CreateDefault()
        {
            var authority = new HostAuthority();
            // Weapons first: a weapon whose hp reached 0 last tick breaks before anything else reads it,
            // and a weapon whose owner left is freed before the enemy rows name it as a target.
            authority.AddRule(new WeaponRule());
            // Enemy rows next: the Game-side brains are read after the weapon facts of this tick are out.
            authority.AddRule(new EnemyStateRule());
            var clock = new ClockRule();
            authority.AddRule(clock);
            authority.SetValidator(MsgId.ClockPing, clock);

            var possession = new PossessValidator();
            authority.SetValidator(MsgId.PossessReq, possession);
            authority.SetValidator(MsgId.ReleaseReq, possession);
            authority.SetValidator(MsgId.HitClaim, new HitValidator());
            authority.SetValidator(MsgId.BatClaim, new BatValidator());
            authority.SetValidator(MsgId.KitReq, new KitValidator());
            // The labyrinth last: it reads the player facts the rules above already settled this tick,
            // and it is its own validator because the Mage's two powers need its secret table.
            var labyrinth = new LabyrinthRule();
            authority.AddRule(labyrinth);
            authority.SetValidator(MsgId.SwapReq, labyrinth);
            authority.SetValidator(MsgId.CompassBendReq, labyrinth);            // The shared scratch pad, last of all: it wipes itself when a round opens and rate-limits the
            // strokes. It decides nothing about the labyrinth and knows none of its secrets.
            var pad = new PadRule();
            authority.AddRule(pad);
            authority.SetValidator(MsgId.PadStrokeReq, pad);

            return authority;
        }
    }
}
