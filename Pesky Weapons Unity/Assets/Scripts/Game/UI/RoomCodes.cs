using System.Text;
using Pesky.Session;

namespace Pesky.Game
{
    /// <summary>
    /// Owns what a "room code" is, and nothing else knows. Two shapes, for the two transports:
    ///
    ///  - browser (WebRTC through trystero): six characters from ABCDEFGHJKMNPQRSTUVWXYZ23456789,
    ///    with no I, L, O, 0 or 1, because people read these aloud over Discord. Ported from ATCK.
    ///  - desktop (TcpTransport): the code is the host's address, "192.168.1.5:7777". The host
    ///    opens on ":port" and shows the addresses TransportFactory lists; a joiner types one.
    ///
    /// The platform test is the exact complement of TransportFactory.ForPlatform's, so the code
    /// shown is always the code that transport understands.
    /// </summary>
    public static class RoomCodes
    {
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const int Length = 6;
        public const int MaxAddressLength = 64;

        /// <summary>True where a room is an address (editor and standalone), false in a WebGL player.</summary>
        public static bool IsAddressBased
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        public static string Generate()
        {
            char[] chars = new char[Length];
            for (int i = 0; i < Length; i++) chars[i] = Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)];
            return new string(chars);
        }

        /// <summary>Trimmed and upper-cased; never null.</summary>
        public static string Normalise(string raw)
        {
            return (raw ?? "").Trim().ToUpperInvariant();
        }

        public static bool IsAllowed(char c)
        {
            return Alphabet.IndexOf(char.ToUpperInvariant(c)) >= 0;
        }

        /// <summary>True for exactly six characters, all from the alphabet (after Normalise).</summary>
        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length) return false;
            for (int i = 0; i < code.Length; i++)
            {
                if (Alphabet.IndexOf(code[i]) < 0) return false;
            }
            return true;
        }

        /// <summary>The room a new host opens: a fresh code in the browser, this machine's listen port on a desktop.</summary>
        public static string NewHostRoom()
        {
            return IsAddressBased ? ":" + TransportFactory.DefaultPort : Generate();
        }

        /// <summary>Every address a desktop host can be reached on (LAN first, localhost last); empty in the browser.</summary>
        public static string[] HostAddresses()
        {
            return IsAddressBased ? TransportFactory.HostAddresses() : new string[0];
        }

        /// <summary>What the room page prints big: the code itself, or the best address to read out.</summary>
        public static string HostDisplay(string roomCode)
        {
            if (!IsAddressBased) return Normalise(roomCode);
            string[] addresses = HostAddresses();
            if (addresses != null && addresses.Length > 0) return addresses[0];
            return "localhost:" + TransportFactory.DefaultPort;
        }

        /// <summary>The host's other addresses on one line, or "" when there is only the one.</summary>
        public static string HostAlternatives()
        {
            string[] addresses = HostAddresses();
            if (addresses == null || addresses.Length < 2) return "";
            StringBuilder sb = new StringBuilder(80);
            for (int i = 1; i < addresses.Length; i++)
            {
                if (sb.Length > 0) sb.Append("     ");
                sb.Append(addresses[i]);
            }
            return "also reachable at   " + sb;
        }

        /// <summary>
        /// What a joiner typed, ready for NetSession.Start. Returns "" with a plain-English reason
        /// in <paramref name="error"/> when it cannot be used.
        /// </summary>
        public static string NormaliseJoin(string raw, out string error)
        {
            error = "";
            string text = (raw ?? "").Trim();
            if (!IsAddressBased)
            {
                string code = Normalise(text);
                if (!IsValid(code))
                {
                    error = "enter the " + Length + "-character room code";
                    return "";
                }
                return code;
            }

            if (text.Length == 0)
            {
                error = "enter the host's address, e.g. 192.168.1.5:" + TransportFactory.DefaultPort;
                return "";
            }
            if (text.Length > MaxAddressLength)
            {
                error = "that address is too long";
                return "";
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ' && text[i] != '\t') continue;
                error = "an address has no spaces in it";
                return "";
            }

            int colon = text.LastIndexOf(':');
            string host = colon >= 0 ? text.Substring(0, colon) : text;
            if (host.Length == 0)
            {
                error = "that address has no host in it";
                return "";
            }
            if (colon >= 0)
            {
                string port = text.Substring(colon + 1);
                if (port.Length == 0)
                {
                    error = "that address ends in a colon: add the port, e.g. :" + TransportFactory.DefaultPort;
                    return "";
                }
                int value;
                if (!int.TryParse(port, out value) || value < 1 || value > 65535)
                {
                    error = "the port after ':' must be a number from 1 to 65535";
                    return "";
                }
            }
            return text;
        }

        /// <summary>The grey line under the join field, which reads differently on each platform.</summary>
        public static string JoinHint()
        {
            return IsAddressBased
                ? "the host's address, e.g. 192.168.1.5:" + TransportFactory.DefaultPort + "   or   localhost:" + TransportFactory.DefaultPort
                : Length + " characters, no I L O 0 or 1";
        }
    }
}
