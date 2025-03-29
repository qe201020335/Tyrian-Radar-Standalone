using System.Reflection;
using SPT.Reflection.Patching;
using EFT;
using JetBrains.Annotations;
using UnityEngine;

namespace Radar.Patches
{
    internal class PlayerOnMakingShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(Player).GetMethod("OnMakingShot", BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        static void PostFix(Player __instance, [NotNull] GInterface322 weapon, Vector3 force)
        {
            //UnityEngine.Debug.LogError($"Patched Player {__instance == null}");
            var radarGo = InRaidRadarManager._radarGo;
            if (radarGo == null)
            {
                return;
            }

            var radar = radarGo.GetComponent<HaloRadar>();
            if (radar != null && radar.inGame && __instance != null)
            {
                radar.UpdateFireTime(__instance.ProfileId);
            }
        }
    }
}
