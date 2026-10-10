using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("mod state reset between missions")]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ResetGame))]
    internal static class GameManager_ResetGame_Patch
    {
        private static void Postfix()
        {
            ModState.ResetAll();
        }
    }
}
