using System;
using BepInEx.Logging;

namespace PlayableVehicles
{
    internal static class ModLog
    {
        private static ManualLogSource _log;

        // Ограничение частоты отладочных сообщений: не чаще одного на ключ в DebugThrottle секунд,
        // иначе трассировка проверки целей (тикает каждый кадр превью/выстрела) затопит лог.
        private const float DebugThrottle = 2f;
        private static readonly System.Collections.Generic.Dictionary<string, DateTime> _lastDebug =
            new System.Collections.Generic.Dictionary<string, DateTime>();

        // DebugLogging привязывается в ModConfig.Bind (Plugin.Awake); до этого логи молчат.
        private static bool DebugEnabled => ModConfig.DebugLogging != null && ModConfig.DebugLogging.Value;

        public static void Init(ManualLogSource log)
        {
            _log = log;
        }

        public static void Info(string text) => _log?.LogInfo(text);
        public static void Warning(string text) => _log?.LogWarning(text);
        public static void Error(string text) => _log?.LogError(text);

        public static void Debug(string key, string text)
        {
            if (_log == null || !DebugEnabled) return;
            var now = DateTime.UtcNow;
            if (_lastDebug.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < DebugThrottle) return;
            _lastDebug[key] = now;
            _log.LogDebug("[DEBUG] " + text);
        }

        public static void Notify(string text)
        {
            Info("[VEHICLE] " + text);
            try
            {
                AircraftActionsReport report = SceneSingleton<AircraftActionsReport>.i;
                if (report != null && report.isActiveAndEnabled)
                {
                    report.ReportText(text, 3.5f);
                    return;
                }
                GameplayUI ui = SceneSingleton<GameplayUI>.i;
                if (ui != null) ui.GameMessage(text);
            }
            catch { }
        }
    }
}
