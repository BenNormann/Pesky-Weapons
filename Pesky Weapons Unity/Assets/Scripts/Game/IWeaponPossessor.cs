namespace Pesky.Game
{
    /// <summary>Whoever is driving a weapon (the local soul now, a remote player after the netcode port).</summary>
    public interface IWeaponPossessor
    {
        /// <summary>The possessed weapon dealt or took damage: restart the in-combat timer.</summary>
        void NotifyCombat();
    }
}
