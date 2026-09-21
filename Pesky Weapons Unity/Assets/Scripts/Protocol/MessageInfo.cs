namespace Pesky.Protocol
{
    /// <summary>
    /// The router's lookup from a type byte to its routing kind. Owns the
    /// id-to-kind table, built from each message struct's own Id and Kind so
    /// the two can never disagree. M0 ids and unassigned bytes are Unknown.
    /// </summary>
    public static class MessageInfo
    {
        static readonly MsgKind[] Kinds = BuildTable();

        public static MsgKind KindOf(byte id) => Kinds[id];

        /// <summary>True for every gameplay id and FRAME; false for M0 ids and unassigned bytes.</summary>
        public static bool IsKnown(byte id) => Kinds[id] != MsgKind.Unknown;

        /// <summary>The type byte of a payload, or 0 for null or empty.</summary>
        public static byte IdOf(byte[] payload) =>
            payload != null && payload.Length > 0 ? payload[0] : (byte)0;

        static MsgKind[] BuildTable()
        {
            var t = new MsgKind[256];

            t[SessionInfoMsg.Id] = SessionInfoMsg.Kind;
            t[TimeSyncMsg.Id] = TimeSyncMsg.Kind;
            t[ClockPingMsg.Id] = ClockPingMsg.Kind;
            t[ClockPongMsg.Id] = ClockPongMsg.Kind;
            t[JoinRequestMsg.Id] = JoinRequestMsg.Kind;
            t[SnapshotPartMsg.Id] = SnapshotPartMsg.Kind;
            t[SessionPhaseMsg.Id] = SessionPhaseMsg.Kind;
            t[PeerSlotsMsg.Id] = PeerSlotsMsg.Kind;
            t[SessionEndMsg.Id] = SessionEndMsg.Kind;
            t[ResyncReqMsg.Id] = ResyncReqMsg.Kind;

            t[PossessReqMsg.Id] = PossessReqMsg.Kind;
            t[ReleaseReqMsg.Id] = ReleaseReqMsg.Kind;
            t[WeaponOwnerMsg.Id] = WeaponOwnerMsg.Kind;
            t[WeaponStateMsg.Id] = WeaponStateMsg.Kind;
            t[WeaponBrokenMsg.Id] = WeaponBrokenMsg.Kind;
            t[WeaponRespawnedMsg.Id] = WeaponRespawnedMsg.Kind;
            t[WeaponDamagedMsg.Id] = WeaponDamagedMsg.Kind;
            t[BatClaimMsg.Id] = BatClaimMsg.Kind;
            t[BatEventMsg.Id] = BatEventMsg.Kind;

            t[SwapReqMsg.Id] = SwapReqMsg.Kind;
            t[CompassBendReqMsg.Id] = CompassBendReqMsg.Kind;
            t[LabLayoutMsg.Id] = LabLayoutMsg.Kind;
            t[LabSwappedMsg.Id] = LabSwappedMsg.Kind;
            t[CompassTargetsMsg.Id] = CompassTargetsMsg.Kind;
            t[RoleAssignMsg.Id] = RoleAssignMsg.Kind;
            t[RoundResultMsg.Id] = RoundResultMsg.Kind;
            t[PlayerDownMsg.Id] = PlayerDownMsg.Kind;
            t[PlayerRespawnMsg.Id] = PlayerRespawnMsg.Kind;
            t[LegendMsg.Id] = LegendMsg.Kind;
            t[PadStrokeReqMsg.Id] = PadStrokeReqMsg.Kind;
            t[PadStrokeMsg.Id] = PadStrokeMsg.Kind;
            t[PadClearMsg.Id] = PadClearMsg.Kind;


            t[EnemyStateMsg.Id] = EnemyStateMsg.Kind;
            t[HitClaimMsg.Id] = HitClaimMsg.Kind;
            t[EnemyHealthMsg.Id] = EnemyHealthMsg.Kind;

            t[KitStateMsg.Id] = KitStateMsg.Kind;
            t[KitReqMsg.Id] = KitReqMsg.Kind;
            t[WorldResetMsg.Id] = WorldResetMsg.Kind;

            
t[MsgId.Frame] = MsgKind.Container;
            return t;
        }
    }
}
