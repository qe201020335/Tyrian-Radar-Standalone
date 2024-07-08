using System.Reflection;
using EFT;
using SPT.Reflection.Patching;

namespace Radar.Patches
{
    public class GameStartPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [PatchPostfix]
        private static void Postfix(GameWorld __instance)
        {
            Radar.Log.LogDebug("GameStartPatch:Postfix");
            
            Radar.Log.LogInfo("Game started, loading radar hud");
            __instance.gameObject.AddComponent<InRaidRadarManager>();
        }
    }
}