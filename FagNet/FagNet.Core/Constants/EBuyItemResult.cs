namespace FagNet.Core.Constants
{
    public enum EBuyItemResult : byte
    {
        DBError = 0x00,
        NotEnoughMoney = 0x01,
        UnkownItem = 0x02,
        OK = 0x03,
    }
}
