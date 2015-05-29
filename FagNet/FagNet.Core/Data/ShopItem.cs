namespace FagNet.Core.Data
{
    public class ShopItem
    {
        public byte Type { get; set; }
        public byte Category { get; set; }
        public byte SubCategory { get; set; }
        public ushort ItemID { get; set; }
        public byte ProductID { get; set; }
        public uint Price { get; set; }
        public uint Cash { get; set; }
        public int Energy { get; set; }
        public int Time { get; set; }
    }
}
