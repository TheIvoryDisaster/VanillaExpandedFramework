﻿using System;
using System.Collections.Generic;
using RimWorld.Planet;

namespace RimWorld
{
    public class WorldObjectCompProperties_TimedForcedExitOutpost : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_TimedForcedExitOutpost()
        {
            this.compClass = typeof(TimedForcedExitOutpost);
        }

        public override IEnumerable<string> ConfigErrors(WorldObjectDef parentDef)
        {
            foreach (string text in base.ConfigErrors(parentDef))
            {
                yield return text;
            }
            IEnumerator<string> enumerator = null;
            if (!typeof(MapParent).IsAssignableFrom(parentDef.worldObjectClass))
            {
                yield return parentDef.defName + " has WorldObjectCompProperties_TimedForcedExit but it's not MapParent.";
            }
            yield break;
            yield break;
        }
    }
}