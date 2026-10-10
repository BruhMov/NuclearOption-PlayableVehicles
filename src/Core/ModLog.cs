using BepInEx.Logging;

namespace PlayableVehicles
{
    internal static class ModLog
    {
        private static ManualLogSource _log;

        public static void Init(ManualLogSource log)
        {
            _log = log;
        }

        public static void Info(string text) => _log?.LogInfo(text);
        public static void Warning(string text) => _log?.LogWarning(text);
        public static void Error(string text) => _log?.LogError(text);

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
