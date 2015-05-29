namespace FagNet.Core.Data
{
    public class Character
    {
        public byte Slot { get; set; }
        public uint Avatar { get; set; }

        public ulong[] Weapons { get; set; }
        public ulong[] Clothes { get; set; }
        public ulong Skill { get; set; }

        public Character()
        {
            Weapons = new ulong[3];
            Clothes = new ulong[7];
        }
    }
}
