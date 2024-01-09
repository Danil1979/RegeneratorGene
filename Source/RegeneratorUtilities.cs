using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RegeneratorGene
{
    public static class RegeneratorUtilities
    {
        public static void NaturalRegenerationOfLimbs(Pawn pawn, HediffDef hediffToAdd)
        {
            var toRemove = new List<Hediff>();
            var toAdd = new List<Hediff>();
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is Hediff_MissingPart && !pawn.health.hediffSet.PartIsMissing(hediff.Part.parent) &&
                    !pawn.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(hediff.Part))
                {
                    var part = hediff.Part;
                    var flag = true;
                    while (part != null)
                        if (pawn.health.hediffSet.hediffs.Any(hd => hd.Part == part && hd.def == hediffToAdd))
                        {
                            flag = false;
                            part = null;
                        }
                        else
                        {
                            part = part.parent;
                        }
                    Hediff hediff1 = FindImmunizableHediffWhichCanKill(pawn);
                    Hediff hediff2 = FindNonInjuryMiscBadHediff(pawn, true);
                    Hediff hediff3 = FindNonInjuryMiscBadHediff(pawn, false);

                    if (hediff1 != null || hediff2 != null || hediff3 != null)
                    {
                        // Add the first non-null Hediff to the toRemove list using the null-coalescing operator (??)
                        toRemove.Add(hediff1 ?? hediff2 ?? hediff3);
                    }

                    if (flag)
                    {
                        var newHediff = HediffMaker.MakeHediff(hediffToAdd, pawn, hediff.Part);
                        newHediff.Severity = 0.01f;
                        toAdd.Add(newHediff);
                        toRemove.Add(hediff);
                    }
                }

                if (hediff.def == hediffToAdd)
                {
                    hediff.Severity += 0.10f;
                    if (hediff.Severity >= 1f) toRemove.Add(hediff);
                }
            }

            foreach (var hediff in toRemove) pawn.health.RemoveHediff(hediff);

            foreach (var hediff in toAdd) pawn.health.AddHediff(hediff);
        }
        private static List<BodyPartRecord> GetAllLostBp(Pawn pawn)
        {
            var noMissingBP = pawn.health.hediffSet.GetNotMissingParts().ToList();
            var missingBP = pawn.def.race.body.AllParts.Where(i =>
                pawn.health.hediffSet.PartIsMissing(i)
                && noMissingBP.Contains(i.parent)
                && !pawn.health.hediffSet.AncestorHasDirectlyAddedParts(i)).ToList();
            return missingBP;
        }

        private static bool HealLimb(Pawn pawn, Hediff_MissingPart removedMissingPartHediff,
            HediffDef hediffToAdd,
            bool instaHeal = false)
        {
            bool healedOnce;
            Hediff regeneratingHediff = HediffMaker.MakeHediff(hediffToAdd,
                                    pawn,
                                    removedMissingPartHediff.Part);
            if (!instaHeal)
                regeneratingHediff.Severity = removedMissingPartHediff.Part.def.GetMaxHealth(pawn) - 1;
            else
                regeneratingHediff.Severity = removedMissingPartHediff.Part.def.GetMaxHealth(pawn);

            pawn.health.AddHediff(regeneratingHediff);
            healedOnce = true;
            return healedOnce;
        }

        /// <summary>
        /// Helper method to regen a missing limb and add the regenerating hediff
        /// </summary>
        /// <param name="pawn"></param>
        /// <param name="missingBP"></param>
        /// <returns></returns>
        private static List<Hediff_MissingPart> GetAnyRemovedMissingPartAfterRegen(Pawn pawn, List<BodyPartRecord> missingBP)
        {
            var missingPart = missingBP.RandomElement();
            var currentMissingHediffs = GetMissingsHediffs(pawn);
            pawn.health.RestorePart(missingPart);
            var currentMissingHediffs2 = GetMissingsHediffs(pawn);
            var removedMissingPartHediff = currentMissingHediffs.Where(i
                => !currentMissingHediffs2.Contains(i)).ToList();
            return removedMissingPartHediff;
        }

        private static List<Hediff_MissingPart> GetMissingsHediffs(Pawn pawn)
        {
            var missingHediffs = pawn.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList();
            return missingHediffs;
        }
        private static Hediff FindImmunizableHediffWhichCanKill(Pawn pawn)
		{
			Hediff hediff = null;
			float num = -1f;
			List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
			for (int i = 0; i < hediffs.Count; i++)
			{
				if (hediffs[i].Visible && hediffs[i].def.everCurableByItem)
				{
					if (hediffs[i].TryGetComp<HediffComp_Immunizable>() != null)
					{
						if (!hediffs[i].FullyImmune())
						{
							if (CanEverKill(hediffs[i]))
							{
								float severity = hediffs[i].Severity;
								if (hediff == null || severity > num)
								{
									hediff = hediffs[i];
									num = severity;
								}
							}
						}
					}
				}
			}
			return hediff;
		}
        private static Hediff FindNonInjuryMiscBadHediff(Pawn pawn, bool onlyIfCanKill)
		{
			Hediff hediff = null;
			float num = -1f;
			List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
			for (int i = 0; i < hediffs.Count; i++)
			{
				if (hediffs[i].Visible && hediffs[i].def.isBad && hediffs[i].def.everCurableByItem)
				{
					if (!(hediffs[i] is Hediff_Injury) && !(hediffs[i] is Hediff_MissingPart) && !(hediffs[i] is Hediff_Addiction) && !(hediffs[i] is Hediff_AddedPart))
					{
						if (!onlyIfCanKill || CanEverKill(hediffs[i]))
						{
							float num2 = (hediffs[i].Part == null) ? 999f : hediffs[i].Part.coverageAbsWithChildren;
							if (hediff == null || num2 > num)
							{
								hediff = hediffs[i];
								num = num2;
							}
						}
					}
				}
			}
			return hediff;
		}

        private static void Cure(Hediff hediff)
		{
			Pawn pawn = hediff.pawn;
			pawn.health.RemoveHediff(hediff);
			if (hediff.def.cureAllAtOnceIfCuredByItem)
			{
				int num = 0;
				while (true)
				{
					num++;
					if (num > 10000)
					{
						break;
					}
					Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(hediff.def, false);
					if (firstHediffOfDef == null)
					{
						goto Block_3;
					}
					pawn.health.RemoveHediff(firstHediffOfDef);
				}
				Log.Error("Too many iterations.", false);
				Block_3:;
			}
			Messages.Message("MessageHediffCuredByItem".Translate(hediff.LabelBase.CapitalizeFirst()), pawn, MessageTypeDefOf.PositiveEvent, true);
		}

        private static bool CanEverKill(Hediff hediff)
		{
			if (hediff.def.stages != null)
			{
				for (int i = 0; i < hediff.def.stages.Count; i++)
				{
					if (hediff.def.stages[i].lifeThreatening)
					{
						return true;
					}
				}
			}
			return hediff.def.lethalSeverity >= 0f;
		}
		}
    }
