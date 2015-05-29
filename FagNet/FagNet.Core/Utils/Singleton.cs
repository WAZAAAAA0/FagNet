using System;

namespace FagNet.Core.Utils
{
    public static class Singleton<T> where T : class, new()
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() => new T());
        public static T Instance { get { return _instance.Value; } }
    }

    public class SingletonBase<T> where T : class, new()
    {
        protected static T _instance = new T();
        public static T Instance { get { return _instance; } }
    }
}
