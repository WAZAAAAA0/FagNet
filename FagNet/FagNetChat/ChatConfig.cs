using FagNet.Core.Utils;

namespace FagNetChat
{
    public class ChatConfig : SingletonBase<ChatConfig>
    {
        public static void Load()
        {
            _instance = Config<ChatConfig>.Load("chat_config.xml");
        }
        public static void Save()
        {
            Config<ChatConfig>.Save(_instance, "chat_config.xml");
        }

        public string IP { get; set; }
        public ushort Port { get; set; }

        public Remote AuthRemote { get; set; }

        public ConfigMySQL MySQLAuth { get; set; }
        public ConfigMySQL MySQLGame { get; set; }

        public ChatConfig()
        {
            IP = "0.0.0.0";
            Port = 28012;

            AuthRemote = new Remote() { Binding = "pipe", Password = "hekker", Port = 27001, Server = "127.0.0.1" };

            MySQLAuth = new ConfigMySQL { Database = "auth" };
            MySQLGame = new ConfigMySQL { Database = "game" };
        }
    }
}
