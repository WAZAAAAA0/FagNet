using FagNet.Core.Utils;

namespace FagNetGame
{
    public class GameConfig : SingletonBase<GameConfig>
    {
        public static void Load()
        {
            _instance = Config<GameConfig>.Load("game_config.xml");
        }
        public static void Save()
        {
            Config<GameConfig>.Save(_instance, "game_config.xml");
        }

        public string IP { get; set; }
        public ushort Port { get; set; }

        public Remote AuthRemote { get; set; }

        public ConfigMySQL MySQLAuth { get; set; }
        public ConfigMySQL MySQLGame { get; set; }

        public uint StartPEN { get; set; }
        public uint StartAP { get; set; }

        public GameConfig()
        {
            IP = "0.0.0.0";
            Port = 28008;

            AuthRemote = new Remote() { Binding = "pipe", Password = "hekker", Port = 27001, Server = "127.0.0.1" };

            MySQLAuth = new ConfigMySQL { Database = "auth" };
            MySQLGame = new ConfigMySQL { Database = "game" };

            StartPEN = 10000;
        }
    }
}
