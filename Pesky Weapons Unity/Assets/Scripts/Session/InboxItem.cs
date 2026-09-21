namespace Pesky.Session
{
    /// <summary>
    /// One queued transport event: the kind, the peer id (or the self id for
    /// Ready, the message text for Error) and the payload for Message.
    /// </summary>
    public struct InboxItem
    {
        public InboxKind kind;
        public string peerId;
        public byte[] payload;
    }
}
