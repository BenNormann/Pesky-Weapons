namespace Pesky.Protocol
{
    /// <summary>
    /// A player's look in six bytes: body mesh (0 to 23), hair mesh (0 to 7),
    /// face texture (0 to 17) and three swatch indices (0 to 15) for the
    /// outfit's colourable palette cells. Rides on JOIN_REQUEST and on every
    /// PEER_SLOTS row, so late joiners get everyone's look with the table.
    /// </summary>
    public struct Appearance
    {
        public const int Bytes = 6;
        public const int BodyCount = 24;
        public const int HairCount = 8;
        public const int FaceCount = 18;
        public const int SwatchCount = 16;
        public const int ColorCells = 3;

        public byte body;
        public byte hair;
        public byte face;
        public byte color0;
        public byte color1;
        public byte color2;

        /// <summary>The first body and hair, the first face, and three distinct swatches.</summary>
        public static Appearance Default => new Appearance { body = 0, hair = 0, face = 0, color0 = 5, color1 = 9, color2 = 1 };

        /// <summary>The swatch index of a colourable cell (0 to 2).</summary>
        public byte Color(int cell)
        {
            switch (cell)
            {
                case 0: return color0;
                case 1: return color1;
                default: return color2;
            }
        }

        public void SetColor(int cell, byte swatch)
        {
            switch (cell)
            {
                case 0: color0 = swatch; break;
                case 1: color1 = swatch; break;
                default: color2 = swatch; break;
            }
        }

        /// <summary>Every field wrapped into its range, so a bad byte never indexes past a mesh list.</summary>
        public Appearance Clamped()
        {
            var a = this;
            a.body = (byte)(a.body % BodyCount);
            a.hair = (byte)(a.hair % HairCount);
            a.face = (byte)(a.face % FaceCount);
            a.color0 = (byte)(a.color0 % SwatchCount);
            a.color1 = (byte)(a.color1 % SwatchCount);
            a.color2 = (byte)(a.color2 % SwatchCount);
            return a;
        }

        public bool Same(in Appearance o) =>
            body == o.body && hair == o.hair && face == o.face && color0 == o.color0 && color1 == o.color1 && color2 == o.color2;

        public void Write(NetWriter w) => w.U8(body).U8(hair).U8(face).U8(color0).U8(color1).U8(color2);

        public static Appearance Read(NetReader r)
        {
            var a = new Appearance();
            a.body = r.U8();
            a.hair = r.U8();
            a.face = r.U8();
            a.color0 = r.U8();
            a.color1 = r.U8();
            a.color2 = r.U8();
            return a;
        }
    }
}
