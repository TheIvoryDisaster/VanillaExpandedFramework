using System;
using System.Collections.Generic;
using System.Linq;
using Outposts;
using Verse;

namespace RimWorld.Planet
{
	public class TimedForcedExitOutpost : WorldObjectComp
	{
		private int defaultDuration = 60000;
		private int ticksLeftToForceExitAndRemoveMap = -1;
		public bool ForceExitAndRemoveMapCountdownActive
		{
			get
			{
				return this.ticksLeftToForceExitAndRemoveMap >= 0;
			}
		}
		public string ForceExitAndRemoveMapCountdownTimeLeftString
		{
			get
			{
				if (!this.ForceExitAndRemoveMapCountdownActive)
				{
					return "";
				}
				return GetForceExitAndRemoveMapCountdownTimeLeftString(this.ticksLeftToForceExitAndRemoveMap);
			}
		}
		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look<int>(ref this.ticksLeftToForceExitAndRemoveMap, "ticksLeftToForceExitAndRemoveMapOutpost", -1, false);
		}
		public void ResetForceExitAndRemoveMapCountdown()
		{
			this.ticksLeftToForceExitAndRemoveMap = -1;
			if (parent.GetComponent<TimeoutComp>() != null)
				this.ticksLeftToForceExitAndRemoveMap = parent.GetComponent<TimeoutComp>().TicksLeft;
		}
		public void StartForceExitAndRemoveMapCountdown()
		{
			this.StartForceExitAndRemoveMapCountdown(defaultDuration);
		}
		public void StartForceExitAndRemoveMapCountdown(int duration)
		{
			this.ticksLeftToForceExitAndRemoveMap = duration;
		}
		public override string CompInspectStringExtra()
		{
			if (this.ForceExitAndRemoveMapCountdownActive)
			{
				return "OutpostForceExitAndRemoveMapCountdown".Translate(this.ForceExitAndRemoveMapCountdownTimeLeftString) + ".";
			}
			return null;
		}
		public override void CompTick()
		{
			Outpost mapParent = (Outpost)this.parent;
			if (this.ForceExitAndRemoveMapCountdownActive)
			{
				if (mapParent.HasMap)
				{
					this.ticksLeftToForceExitAndRemoveMap--;
					if (this.ticksLeftToForceExitAndRemoveMap <= 0)
					{	
						
						var pawns = mapParent.Map.mapPawns.AllPawns.ListFullCopy();
						ForceReformOutpost(mapParent, pawns);
						return;
					}
				}
				else
				{
					this.ticksLeftToForceExitAndRemoveMap = -1;
				}
			}
		}
		public static string GetForceExitAndRemoveMapCountdownTimeLeftString(int ticksLeft)
		{
			if (ticksLeft < 0)
			{
				return "";
			}
			return ticksLeft.ToStringTicksToPeriod(true, false, true, true);
		}
		
		public static void ForceReformOutpost(Outpost outpost, List<Pawn> source)
		{
			foreach (Pawn pawn in ((IEnumerable<Pawn>) source).Where<Pawn>((Func<Pawn, bool>) (pawn =>
			         {
				         Faction faction = ((Thing) pawn).Faction;
				         if (faction != null && faction.IsPlayer)
					         return true;
				         Faction hostFaction = pawn.HostFaction;
				         return hostFaction != null && hostFaction.IsPlayer;
			         })))
			{
				((Entity) pawn).DeSpawn((DestroyMode) 0);
				outpost.AddPawn(pawn);
			}
			outpost.RecachePawnTraits();
			Current.Game.DeinitAndRemoveMap(outpost.Map);
		}
	}
}
