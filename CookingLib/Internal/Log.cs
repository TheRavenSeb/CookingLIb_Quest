using MelonLoader;

namespace CookingLib
{
    internal static class Log
    {
        static MelonLogger.Instance logger;

        internal static void Init(MelonLogger.Instance instance) => logger = instance;

        internal static void Info(string message) => logger?.Msg(message);

        internal static void Warn(string message) => logger?.Warning(message);

        internal static void Error(string message) => logger?.Error(message);
    }
}