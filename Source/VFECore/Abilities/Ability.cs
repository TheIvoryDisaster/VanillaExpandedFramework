﻿namespace VFECore.Abilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using JetBrains.Annotations;
    using RimWorld;
    using UnityEngine;
    using Verse;
    using Verse.AI;
    using Verse.Sound;
    using UItils;

    public abstract class Ability : IExposable, ILoadReferenceable, ITargetingSource
    {
        public Pawn                 pawn;
        public Thing                holder;
        public Abilities.AbilityDef def;
        public int                  cooldown;

        public Abilities.Verb_CastAbility verb;

        public Hediff_Abilities hediff;

        public Hediff_Abilities Hediff => this.hediff == null && this.def.requiredHediff != null
                                              ? (this.hediff = (Hediff_Abilities)this.pawn?.health.hediffSet.GetFirstHediffOfDef(this.def.requiredHediff.hediffDef))
                                              : this.hediff;

        public List<AbilityExtension_AbilityMod> abilityModExtensions;

        public List<AbilityExtension_AbilityMod> AbilityModExtensions =>
            this.abilityModExtensions ?? (this.abilityModExtensions = this.def.modExtensions.Where(dme => dme is AbilityExtension_AbilityMod)
                                                                       .Cast<AbilityExtension_AbilityMod>().ToList());
        public Mote warmupMote;
        private Sustainer soundCast;
        public LocalTargetInfo selectedTarget = LocalTargetInfo.Invalid;
        public void Init()
        {
            if (this.verb == null)
                this.verb = (Abilities.Verb_CastAbility)Activator.CreateInstance(this.def.verbProperties.verbClass);
            this.verb.loadID      = this.GetUniqueLoadID() + "_Verb";
            this.verb.verbProps   = this.def.verbProperties;
            this.verb.verbTracker = this.pawn?.verbTracker;
            this.verb.caster      = this.pawn;
            this.verb.ability     = this;
            this.autoCast         = this.def.autocastPlayerDefault;
        }

        public virtual bool ShowGizmoOnPawn() =>
            this.pawn != null && this.pawn.IsColonistPlayerControlled && this.pawn.Drafted;

        public virtual bool IsEnabledForPawn(out string reason)
        {
            if (this.cooldown > Find.TickManager.TicksGame)
            {
                reason = "VFEA.AbilityDisableReasonCooldown".Translate(this.def.LabelCap, (this.cooldown - Find.TickManager.TicksGame).ToStringTicksToPeriod());
                return false;
            }

            foreach (AbilityExtension_AbilityMod abilityMod in this.AbilityModExtensions)
            {
                if (!abilityMod.IsEnabledForPawn(this, out string s))
                {
                    reason = s;
                    return false;
                }
            }

            reason = "VFEA.AbilityDisableReasonGeneral".Translate(this.pawn?.NameShortColored ?? this.holder.LabelCap);

            return this.def.Satisfied(this.Hediff);
        }

        public virtual float GetRangeForPawn() =>
            this.def.targetMode == AbilityTargetingMode.Self
                ? 0f
                : this.def.rangeStatFactors.Aggregate(this.def.range, (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value));

        public virtual float GetRadiusForPawn() =>
            this.def.radiusStatFactors.Aggregate(this.def.radius, (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value));
        public float GetAdditionalRadius() => this.def.GetModExtension<AbilityExtension_AdditionalRadius>().GetRadiusFor(this.pawn);
        public virtual float GetPowerForPawn() =>
            this.def.powerStatFactors.Aggregate(this.def.power, (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value));

        public virtual int GetCastTimeForPawn() =>
            Mathf.RoundToInt(this.def.castTimeStatFactors.Aggregate((float)this.def.castTime, (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value)));

        public virtual int GetCooldownForPawn() =>
            Mathf.RoundToInt(this.def.cooldownTimeStatFactors.Aggregate((float)this.def.cooldownTime,
                                                                        (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value)));

        public virtual int GetDurationForPawn() =>
            Mathf.RoundToInt(this.def.durationTimeStatFactors.Aggregate((float)this.def.durationTime,
                                                                        (current, statFactor) => current * (this.pawn.GetStatValue(statFactor.stat) * statFactor.value)));
        
        private List<Pair<Effecter, TargetInfo>> maintainedEffecters = new List<Pair<Effecter, TargetInfo>>();
        public virtual string GetDescriptionForPawn()
        {
            StringBuilder sb = new StringBuilder(this.def.description);

            sb.AppendLine();

            float rangeForPawn = this.GetRangeForPawn();
            if (rangeForPawn > 0f)
                sb.AppendLine($"{"Range".Translate()}: {rangeForPawn}".Colorize(Color.cyan));
            float radiusForPawn = this.GetRadiusForPawn();
            if (radiusForPawn > 0f)
                sb.AppendLine($"{"radius".Translate()}: {radiusForPawn}".Colorize(Color.cyan));
            float powerForPawn = this.GetPowerForPawn();
            if (powerForPawn > 0f)
                sb.AppendLine($"{"VFEA.AbilityStatsPower".Translate()}: {powerForPawn}".Colorize(Color.cyan));
            int castTimeForPawn = this.GetCastTimeForPawn();
            if (castTimeForPawn > 0)
                sb.AppendLine($"{"AbilityCastingTime".Translate()}: {castTimeForPawn.ToStringTicksToPeriodSpecific()}".Colorize(Color.cyan));
            int cooldownForPawn = this.GetCooldownForPawn();
            if (cooldownForPawn > 0)
                sb.AppendLine($"{"CooldownTime".Translate()}: {cooldownForPawn.ToStringTicksToPeriodSpecific()}".Colorize(Color.cyan));
            int durationForPawn = this.GetDurationForPawn();
            if (durationForPawn > 0)
                sb.AppendLine($"{"VFEA.AbilityStatsDuration".Translate()}: {durationForPawn.ToStringTicksToPeriodSpecific()}".Colorize(Color.cyan));
            else if (this.def.HasModExtension<AbilityExtension_Hediff>())
            {
                AbilityExtension_Hediff         extension            = this.def.GetModExtension<AbilityExtension_Hediff>();
                HediffCompProperties_Disappears propertiesDisappears = extension.hediff.CompProps<HediffCompProperties_Disappears>();
                if (propertiesDisappears != null)
                    sb.AppendLine($"{"VFEA.AbilityStatsDuration".Translate()}: {propertiesDisappears.disappearsAfterTicks.min.ToStringTicksToPeriodSpecific()} ~ {propertiesDisappears.disappearsAfterTicks.max.ToStringTicksToPeriodSpecific()}"
                                  .Colorize(Color.cyan));
            }

            foreach (AbilityExtension_AbilityMod modExtension in this.AbilityModExtensions)
            {
                string description = modExtension.GetDescription(this);
                if (description.Length > 1)
                    sb.AppendLine(description);
            }

            return sb.ToString();
        }

        public bool autoCast;

        public virtual bool AutoCast =>
            !this.pawn.IsColonistPlayerControlled || this.autoCast;

        public virtual bool CanAutoCast =>
            this.AutoCast && this.Chance > 0;

        public virtual float Chance =>
            this.def.Chance;

        public virtual void Tick()
        {
            for (int num2 = maintainedEffecters.Count - 1; num2 >= 0; num2--)
            {
                Effecter first = maintainedEffecters[num2].First;
                if (first.ticksLeft > 0)
                {
                    TargetInfo second = maintainedEffecters[num2].Second;
                    first.EffectTick(second, second);
                    first.ticksLeft--;
                }
                else
                {
                    first.Cleanup();
                    maintainedEffecters.RemoveAt(num2);
                }
            }
        }

        public virtual Gizmo GetGizmo()
        {
            Abilities.Command_Ability action = new Abilities.Command_Ability(this.pawn, this);
            return action;
        }

        public virtual void GizmoUpdateOnMouseover()
        {
            float radius;
            switch (this.def.targetMode)
            {
                case AbilityTargetingMode.Self:
                    radius = this.GetRadiusForPawn();
                    break;
                default:
                    radius = this.GetRangeForPawn();
                    break;
            }

            if (GenRadial.MaxRadialPatternRadius > radius && radius >= 1)
                GenDraw.DrawRadiusRing(this.pawn.Position, radius, Color.cyan);

            foreach (var extension in AbilityModExtensions)
            {
                extension.GizmoUpdateOnMouseover(this);
            }
        }
        public virtual void WarmupToil(Toil toil)
        {
            toil.initAction = delegate
            {
                def.warmupStartSound?.PlayOneShot(new TargetInfo(toil.actor.Position, toil.actor.Map));
            };
            toil.AddPreTickAction(delegate
            {
                if (def.warmupPreEndSound != null && this.verb.WarmupTicksLeft == def.warmupPreEndSoundTicks)
                {
                    def.warmupPreEndSound.PlayOneShot(new TargetInfo(toil.actor.Position, toil.actor.Map));
                }
                if (def.warmupMote != null)
                {
                    Vector3 vector = pawn.DrawPos;
                    vector += (verb.CurrentTarget.CenterVector3 - vector) * def.moteOffsetAmountTowardsTarget;
                    if (warmupMote == null || warmupMote.Destroyed)
                    {
                        warmupMote = MoteMaker.MakeStaticMote(vector, pawn.Map, def.warmupMote);
                    }
                    else
                    {
                        warmupMote.exactPosition = vector;
                        warmupMote.Maintain();
                    }
                }

                if (def.warmupSound != null)
                {
                    if (soundCast == null || soundCast.Ended)
                    {
                        soundCast = def.warmupSound.TrySpawnSustainer(SoundInfo.InMap(new TargetInfo(pawn.Position, pawn.Map), MaintenanceType.PerTick));
                    }
                    else
                    {
                        soundCast.Maintain();
                    }
                }
            });
            foreach (AbilityExtension_AbilityMod modExtension in this.AbilityModExtensions)
                modExtension.WarmupToil(toil);
        }

        public virtual void DoAction()
        {
            if (Event.current.button == 1)
            {
                this.autoCast = !this.autoCast;
            }
            else
            {
                if (this.def.targetMode == AbilityTargetingMode.Self)
                    this.CreateCastJob(this.pawn);
                else
                    Find.Targeter.BeginTargeting(this);
            }
        }

        // Careful with changing this, hook in mp compat.
        public virtual void CreateCastJob(LocalTargetInfo target)
        {
            foreach (var modExtension in this.AbilityModExtensions)
            {
                if (!modExtension.Valid(target, this, true))
                {
                    return;
                }
            }
            bool startAbilityJobImmediately = true;
            PreCast(target, ref startAbilityJobImmediately, delegate
            {
                StartAbilityJob(target);
            });
            if (startAbilityJobImmediately)
            {
                StartAbilityJob(target);
            }
        }

        public void StartAbilityJob(LocalTargetInfo target)
        {
            this.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
            Job job = JobMaker.MakeJob(def.jobDef ?? VFE_DefOf_Abilities.VFEA_UseAbility, target);
            this.pawn.GetComp<CompAbilities>().currentlyCasting = this;
            this.pawn.jobs.StartJob(job, JobCondition.InterruptForced);
        }
        public virtual void PreCast(LocalTargetInfo target, ref bool startAbilityJobImmediately, Action startJobAction)
        {
            foreach (AbilityExtension_AbilityMod modExtension in this.AbilityModExtensions)
                modExtension.PreCast(target, this, ref startAbilityJobImmediately, startJobAction);
        }

        public virtual void PreWarmupAction(LocalTargetInfo target)
        {
            foreach (AbilityExtension_AbilityMod modExtension in this.AbilityModExtensions)
                modExtension.PreWarmupAction(target, this);
        }
        public virtual void Cast(LocalTargetInfo target)
        {
            this.cooldown = Find.TickManager.TicksGame + this.GetCooldownForPawn();
            if (this.def.goodwillImpact != 0 && target.Thing is Pawn pawnTarget)
            {
                Pawn pawn = this.pawn;
                if (pawnTarget != null && !pawnTarget.IsSlaveOfColony)
                {
                    Faction homeFaction = pawnTarget.HomeFaction;
                    if (pawn.Faction == Faction.OfPlayer && homeFaction != null && !homeFaction.HostileTo(pawn.Faction) 
                        && (def.applyGoodwillImpactToLodgers || !pawnTarget.IsQuestLodger()) && !pawnTarget.IsQuestHelper())
                    {
                        Faction.OfPlayer.TryAffectGoodwillWith(homeFaction, def.goodwillImpact, canSendMessage: true, canSendHostilityLetter: true, HistoryEventDefOf.UsedHarmfulAbility);
                    }
                }
            }
            foreach (AbilityExtension_AbilityMod modExtension in this.AbilityModExtensions)
                modExtension.Cast(target, this);

            this.CheckCastEffects(target, out bool cast, out bool targetMote, out bool hediffApply);

            if (hediffApply)
                this.ApplyHediffs(target);

            if (cast)
                this.CastEffects(target);

            if (targetMote)
                this.TargetEffects(target);
        }

        public virtual void EndCastJob()
        {
            this.selectedTarget = IntVec3.Invalid;
        }
        public virtual void CastEffects(LocalTargetInfo targetInfo)
        {
            if (this.def.castFleck != null)
                MakeStaticFleck(this.pawn.DrawPos, this.pawn.Map, this.def.castFleck, this.def.castFleckScale, this.def.castFleckSpeed);
            if (this.def.fleckOnTarget != null)
                MakeStaticFleck(targetInfo.Thing.DrawPos, targetInfo.Thing.Map, this.def.fleckOnTarget, this.def.fleckOnTargetScale, this.def.fleckOnTargetSpeed);
            if (this.def.casterHediff != null)
                this.pawn.health.AddHediff(this.def.casterHediff);
            this.def.castSound?.PlayOneShot(new TargetInfo(this.pawn.Position, this.pawn.Map));
        }

        public static void MakeStaticFleck(IntVec3 cell, Map map, FleckDef fleckDef, float scale, float speed)
        {
            MakeStaticFleck(cell.ToVector3Shifted(), map, fleckDef, scale, speed);
        }
        public static void MakeStaticFleck(Vector3 loc, Map map, FleckDef fleckDef, float scale, float speed)
        {
            var data = FleckMaker.GetDataStatic(loc, map, fleckDef, scale);
            data.velocitySpeed = speed;
            Log.Message(fleckDef + " - Speed: " + speed);
            map.flecks.CreateFleck(data);
        }
        public void AddEffecterToMaintain(Effecter eff, IntVec3 pos, int ticks, Map map = null)
        {
            eff.ticksLeft = ticks;
            maintainedEffecters.Add(new Pair<Effecter, TargetInfo>(eff, new TargetInfo(pos, map ?? pawn.Map)));
        }
        public virtual void TargetEffects(LocalTargetInfo targetInfo)
        {
            if (!this.def.targetFlecks.NullOrEmpty())
                foreach (FleckDef fleck in this.def.targetFlecks)
                    FleckMaker.Static(targetInfo.Cell, this.pawn.Map, fleck);

            if (targetInfo.Pawn?.health.hediffSet.hediffs != null)
                foreach (Hediff hediff in targetInfo.Pawn.health.hediffSet.hediffs)
                {
                    if (hediff is HediffWithComps hediffWithComps)
                        foreach (HediffComp comp in hediffWithComps.comps)
                        {
                            if (comp is HediffComp_AbilityTargetReact compReact)
                                compReact.ReactTo(this);
                        }
                }
        }

        public virtual void ApplyHediffs(LocalTargetInfo targetInfo)
        {
            if (targetInfo.Pawn != null)
            {
                AbilityExtension_Hediff hediffExtension = this.def.GetModExtension<AbilityExtension_Hediff>();
                if (hediffExtension?.applyAuto ?? false)
                {
                    BodyPartRecord bodyPart = hediffExtension.bodyPartToApply != null 
                        ? pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == hediffExtension.bodyPartToApply)
                        : null;
                    Hediff localHediff = HediffMaker.MakeHediff(hediffExtension.hediff, targetInfo.Pawn, bodyPart);
                    if (localHediff is Hediff_Ability hediffAbility)
                    {
                        hediffAbility.ability = this;
                    }
                    if (hediffExtension.severity > float.Epsilon)
                        localHediff.Severity = hediffExtension.severity;
                    if (localHediff is HediffWithComps hwc)
                        foreach (HediffComp hediffComp in hwc.comps)
                            if (hediffComp is HediffComp_Ability hca)
                                hca.ability = this;
                    targetInfo.Pawn.health.AddHediff(localHediff);
                }
            }
        }

        public virtual void CheckCastEffects(LocalTargetInfo targetInfo, out bool cast, out bool target, out bool hediffApply) =>
            cast = target = hediffApply = true;

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref this.pawn, nameof(this.pawn));
            Scribe_Values.Look(ref this.cooldown, nameof(this.cooldown));
            Scribe_Defs.Look(ref this.def, nameof(this.def));
            Scribe_Deep.Look(ref this.verb, nameof(this.verb));
            Scribe_Values.Look(ref this.autoCast, nameof(this.autoCast));
            Scribe_TargetInfo.Look(ref this.selectedTarget, nameof(this.selectedTarget));

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.verb == null) // no idea how the hell that happens
                    this.verb = (Abilities.Verb_CastAbility)Activator.CreateInstance(this.def.verbProperties.verbClass);

                this.verb.loadID      = this.GetUniqueLoadID() + "_Verb";
                this.verb.verbProps   = this.def.verbProperties;
                this.verb.verbTracker = this.pawn?.verbTracker;
                this.verb.caster      = this.pawn;
                this.verb.ability     = this;
            }
        }

        public string GetUniqueLoadID() =>
            $"Ability_{this.def.defName}_{this.holder.GetUniqueLoadID()}";

        public virtual bool CanHitTarget(LocalTargetInfo target) => this.CanHitTarget(target, this.def.requireLineOfSight);

        public virtual bool CanHitTarget(LocalTargetInfo target, bool sightCheck)
        {
            foreach (var modExtension in this.AbilityModExtensions)
            {
                if (!modExtension.CanApplyOn(target, this))
                {
                    return false;
                }
            }

            if (target.IsValid && target.Cell.DistanceTo(this.pawn.Position) < this.GetRangeForPawn())
            {
                if ((this.targetParams.canTargetLocations && this.targetParams.CanTarget(new TargetInfo(target.Cell, this.Caster.Map))) ||
                    this.targetParams.CanTarget(target.ToTargetInfo(this.Caster.Map)))
                {
                    if (!sightCheck)
                        return true;

                    if (GenSight.LineOfSight(this.pawn.Position, target.Cell, this.pawn.Map))
                        return true;
                    List<IntVec3> tempSourceList = new List<IntVec3>();
                    ShootLeanUtility.LeanShootingSourcesFromTo(this.pawn.Position, target.Cell, this.pawn.Map, tempSourceList);
                    if (tempSourceList.Any(ivc => GenSight.LineOfSight(ivc, target.Cell, this.pawn.Map)))
                        return true;
                }
            }

            return false;
        }

        public virtual bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) =>
            this.CanHitTarget(target);

        public virtual void DrawHighlight(LocalTargetInfo target)
        {
            float range = this.GetRangeForPawn();
            if (GenRadial.MaxRadialPatternRadius > range && range >= 1)
                GenDraw.DrawRadiusRing(this.pawn.Position, range, Color.cyan);

            if (target.IsValid)
            {
                GenDraw.DrawTargetHighlight(target);

                float radius = this.GetRadiusForPawn();
                if (GenRadial.MaxRadialPatternRadius > radius && radius >= 1)
                    GenDraw.DrawRadiusRing(target.Cell, radius, Color.red);
            }
        }

        public virtual void OrderForceTarget(LocalTargetInfo target) =>
            this.CreateCastJob(target);

        public virtual void OnGUI(LocalTargetInfo target)
        {
            Texture2D icon = (!target.IsValid) ? TexCommand.CannotShoot : ((!(this.UIIcon != BaseContent.BadTex)) ? TexCommand.Attack : this.UIIcon);
            GenUI.DrawMouseAttachment(icon);
        }

        public bool      CasterIsPawn     => this.CasterPawn        != null;
        public bool      IsMeleeAttack    => this.GetRangeForPawn() < 6;
        public bool      Targetable       => this.def.targetMode    != AbilityTargetingMode.Self;
        public bool      MultiSelect      { get; }
        public bool      HidePawnTooltips { get; }
        public Thing     Caster           => this.pawn ?? this.holder;
        public Pawn      CasterPawn       => this.pawn;
        public Verb      GetVerb          => this.verb;
        public Texture2D UIIcon           => this.def.icon;

        public virtual TargetingParameters targetParams
        {
            get
            {
                TargetingParameters parameters = this.def.targetingParameters;

                if (this.def.targetMode == AbilityTargetingMode.Self)
                    parameters.targetSpecificThing = this.pawn;

                return parameters;
            }
        }

        public ITargetingSource DestinationSelector { get; }


        [DebugAction("Pawns", "Give ability...", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        [UsedImplicitly]
        private static void GiveAbility()
        {
            List<DebugMenuOption> list = new List<DebugMenuOption>();
            foreach (Abilities.AbilityDef def in DefDatabase<Abilities.AbilityDef>.AllDefs)
            {
                Abilities.AbilityDef abilityDef = def;

                list.Add(new
                             DebugMenuOption($"{(abilityDef.requiredHediff != null ? $"{abilityDef.requiredHediff.hediffDef.LabelCap} ({abilityDef.requiredHediff.minimumLevel}): " : string.Empty)}{abilityDef.LabelCap}",
                                             DebugMenuOptionMode.Tool, () =>
                                                                       {
                                                                           foreach (Pawn item in (from t in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell())
                                                                                                  where t is Pawn
                                                                                                  select t).Cast<Pawn>())
                                                                           {
                                                                               CompAbilities abilityComp = item.TryGetComp<CompAbilities>();
                                                                               if (abilityComp != null)
                                                                               {
                                                                                   abilityComp.GiveAbility(abilityDef);
                                                                                   DebugActionsUtility.DustPuffFrom(item);
                                                                               }
                                                                           }
                                                                       }));
            }

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(list));
        }
    }

    public class Ability_Blank : Ability
    {
    }
}