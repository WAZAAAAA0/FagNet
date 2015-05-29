using System;
using System.IO;

namespace FagNet.Core.Utils
{
    public class Logger : IDisposable
    {
        protected StreamWriter _stream;
        protected object _sync = new object();
        private bool _isDisposed;

        public bool WriteToConsole { get; set; }

        ~Logger()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            if (_stream == null) return;
            try
            {
                _stream.Flush();
                _stream.Dispose();
                _stream = null;
            }
            catch
            { }
                    
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        public virtual void Load(string fileName)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            path = Path.Combine(path, fileName);
            var directoryName = Path.GetDirectoryName(path);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);

            _stream = new StreamWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            _stream.AutoFlush = true;
        }

        public virtual void Info(string format, params object[] str)
        {
            if (_isDisposed)
                return;
            var val = string.Format(format, str);
            var tmp = string.Format("{0} [INFO]: {1}", DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"), val);
            var tmp2 = string.Format("[INFO]: {0}", val);
            lock (_sync)
            {
                _stream.WriteLine(tmp);
                if (WriteToConsole)
                    Console.WriteLine(tmp2);
            }
        }

        public virtual void Warning(string format, params object[] str)
        {
            if (_isDisposed)
                return;
            var val = string.Format(format, str);
            var tmp = string.Format("{0} [WARNING]: {1}", DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"), val);
            var tmp2 = string.Format("[WARNING]: {0}", val);
            lock (_sync)
            {
                _stream.WriteLine(tmp);
                if (!WriteToConsole) return;
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine(tmp2);
                Console.ResetColor();
            }
        }

        public virtual void Error(string format, params object[] str)
        {
            if (_isDisposed)
                return;
            var val = string.Format(format, str);
            var tmp = string.Format("{0} [ERROR]: {1}", DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"), val);
            var tmp2 = string.Format("[ERROR]: {0}", val);
            lock (_sync)
            {
                _stream.WriteLine(tmp);
                if (!WriteToConsole) return;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(tmp2);
                Console.ResetColor();
            }
        }

        public virtual void Debug(string format, params object[] str)
        {
            if (_isDisposed)
                return;
            var val = string.Format(format, str);
            var tmp = string.Format("{0} [DEBUG]: {1}", DateTime.Now.ToString("HH:mm:ss dd.MM.yyyy"), val);
            var tmp2 = string.Format("[DEBUG]: {0}", val);
            lock (_sync)
            {
                _stream.WriteLine(tmp);
                if (!WriteToConsole) return;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(tmp2);
                Console.ResetColor();
            }
        }

        public virtual void Write(string format, params object[] str)
        {
            if (_isDisposed)
                return;
            lock (_sync)
            {
                var val = string.Format(format, str);
                _stream.WriteLine(val);
                if (WriteToConsole)
                    Console.WriteLine(val);
            }
        }
    }
}
