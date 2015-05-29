using System;
using System.Xml.Serialization;
using System.IO;

namespace FagNet.Core.Utils
{
    public static class Config<T> where T : class, new()
    {
        public static T Load(string fileName)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.Combine(path, fileName);

            var serializer = new XmlSerializer(typeof(T));
            if (!File.Exists(path))
            {
                var instance = new T();
                Save(instance, fileName);
                return instance;
            }
            try
            {
                using (var fs = File.OpenRead(path))
                    return (T)serializer.Deserialize(fs);
            }
            catch
            {
                var instance = new T();
                Save(instance, fileName);
                return instance;
            }
        }

        public static void Save(T instance, string fileName)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.Combine(path, fileName);

            var serializer = new XmlSerializer(typeof(T));
            using (var fs = File.Create(path))
                serializer.Serialize(fs, instance);
        }
    }

    public class ConfigMySQL
    {
        [XmlAttribute]
        public string Server { get; set; }
        [XmlAttribute]
        public string User { get; set; }
        [XmlAttribute]
        public string Password { get; set; }
        [XmlAttribute]
        public string Database { get; set; }

        public ConfigMySQL()
        {
            Server = "127.0.0.1";
            User = "root";
            Password = "";
            Database = "";
        }
    }

    public class Remote
    {
        [XmlAttribute]
        public string Server { get; set; }
        [XmlAttribute]
        public ushort Port { get; set; }
        [XmlAttribute]
        public string Password { get; set; }
        [XmlAttribute]
        public string Binding { get; set; }
    }
}
