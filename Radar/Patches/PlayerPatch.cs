using System.Reflection;
using Aki.Reflection.Patching;
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
            var radar = InRaidRadarManager._radarGo?.GetComponent<HaloRadar>();
            radar?.UpdateFireTime(__instance.ProfileId);
        }
    }
}
