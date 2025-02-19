﻿using HarmonyLib;
using System.Collections.Generic;
using Verse;

namespace KCSG
{
    [HarmonyPatch(typeof(Map), "ExposeData")]
    public static class Map_ExposeData_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Map __instance)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var kcsgSkyfallers = new List<Thing>();
                var allThings = __instance.listerThings.AllThings;
                for (int i = 0; i < allThings.Count; i++)
                {
                    var thing = allThings[i];
                    if (thing is KCSG_Skyfaller)
                        kcsgSkyfallers.Add(thing);
                }


                if (kcsgSkyfallers.Count > 0)
                {
                    for (int i = 0; i < kcsgSkyfallers.Count; i++)
                    {
                        Thing faller = kcsgSkyfallers[i];
                        if (faller is KCSG_Skyfaller skyfaller && skyfaller != null)
                        {
                            skyfaller.SaveImpact();
                        }
                    }
                }
            }
        }
    }
}