namespace Pesky.Session
{
    /// <summary>What an inbox item carries: a transport lifecycle event or a payload from a peer.</summary>
    public enum InboxKind : byte
    {
        Ready = 0,
        Error = 1,
        PeerJoined = 2,
        PeerLeft = 3,
        Message = 4,
    }
}
