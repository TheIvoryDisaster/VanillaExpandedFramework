﻿using System.Linq;
using KCSG;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Outposts
{
    public partial class Outpost
    {
        public override MapGeneratorDef MapGeneratorDef
        {
            get
            {
                if (def.GetModExtension<CustomGenOption>() is { } cGen && (cGen.chooseFromlayouts.Count > 0 || cGen.chooseFromSettlements.Count > 0))
                    return DefDatabase<MapGeneratorDef>.GetNamed("KCSG_WorldObject_Gen");
                return MapGeneratorDefOf.Base_Faction;
            }
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();

            foreach (var pawn in Map.mapPawns.AllPawns.Where(p => p.RaceProps.Humanlike).ToList()) pawn.Destroy();

            foreach (var occupant in occupants) GenPlace.TryPlaceThing(occupant, Map.Center, Map, ThingPlaceMode.Near);
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            if (!Map.mapPawns.FreeColonists.Any())
            {
                occupants.Clear();
                Find.LetterStack.ReceiveLetter("Outposts.Letters.Lost.Label".Translate(), "Outposts.Letters.Lost.Text".Translate(Name),
                    LetterDefOf.NegativeEvent);
                alsoRemoveWorldObject = true;
                this.CombatResolved = true;
                return true;
            }

            var pawns = Map.mapPawns.AllPawns.ListFullCopy();
            if (!pawns.Any(p => p.HostileTo(Faction.OfPlayer)) && !this.CombatResolved)
            {
                this.CombatResolved = true;
                occupants.Clear();
                Find.LetterStack.ReceiveLetter("Outposts.Letters.BattleWon.Label".Translate(), "Outposts.Letters.BattleWon.Text".Translate(Name),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(Gen.YieldSingle(this)));
                alsoRemoveWorldObject = false;
                this.GetComponent<TimedForcedExitOutpost>().StartForceExitAndRemoveMapCountdown();
                return false;
            }

            alsoRemoveWorldObject = false;
            return false;
        }
    }
}