using StardewModdingAPI;

namespace StardewPresence.Framework.Services
{
    public static class ModLogger
    {
        public static bool IsDevMode
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        public static void Log(IMonitor monitor, string message, LogLevel level)
        {
            if (!IsDevMode && level != LogLevel.Warn && level != LogLevel.Error)
            {
                return;
            }

            monitor.Log(message, level);
        }

        public static void LogInfo(IMonitor monitor, string message) => Log(monitor, message, LogLevel.Info);
        public static void LogTrace(IMonitor monitor, string message) => Log(monitor, message, LogLevel.Trace);
        public static void LogWarn(IMonitor monitor, string message) => monitor.Log(message, LogLevel.Warn);
        public static void LogError(IMonitor monitor, string message) => monitor.Log(message, LogLevel.Error);
    }
}
