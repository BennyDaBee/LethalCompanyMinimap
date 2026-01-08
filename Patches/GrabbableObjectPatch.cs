// ----------------------------------------------------------------------
// Copyright (c) Tyzeron. All Rights Reserved.
// Licensed under the GNU Affero General Public License, Version 3
// ----------------------------------------------------------------------

using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace LethalCompanyMinimap.Patches
{
    [HarmonyPatch(typeof(GrabbableObject))]
    internal class GrabbableObjectPatch
    {
        private static bool showTerminalCodes = false;

        private static readonly FieldInfo mapRadarTextField = AccessTools.Field(typeof(TerminalAccessibleObject), "mapRadarText");

        [HarmonyPatch(nameof(GrabbableObject.LateUpdate))]
        [HarmonyPostfix]
        static void LootVisibilityOnMapPatch(GrabbableObject __instance)
        {
            if (MinimapMod.minimapGUI == null) return;

            // Toggle loot visibility based on user's Minimap settings
            if (__instance != null && __instance.radarIcon != null && __instance.radarIcon.gameObject != null)
            {
                if (MinimapMod.minimapGUI.showLoots != __instance.radarIcon.gameObject.activeSelf)
                {
                    __instance.radarIcon.gameObject.SetActive(MinimapMod.minimapGUI.showLoots);
                }
            }

            // Toggle terminal code visibility based on user's Minimap settings
            if (showTerminalCodes != MinimapMod.minimapGUI.showTerminalCodes)
            {
                showTerminalCodes = MinimapMod.minimapGUI.showTerminalCodes;
                TerminalAccessibleObject[] taoObjecs = Object.FindObjectsOfType<TerminalAccessibleObject>();
                for (int i = 0; i < taoObjecs.Length; i++)
                {
                    TextMeshProUGUI mapRadarText = (TextMeshProUGUI)mapRadarTextField.GetValue(taoObjecs[i]);
                    if (mapRadarText != null && mapRadarText.gameObject != null)
                    {
                        mapRadarText.gameObject.SetActive(MinimapMod.minimapGUI.showTerminalCodes);
                    }
                    if (taoObjecs[i].mapRadarObject != null)
                    {
                        taoObjecs[i].mapRadarObject.SetActive(MinimapMod.minimapGUI.showTerminalCodes);
                    }
                }
            }
        }

    }
}
