using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using System.Text;
using HugsLib;
using HugsLib.Settings;
using HarmonyLib;
using UnityEngine;

namespace ArmorIsUncomfortable
{
    public class AIUCore : ModBase
    {
        public override string ModIdentifier { get { return "ArmorIsUncomfortable"; } }

        static private SettingHandle<int> setting_HoursUntilUncomfortable;
        static public int hoursUntilUncomfortable { get { return setting_HoursUntilUncomfortable.Value; } }

        static private SettingHandle<int> setting_HoursUntilComfortable;
        static public int hoursUntilComfortable { get { return setting_HoursUntilComfortable.Value; } }

        //static private SettingHandle<float> setting_DiscomfortRecoveryMultiplier;
        //static public float discomfortRecoveryMultiplier { get { return (setting_DiscomfortRecoveryMultiplier.Value); } }

        static private SettingHandle<float> setting_GlobalDiscomfortMultiplier;
        static public float globalDiscomfortMultiplier { get { return (setting_GlobalDiscomfortMultiplier.Value); } }

        static private SettingHandle<bool> setting_DisableThought;
        static public bool disableThought { get { return (setting_DisableThought.Value); } }

        public override void DefsLoaded()
        {

            base.DefsLoaded();
            setting_HoursUntilUncomfortable = Settings.GetHandle<int>("AIU_setting_HoursUntilUncomfortable", "AIU_HoursUntilUncomfortable_title".Translate(), "AIU_HoursUntilUncomfortable_desc".Translate(), 48, Validators.IntRangeValidator(1, 1440));
            //setting_DiscomfortRecoveryMultiplier = Settings.GetHandle<float>("AIU_setting_DiscomfortRecoveryMultiplier", "AIU_DiscomfortRecoveryMultiplier_title".Translate(), "AIU_DiscomfortRecoveryMultiplier_desc".Translate(), 0.33f, Validators.FloatRangeValidator(0.01f, 128f));
            setting_GlobalDiscomfortMultiplier = Settings.GetHandle<float>("AIU_setting_GlobalDiscomfortMultiplier", "AIU_GlobalDiscomfortMultiplier_title".Translate(), "AIU_GlobalDiscomfortMultiplier_desc".Translate(), 1f, Validators.FloatRangeValidator(0.01f, 128f));
            setting_HoursUntilComfortable = Settings.GetHandle<int>("AIU_setting_HoursUntilComfortable", "AIU_HourUntilComfortable_title".Translate(), "AIU_HoursUntilComfortable_desc".Translate(), 8, Validators.IntRangeValidator(1, 1440));
            setting_DisableThought = Settings.GetHandle<bool>("AIU_setting_DisableThought", "AIU_DisableThought_title".Translate(), "AIU_DisableThought_desc".Translate(), false);
            List<BodyPartGroupDef> bodyPartGroups = DefDatabase<BodyPartGroupDef>.AllDefsListForReading;
            BodyDef defaultBody = BodyDefOf.Human;
            for (int i = 0; i < bodyPartGroups.Count; i++)
            {
                BodyPartGroupDef groupDef = bodyPartGroups[i];
                if (groupDef.modExtensions == null)
                {
                    groupDef.modExtensions = new List<DefModExtension> { };
                }
                if (!groupDef.HasModExtension<ApparelDiscomfortExtension>())
                {
                    ApparelDiscomfortExtension extension = new ApparelDiscomfortExtension();
                    List<BodyPartRecord> allDefaultParts = defaultBody.AllParts;
                    for (int i2 = 0; i2 < allDefaultParts.Count; i2++)
                    {
                        if (!allDefaultParts[i2].IsInGroup(groupDef))
                        {
                            continue;
                        }
                        if (allDefaultParts[i2].depth == BodyPartDepth.Inside)
                        {
                            continue;
                        }
                        extension.baseDiscomfort += allDefaultParts[i2].coverageAbs;
                    }
                    groupDef.modExtensions.Add(extension);
                }
            }
            List<ThingDef> allHumanlikes = DefDatabase<ThingDef>.AllDefsListForReading.FindAll(x => x.race != null && x.race.Humanlike);
            foreach (ThingDef humanlike in allHumanlikes)
            {
                if (humanlike.comps == null)
                {
                    //Logger.Message("Adding empty list to " + humanlike.defName);
                    humanlike.comps = new List<CompProperties>() { };
                }
             
                humanlike.comps.Add(new CompProperties(typeof(DiscomfortComp)));
            }
        }

    }
    //At discomfort 1.0, it takes 1 day to reach the first level of discomfort
    //It take 8 hours to remove one level of discomfort.
    //The need interval is 150. There are 400 updates in 1 day, 12000 in two in-game months.
    public class Need_ApparelComfort : Need
    {
        public override void NeedInterval()
        {
            DiscomfortComp discomfortComp = DiscomfortUtility.Comp(this.pawn);
            if (discomfortComp == null)
            {
                return;
            }
            if (discomfortComp.currentlyUncomfortable == true)
            {
                //As discomfort gets higher, the rate of discomfort increase slows, but it still should be possible to reach 0f.
                float discomfortPace;
                discomfortPace = Mathf.Max(1f, (CurLevel + 0.1f));
                CurLevel -= ((discomfortComp.discomfort * 0.1f) / (16.5f * AIUCore.hoursUntilUncomfortable) * discomfortPace);
            }
            else
            {
                CurLevel += (1f / AIUCore.hoursUntilComfortable) * (150f / 2500f);
            }
            //Every 150 ticks, apparel comfort is decreased by (0.1 * current discomfort) / (400 * discomfort multiplier)
        }

    }

    public class DiscomfortComp : ThingComp
    {
        public override void Initialize(CompProperties props)
        {
            pawn = parent as Pawn;
            cachedDiscomfort = DiscomfortUtility.CalculateDiscomfort(pawn);
            /*if (cachedDiscomfort >= 1f)
            {
                tickWhenUncomfortable = Find.TickManager.TicksGame;
                currentlyUncomfortable = true;
            }*/
        }

        public override void PostExposeData()
        {
            //Scribe_Values.Look(ref tickWhenUncomfortable, "AIUDiscomfortComp_tickWhenUncomfortable");
            Scribe_Values.Look(ref currentlyUncomfortable, "AIUDiscomfortComp_currentlyUncomfortable");
        }

        public void Refresh()
        {
            cachedDiscomfort = DiscomfortUtility.CalculateDiscomfort(pawn);
            if (currentlyUncomfortable && cachedDiscomfort < 1f)
            {
                currentlyUncomfortable = false;
            }
            else if (currentlyUncomfortable == false && cachedDiscomfort >= 1f)
            {
                currentlyUncomfortable = true;
            }
        }

        public void Update()
        {
            if (cachedDiscomfort < 0f)
            {
                Refresh();
            }
            /*
            if (!currentlyUncomfortable && tickWhenUncomfortable < Find.TickManager.TicksGame)
            {
                tickWhenUncomfortable += 100 + Mathf.RoundToInt(100f * AIUCore.discomfortRecoveryMultiplier);
                //Log.Message(pawn.Name.ToString() + ": " + tickWhenUncomfortable.ToString());
            }*/
        }

        public void Reset()
        {
            cachedDiscomfort = -1f;
            //tickWhenUncomfortable = Find.TickManager.TicksGame;
            currentlyUncomfortable = false;
        }

        //public int tickWhenUncomfortable;
        public bool currentlyUncomfortable;

        private float cachedDiscomfort = -1f;
        public float discomfort { get
            {
                if (cachedDiscomfort < 0f)
                {
                    cachedDiscomfort = DiscomfortUtility.CalculateDiscomfort(pawn);
                }
                return cachedDiscomfort;
            } }

        public int severity { get
            {
                if (!currentlyUncomfortable)
                {
                    return 0;
                }
                Need_ApparelComfort need = DiscomfortUtility.Need(pawn);
                if (need == null)
                {
                    return 0;
                }
                return DiscomfortUtility.CalculateSeverity(need.CurLevelPercentage);

            } }

        private Pawn pawn;
    }

    public static class DiscomfortUtility
    {
        public static DiscomfortComp Comp(Pawn pawn)
        {
            return (pawn.GetComp<DiscomfortComp>());
        }
        public static Need_ApparelComfort Need(Pawn pawn)
        {
            return (pawn.needs.TryGetNeed<Need_ApparelComfort>());
        }
        public static bool ShouldRecordDiscomfort(Pawn pawn)
        {
            return (pawn.IsColonist && pawn.Spawned && !pawn.Dead);
        }
        public static float CalculateDiscomfort(Pawn pawn)
        {
            List<Apparel> allApparel = pawn.apparel.WornApparel;
            float discomfort = 0f;
            foreach (Apparel item in allApparel)
            {
                discomfort += item.GetStatValue(StatDefOfAIU.ApparelDiscomfort);
            }
            return discomfort;
        }
        public static int CalculateSeverity(float num)
        {
            float num2 = ((1f - num) / 0.1f);
            return (Mathf.FloorToInt(num2));
        }

    }

    public class StatPart_CachedDiscomfort : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing)
            {
                Pawn pawn = req.Thing as Pawn;
                DiscomfortComp comp = DiscomfortUtility.Comp(pawn);
                if (comp == null)
                {
                    Log.Error("[ArmorIsUncomfortable]Couldn't find a DiscomfortComp on " + pawn.Name.ToStringFull);
                }
                else
                {
                    val = comp.discomfort;
                }
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            StringBuilder explanation = new StringBuilder();
            if (req.HasThing)
            {
                Pawn pawn = req.Thing as Pawn;
                List<Apparel> allApparel = pawn.apparel.WornApparel;
                foreach (Apparel item in allApparel)
                {
                    explanation.AppendLine(item.LabelCap + ": +" + item.GetStatValue(StatDefOfAIU.ApparelDiscomfort).ToStringByStyle(ToStringStyle.FloatTwo));
                }
            }
            return explanation.ToString();
        }
    }
    public class StatPart_Discomfort : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.HasThing)
            {
                Thing thing = req.Thing as Apparel;
                float armorPenalty = 0f;
                foreach(ArmorFactor factor in armorFactors)
                {
                    armorPenalty += thing.GetStatValue(factor.armorType) * factor.weight;
                }
                float weightPenalty = thing.GetStatValue(StatDefOf.Mass);
                if (val == 0f)
                {
                    val = armorPenalty * weightPenalty;
                }
                else
                {
                    val *= armorPenalty * weightPenalty;
                }
                List<BodyPartGroupDef> allBodyParts = thing.def.apparel.bodyPartGroups;
                float baseDiscomfort = 0f;
                for (int i = 0; i < allBodyParts.Count; i++)
                {
                    BodyPartGroupDef bodyPart = allBodyParts[i];
                    baseDiscomfort += bodyPart.GetModExtension<ApparelDiscomfortExtension>().baseDiscomfort;
                }
                val *= baseDiscomfort;
            }
        }
        public override string ExplanationPart(StatRequest req)
        {
            StringBuilder explanation = new StringBuilder();
            explanation.AppendLine("AIU_DiscomfortExplanation1".Translate());
            if (req.HasThing)
            {
                Thing thing = req.Thing;
                List<BodyPartGroupDef> allBodyParts = thing.def.apparel.bodyPartGroups;
                float sizeFactor = 0f;
                explanation.AppendLine();
                explanation.AppendLine("AIU_DiscomfortExplanation2".Translate());
                for (int i = 0; i < allBodyParts.Count; i++)
                {
                    BodyPartGroupDef bodyPart = allBodyParts[i];
                    sizeFactor += bodyPart.GetModExtension<ApparelDiscomfortExtension>().baseDiscomfort;
                    explanation.AppendLine("   " + bodyPart.label + ": +" + bodyPart.GetModExtension<ApparelDiscomfortExtension>().baseDiscomfort.ToStringByStyle(ToStringStyle.FloatTwo));
                }
                explanation.AppendLine();
                explanation.AppendLine("AIUDiscomfortExplanation4".Translate());
                for (int i = 0; i < armorFactors.Count; i++)
                {
                    if (i == 0)
                    {
                        explanation.AppendLine("   (" + armorFactors[i].armorType.label + ": " + thing.GetStatValue(armorFactors[i].armorType).ToStringPercent() + " x " + armorFactors[i].weight.ToStringByStyle(ToStringStyle.FloatTwoOrThree) + ")");
                    }
                    else
                    {
                        explanation.Append("+ (" + armorFactors[i].armorType.label + ": " + thing.GetStatValue(armorFactors[i].armorType).ToStringPercent() + " x " + armorFactors[i].weight.ToStringByStyle(ToStringStyle.FloatTwoOrThree) + ")");
                    }
                }
                explanation.AppendLine();
                float armorFactor = 0f;
                foreach (ArmorFactor factor in armorFactors)
                {
                    armorFactor += thing.GetStatValue(factor.armorType) * factor.weight;
                }
                explanation.AppendLine();
                explanation.AppendLine("AIUDiscomfortExplanation5".Translate());
                explanation.AppendLine("   " + thing.GetStatValue(StatDefOf.Mass).ToStringByStyle(ToStringStyle.FloatTwo));
                explanation.AppendLine();
                explanation.AppendLine("AIUDiscomfortExplanation6".Translate());
                explanation.AppendLine("x(" + sizeFactor.ToStringByStyle(ToStringStyle.FloatTwo) + " x " + armorFactor.ToStringByStyle(ToStringStyle.PercentZero) + " x " + thing.GetStatValue(StatDefOf.Mass).ToStringByStyle(ToStringStyle.FloatTwo) + " = " + (sizeFactor * armorFactor * thing.GetStatValue(StatDefOf.Mass)).ToStringByStyle(ToStringStyle.FloatTwoOrThree) + ")");

                
            }
            return explanation.ToString();
        }
        private List<ArmorFactor> armorFactors = new List<ArmorFactor>() { };

        internal class ArmorFactor
        {
            public StatDef armorType;
            public float weight = 1f;

        }
    }

 public class ThoughtWorker_UncomfortableApparel : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Need_ApparelComfort comfortNeed = p.needs.TryGetNeed<Need_ApparelComfort>();
            if (comfortNeed == null)
            {
                return false;
            }
            DiscomfortComp comp = DiscomfortUtility.Comp(p);
            if (comp == null)
            {
                return false;
            }
            comp.Update();
            int severity = comp.severity - 1;
            if (severity <= -1)
            {
                return false;
            }
            severity = Mathf.Clamp(severity, 0, ThoughtDefOfAIU.UncomfortableApparel.stages.Count - 1);
            return ThoughtState.ActiveAtStage(severity);
        }
    }

    public class ApparelDiscomfortExtension : DefModExtension
    {
        public float baseDiscomfort;
    }
    //DefOf, nothing special here
    [DefOf]
    public static class StatDefOfAIU
    {
        public static StatDef ApparelDiscomfort;
        public static StatDef PawnDiscomfort;
        public static StatDef PawnDiscomfortTolerance;
    }
    [DefOf]
    public static class ThoughtDefOfAIU
    {
        public static ThoughtDef UncomfortableApparel;

    }
    /*Applying this patch as a post-fix causes incompatibility issues with certain graphical mods.
     * Otherwise it would be preferable that way.
     */
    [HarmonyPatch(typeof(Pawn_ApparelTracker), "ApparelChanged")]
    public static class AIUPatch_ApparelTracker_Wear
    {
        [HarmonyPrefix]
        public static void AIUPatch_UpdateDiscomfortAfterWearing(Pawn_ApparelTracker __instance)
        {
            //Every time you put on something, update the discomfort cache.
            DiscomfortComp comp = DiscomfortUtility.Comp(__instance.pawn);
            if (comp != null)
            {
                comp.Refresh();
            }
        }
    }

    //In case there's an issue with former player faction members.
    /*
    [HarmonyPatch(typeof(Thing), "SetFaction")]
    public static class AIUPatch_Thing_SetFaction
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static void AIUPatch_SetCache(Thing __instance, ref Faction ___factionInt)
        {
            DiscomfortComp comp = __instance.TryGetComp<DiscomfortComp>();
            if (comp != null && ___factionInt.IsPlayer)
            {
                comp.Reset();
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "SetFactionDirect")]
    public static class AIUPatch_Thing_SetFactionDirect
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static void AIUPatch_SetCache(Thing __instance, ref Faction ___factionInt)
        {
            DiscomfortComp comp = __instance.TryGetComp<DiscomfortComp>();
            if (comp != null && ___factionInt.IsPlayer)
            {
                comp.Reset();
            }
        }
    }
    */
}
