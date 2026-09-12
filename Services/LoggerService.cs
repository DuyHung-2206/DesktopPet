using System;
using System.IO;

namespace DesktopPet.Services
{
    public static class LoggerService
    {
        private static readonly object _lock = new();
        private static readonly string _logDir;
        private static readonly string _logFile;

        static LoggerService()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _logDir = Path.Combine(appData, "DesktopPetWorld", "logs");
                Directory.CreateDirectory(_logDir);
                _logFile = Path.Combine(_logDir, "game.log");

                // Giới hạn kích thước file log nếu vượt quá 5MB
                if (File.Exists(_logFile))
                {
                    var fi = new FileInfo(_logFile);
                    if (fi.Length > 5 * 1024 * 1024)
                    {
                        var oldFile = Path.Combine(_logDir, $"game_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        File.Move(_logFile, oldFile);
                    }
                }
            }
            catch
            {
                _logDir = Path.GetTempPath();
                _logFile = Path.Combine(_logDir, "desktop_pet_game.log");
            }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warn(string message) => Log("WARN", message);
        public static void Error(string message, Exception? ex = null)
        {
            var msg = ex != null ? $"{message} | Exception: {ex.Message}\n{ex.StackTrace}" : message;
            Log("ERROR", msg);
        }
        public static void Debug(string message) => Log("DEBUG", message);

        private static void Log(string level, string message)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
                System.Diagnostics.Debug.WriteLine(entry);

                lock (_lock)
                {
                    File.AppendAllText(_logFile, entry + Environment.NewLine);
                }
            }
            catch
            {
                // Không bao giờ throw crash từ logger
            }
        }
    }
}
