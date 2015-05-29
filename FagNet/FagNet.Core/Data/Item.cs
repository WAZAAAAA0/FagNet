using System;
using System.Globalization;
using FagNet.Core.Database;
using FagNet.Core.Utils;

namespace FagNet.Core.Data
{
    public class Item
    {
        public ulong ID { get; set; }
        public byte Category { get; set; }
        public byte SubCategory { get; set; }
        public ushort ItemID { get; set; }
        public byte ProductID { get; set; }
        public uint EffectID { get; set; }
        public uint SellPrice
        {
            get
            {
                var shopItem = GameDatabase.Instance.GetShopItem(Category, SubCategory, ItemID, ProductID);
                if(shopItem == null || shopItem.Type > 0)
                    return 0;

                if (TimeLeft == -1)
                    return 10000; // 10k for perm items
                
                var percentage = (float)TimeUsed / shopItem.Time;
                percentage = 1.0f - percentage;

                var price = shopItem.Price * 0.80f; // 20% less then buy price
                var sellPrice = price*percentage; // remove % based on TimeUsed
                return (uint) sellPrice;
            }
        }
        public long PurchaseTime { get; set; }
        public long ExpireTime { get; set; }
        public int Energy { get; set; }

        public int MaxEnergy
        {
            get
            {
                var shopItem = GameDatabase.Instance.GetShopItem(Category, SubCategory, ItemID, ProductID);
                return shopItem == null ? 0 : shopItem.Energy;
            }
        }
        public int TimeUsed { get; set; }
        public int TimeLeft
        {
            get
            {
                if (ExpireTime == -1)
                    return -1;

                int diff;
                if (Type > 0)
                {
                    diff = (int) (ExpireTime - HelperUtils.GetUnixTimestamp());
                    return diff <= 0 ? 0 : diff;
                }
                diff = (int)(ExpireTime - PurchaseTime);
                diff -= TimeUsed;
                return diff <= 0 ? 0 : diff;
            }
        }
        public byte Type { get; set; }

        public void SetupAPWeapon()
        {
            var shopItem = GameDatabase.Instance.GetShopItem(Category, SubCategory, ItemID, ProductID);
            if (shopItem == null) return;
            if (shopItem.Type <= 0) return;
            DateTime tmpExpire;
            switch (shopItem.Type)
            {
                case 1: // 1 day
                    tmpExpire = HelperUtils.UnixToDateTime(PurchaseTime).AddDays(1);
                    ExpireTime = HelperUtils.GetUnixTimestamp(tmpExpire);
                    break;

                case 2: // 7 days
                    tmpExpire = HelperUtils.UnixToDateTime(PurchaseTime).AddDays(7);
                    ExpireTime = HelperUtils.GetUnixTimestamp(tmpExpire);
                    break;

                case 3: // 30 days
                    tmpExpire = HelperUtils.UnixToDateTime(PurchaseTime).AddDays(30);
                    ExpireTime = HelperUtils.GetUnixTimestamp(tmpExpire);
                    break;
            }

            Type = shopItem.Type;
        }
    }
}
