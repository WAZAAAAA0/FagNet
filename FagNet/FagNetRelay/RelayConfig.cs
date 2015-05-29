using FagNet.Core.Utils;

namespace FagNetRelay
{
    public class RelayConfig : SingletonBase<RelayConfig>
    {
        public static void Load()
        {
            _instance = Config<RelayConfig>.Load("relay_config.xml");
        }
        public static void Save()
        {
            Config<RelayConfig>.Save(_instance, "relay_config.xml");
        }

        public string IP { get; set; }
        public ushort Port { get; set; }

        public ConfigMySQL MySQLAuth { get; set; }
        public ConfigMySQL MySQLGame { get; set; }

        public RelayConfig()
        {
            IP = "0.0.0.0";
            Port = 28013;

            MySQLAuth = new ConfigMySQL { Database = "auth" };
            MySQLGame = new ConfigMySQL { Database = "game" };
        }
    }
}
