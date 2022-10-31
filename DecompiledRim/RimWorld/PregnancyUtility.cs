using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimWorld
{
	public static class PregnancyUtility
	{
		public static readonly SimpleCurve DeathChanceFromBirthQuality = new SimpleCurve
		{
			new CurvePoint(0f, 0.2f),
			new CurvePoint(0.2f, 0.05f),
			new CurvePoint(0.9f, 0f)
		};

		public static readonly Dictionary<PawnRelationDef, float> BeingFatherWeightPerRelation = new Dictionary<PawnRelationDef, float>
		{
			{
				PawnRelationDefOf.Spouse,
				1f
			},
			{
				PawnRelationDefOf.Lover,
				0.8f
			},
			{
				PawnRelationDefOf.ExSpouse,
				0.5f
			},
			{
				PawnRelationDefOf.ExLover,
				0.4f
			}
		};

		private static readonly (int, float)[] MetabolismRangeChances = new(int, float)[3]
		{
			(1, 0.7f),
			(2, 0.2f),
			(3, 0.1f)
		};

		public const float MinFertileMaleAge = 14f;

		public const float ChanceOfNoFatherWithCandidates = 0.2f;

		public const float ChanceToInheritOneParent = 0.5f;

		public const float ChanceToInheritTwoParents = 1f;

		public const float IvfPregnancyFactor = 2f;

		private static readonly IntRange BirthFilthCount = new IntRange(4, 7);

		private static float PregnancyChanceFactor_PregnancyApproach_Normal = 1f;

		private static float PregnancyChanceFactor_PregnancyApproach_AvoidPregnancy = 0.25f;

		private static float PregnancyChanceFactor_PregnancyApproach_TryForBaby = 4f;

		private static CachedTexture PregnancyApproachIcon_Normal = new CachedTexture("UI/Icons/PregnancyApproaches/Normal");

		private static CachedTexture PregnancyApproachIcon_AvoidPregnancy = new CachedTexture("UI/Icons/PregnancyApproaches/AvoidPregnancy");

		private static CachedTexture PregnancyApproachIcon_TryForBaby = new CachedTexture("UI/Icons/PregnancyApproaches/TryForBaby");

		private static List<string> tmpLastNames = new List<string>(3);

		private static Dictionary<GeneDef, float> tmpGeneChances = new Dictionary<GeneDef, float>();

		private static List<GeneDef> tmpGenes = new List<GeneDef>();

		public static FloatRange GeneratedPawnPregnancyProgressRange => new FloatRange(0.05f, 0.5f);

		public static AcceptanceReport CanEverProduceChild(Pawn first, Pawn second)
		{
			if (first.Dead)
			{
				return "PawnIsDead".Translate(first.Named("PAWN"));
			}
			if (second.Dead)
			{
				return "PawnIsDead".Translate(second.Named("PAWN"));
			}
			if (first.gender == second.gender)
			{
				return "PawnsHaveSameGender".Translate(first.Named("PAWN1"), second.Named("PAWN2")).Resolve();
			}
			Pawn pawn = ((first.gender == Gender.Male) ? first : second);
			Pawn pawn2 = ((first.gender == Gender.Female) ? first : second);
			bool flag = pawn.GetStatValue(StatDefOf.Fertility) <= 0f;
			bool flag2 = pawn2.GetStatValue(StatDefOf.Fertility) <= 0f;
			if (flag && flag2)
			{
				return "PawnsAreInfertile".Translate(pawn.Named("PAWN1"), pawn2.Named("PAWN2")).Resolve();
			}
			if (flag != flag2)
			{
				return "PawnIsInfertile".Translate((flag ? pawn : pawn2).Named("PAWN")).Resolve();
			}
			bool flag3 = !pawn.ageTracker.CurLifeStage.reproductive;
			bool flag4 = !pawn2.ageTracker.CurLifeStage.reproductive;
			if (flag3 && flag4)
			{
				return "PawnsAreTooYoung".Translate(pawn.Named("PAWN1"), pawn2.Named("PAWN2")).Resolve();
			}
			if (flag3 != flag4)
			{
				return "PawnIsTooYoung".Translate((flag3 ? pawn : pawn2).Named("PAWN")).Resolve();
			}
			bool flag5 = pawn.Sterile();
			bool flag6 = pawn2.Sterile() && GetPregnancyHediff(pawn2) == null && !pawn2.health.hediffSet.HasHediff(HediffDefOf.Lactating);
			if (flag5 && flag6)
			{
				return "PawnsAreSterile".Translate(pawn.Named("PAWN1"), pawn2.Named("PAWN2")).Resolve();
			}
			if (flag5 != flag6)
			{
				return "PawnIsSterile".Translate((flag5 ? pawn : pawn2).Named("PAWN")).Resolve();
			}
			return true;
		}

		public static float PregnancyChanceImplantEmbryo(Pawn surrogate)
		{
			return Mathf.Clamp01(PregnancyChanceForWoman(surrogate) * 2f);
		}

		private static float PregnancyChanceForPartnersWithoutPregnancyApproach(Pawn woman, Pawn man)
		{
			float num = 1f;
			if (man != null)
			{
				if (man.Sterile() || !Find.Storyteller.difficulty.ChildrenAllowed)
				{
					return 0f;
				}
				num = man.GetStatValue(StatDefOf.Fertility);
			}
			return num * PregnancyChanceForWoman(woman);
		}

		public static float PregnancyChanceForPartners(Pawn woman, Pawn man)
		{
			float num = PregnancyChanceForPartnersWithoutPregnancyApproach(woman, man);
			float pregnancyChanceFactor = woman.relations.GetPregnancyApproachForPartner(man).GetPregnancyChanceFactor();
			return num * pregnancyChanceFactor;
		}

		public static float PregnancyChanceForWoman(Pawn woman)
		{
			if (woman.Sterile() || !Find.Storyteller.difficulty.ChildrenAllowed)
			{
				return 0f;
			}
			return woman.GetStatValue(StatDefOf.Fertility);
		}

		public static RitualRoleAssignments RitualAssignmentsForBirth(Precept_Ritual ritual, Pawn mother)
		{
			RitualRoleAssignments ritualRoleAssignments = new RitualRoleAssignments(ritual);
			List<Pawn> list = new List<Pawn> { mother };
			Dictionary<string, Pawn> forcedRoles = new Dictionary<string, Pawn> { { "mother", mother } };
			ritualRoleAssignments.Setup(list, forcedRoles, list, mother);
			ritualRoleAssignments.FillPawns(null, TargetInfo.Invalid);
			return ritualRoleAssignments;
		}

		public static float GetBirthQualityFor(Pawn mother)
		{
			Precept_Ritual ritual = (Precept_Ritual)mother.Ideo.GetPrecept(PreceptDefOf.ChildBirth);
			return RitualUtility.CalculateQualityAbstract(ritual, new TargetInfo(mother.PositionHeld, mother.MapHeld, allowNullMap: true), RitualAssignmentsForBirth(ritual, mother));
		}

		public static IEnumerable<Building_Bed> BedsForBirth(Pawn p)
		{
			List<Building> things = p.MapHeld.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < things.Count; i++)
			{
				Building_Bed building_Bed = things[i] as Building_Bed;
				if (building_Bed != null && IsUsableBedFor(p, p, building_Bed))
				{
					yield return building_Bed;
				}
			}
		}

		public static bool IsUsableBedFor(Pawn mother, Pawn doctor, Building_Bed bed)
		{
			return RestUtility.IsValidBedFor(bed, mother, doctor, checkSocialProperness: true, allowMedBedEvenIfSetToNoCare: false, ignoreOtherReservations: false, mother.GuestStatus);
		}

		public static float ChanceMomDiesDuringBirth(float quality)
		{
			if (Find.Storyteller.difficulty.babiesAreHealthy)
			{
				return 0f;
			}
			return DeathChanceFromBirthQuality.Evaluate(quality);
		}

		private static string RandomLastName(Pawn geneticMother, Pawn birthingMother, Pawn father)
		{
			tmpLastNames.Clear();
			if (geneticMother != null)
			{
				tmpLastNames.Add(PawnNamingUtility.GetLastName(geneticMother));
			}
			if (father != null)
			{
				tmpLastNames.Add(PawnNamingUtility.GetLastName(father));
			}
			if (birthingMother != null && birthingMother != geneticMother && birthingMother != father)
			{
				tmpLastNames.Add(PawnNamingUtility.GetLastName(birthingMother));
			}
			if (tmpLastNames.Count == 0)
			{
				return null;
			}
			return tmpLastNames.RandomElement();
		}

		public static Thing ApplyBirthOutcome(OutcomeChance outcome, float quality, Precept_Ritual ritual, List<GeneDef> genes, Pawn geneticMother, Thing birtherThing, Pawn father = null, Pawn doctor = null, LordJob_Ritual lordJobRitual = null, RitualRoleAssignments assignments = null)
		{
			Pawn birtherPawn = birtherThing as Pawn;
			Building_GrowthVat building_GrowthVat = birtherThing as Building_GrowthVat;
			if (birtherThing.Spawned)
			{
				EffecterDefOf.Birth.Spawn(birtherThing, birtherThing.Map);
			}
			bool babiesAreHealthy = Find.Storyteller.difficulty.babiesAreHealthy;
			int positivityIndex = outcome.positivityIndex;
			bool flag = birtherPawn != null && Rand.Chance(ChanceMomDiesDuringBirth(quality)) && (birtherPawn.genes == null || !birtherPawn.genes.HasGene(GeneDefOf.Deathless));
			Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(geneticMother?.kindDef ?? PawnKindDefOf.Colonist, birtherThing.Faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, RandomLastName(geneticMother, birtherThing as Pawn, father), null, null, null, forceNoIdeo: true, forceNoBackstory: false, forbidAnyTitle: false, positivityIndex == -1, null, forcedXenotype: XenotypeDefOf.Baseliner, forcedEndogenes: (genes != null) ? genes : GetInheritedGenes(father, geneticMother), forcedCustomXenotype: null, allowedXenotypes: null, forceBaselinerChance: 0f, developmentalStages: DevelopmentalStage.Newborn));
			if (GeneUtility.SameHeritableXenotype(geneticMother, father) && geneticMother.genes.UniqueXenotype)
			{
				pawn.genes.xenotypeName = geneticMother.genes.xenotypeName;
				pawn.genes.iconDef = geneticMother.genes.iconDef;
			}
			if (TryGetInheritedXenotype(geneticMother, father, out var xenotype))
			{
				pawn.genes?.SetXenotypeDirect(xenotype);
			}
			else if (ShouldByHybrid(geneticMother, father))
			{
				pawn.genes.hybrid = true;
				pawn.genes.xenotypeName = "Hybrid".Translate();
			}
			IntVec3? intVec = null;
			if (birtherPawn?.Spawned ?? false)
			{
				int? sleepingSlot;
				IntVec3 intVec2 = birtherPawn.CurrentBed(out sleepingSlot)?.GetFootSlotPos(sleepingSlot.Value) ?? birtherPawn.PositionHeld;
				intVec = CellFinder.RandomClosewalkCellNear(intVec2, birtherPawn.Map, 1, delegate(IntVec3 cell)
				{
					if (cell != birtherPawn.PositionHeld)
					{
						Building building = birtherPawn.Map.edificeGrid[cell];
						if (building == null)
						{
							return true;
						}
						return building.def?.IsBed != true;
					}
					return false;
				});
				SpawnBirthFilth(birtherPawn, intVec2, ThingDefOf.Filth_AmnioticFluid, 1);
				if (flag)
				{
					SpawnBirthFilth(birtherPawn, intVec2, ThingDefOf.Filth_Blood, 2);
				}
			}
			if (building_GrowthVat != null)
			{
				intVec = building_GrowthVat.InteractionCell;
				FilthMaker.TryMakeFilth(intVec.Value, building_GrowthVat.Map, ThingDefOf.Filth_AmnioticFluid, BirthFilthCount.RandomInRange);
			}
			if (birtherPawn != null)
			{
				birtherPawn.health.AddHediff(HediffDefOf.PostpartumExhaustion);
				birtherPawn.health.AddHediff(HediffDefOf.Lactating);
			}
			if (pawn.RaceProps.IsFlesh)
			{
				if (geneticMother != null)
				{
					pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, geneticMother);
				}
				if (father != null)
				{
					pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, father);
				}
				if (birtherPawn != null && birtherPawn != geneticMother)
				{
					pawn.relations.AddDirectRelation(PawnRelationDefOf.ParentBirth, birtherPawn);
				}
			}
			bool flag2 = false;
			bool flag3 = positivityIndex == 0;
			if (positivityIndex >= 0)
			{
				if (pawn.playerSettings != null && geneticMother?.playerSettings != null)
				{
					pawn.playerSettings.AreaRestriction = geneticMother.playerSettings.AreaRestriction;
				}
				if (flag3)
				{
					pawn.health.AddHediff(HediffDefOf.InfantIllness);
				}
				if (birtherPawn != null)
				{
					TaleRecorder.RecordTale(TaleDefOf.GaveBirth, birtherPawn, pawn);
					pawn.mindState.SetAutofeeder(birtherPawn, AutofeedMode.Urgent);
				}
				if (doctor != null && doctor.Spawned && doctor != null && doctor.carryTracker?.TryStartCarry(pawn) == true && birtherPawn != null && (building_GrowthVat != null || doctor.CanReachImmediate(birtherPawn, PathEndMode.Touch)))
				{
					Job job = JobMaker.MakeJob(JobDefOf.CarryToMomAfterBirth, pawn, birtherPawn);
					job.count = 1;
					doctor.jobs.StartJob(job, JobCondition.Succeeded, null, resumeCurJobAfterwards: false, cancelBusyStances: true, null, null, fromQueue: false, canReturnCurJobToPool: false, true);
				}
				else if (!PawnUtility.TrySpawnHatchedOrBornPawn(pawn, birtherThing, intVec))
				{
					Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
				}
				if (!flag3)
				{
					father?.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.BabyBorn, pawn);
					geneticMother?.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.BabyBorn, pawn);
					birtherPawn?.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.BabyBorn, pawn);
				}
			}
			else
			{
				Hediff culpritHediff = pawn.health.AddHediff(HediffDefOf.Stillborn);
				flag2 = true;
				birtherPawn?.Ideo?.Notify_MemberDied(pawn);
				pawn.babyNamingDeadline = Find.TickManager.TicksGame + 1;
				Find.BattleLog.Add(new BattleLogEntry_StateTransition(pawn, pawn.RaceProps.DeathActionWorker.DeathRules, null, culpritHediff, null));
				if (birtherThing.Spawned)
				{
					GenSpawn.Spawn(pawn.Corpse, intVec ?? birtherThing.PositionHeld, birtherThing.MapHeld);
				}
			}
			if (birtherPawn != null && flag2 && birtherPawn == geneticMother)
			{
				birtherPawn?.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.Stillbirth);
			}
			Map mapHeld = birtherThing.MapHeld;
			IntVec3 positionHeld = birtherThing.PositionHeld;
			if (flag)
			{
				birtherPawn.Kill(null, null);
			}
			TaggedString text = ((birtherPawn != null) ? outcome.description.Formatted(birtherPawn.Named("MOTHER")) : (flag2 ? ((geneticMother == null || father == null) ? "LetterVatSillbornNoParents".Translate() : "LetterVatStillborn".Translate(geneticMother, father)) : (flag3 ? ((geneticMother == null || father == null) ? "LetterVatInfantIllnessNoParents".Translate() : "LetterVatInfantIllness".Translate(geneticMother, father)) : ((geneticMother == null || father == null) ? "LetterVatHealthyBabyNoParents".Translate() : "LetterVatHealthyBaby".Translate(geneticMother, father)))));
			if (!flag2 && birtherPawn != null && geneticMother != null && birtherPawn != geneticMother && father != null)
			{
				text += "\n\n" + "LetterPartSurrogacy".Translate(geneticMother, father);
			}
			if (birtherPawn != null && !babiesAreHealthy)
			{
				RitualOutcomeEffectWorker_ChildBirth ritualOutcomeEffectWorker_ChildBirth = (RitualOutcomeEffectWorker_ChildBirth)RitualOutcomeEffectDefOf.ChildBirth.GetInstance();
				if (lordJobRitual != null)
				{
					text += "\n\n" + ritualOutcomeEffectWorker_ChildBirth.OutcomeQualityBreakdownDesc(quality, 1f, lordJobRitual);
				}
				else
				{
					text += "\n\n" + RitualUtility.QualityBreakdownAbstract(ritual, new TargetInfo(positionHeld, mapHeld, allowNullMap: true), assignments);
				}
				if (!babiesAreHealthy)
				{
					text += "\n\n" + "BirthRitualHealthyBabyChance".Translate(ritualOutcomeEffectWorker_ChildBirth.GetOutcomeChanceAtQuality(lordJobRitual, ritualOutcomeEffectWorker_ChildBirth.def.BestOutcome, quality));
				}
				if (flag)
				{
					text += "\n\n" + "LetterPartColonistDiedAfterChildbirth".Translate(birtherPawn);
				}
			}
			if (pawn.genes.HasGene(GeneDefOf.Inbred))
			{
				text += "\n\n" + "InbredBabyBorn".Translate();
			}
			text += "\n\n" + "LetterPartNameBaby".Translate(pawn, flag2 ? "Immediately".Translate().ToString() : string.Format("{0} {1}", "Within".Translate(), 60000.ToStringTicksToPeriod()));
			ChoiceLetter_BabyBirth choiceLetter_BabyBirth = (ChoiceLetter_BabyBirth)LetterMaker.MakeLetter((birtherPawn != null) ? "OutcomeLetterLabel".Translate(outcome.label.Named("OUTCOMELABEL"), ritual.Label.Named("RITUALLABEL")) : "LetterVatBirth".Translate(outcome.label), text, LetterDefOf.BabyBirth, pawn);
			choiceLetter_BabyBirth.Start();
			Find.LetterStack.ReceiveLetter(choiceLetter_BabyBirth);
			if (positivityIndex < 0)
			{
				return pawn.Corpse;
			}
			return pawn;
		}

		public static Gizmo BirthQualityGizmo(Pawn pawn)
		{
			return new Command_Action
			{
				defaultLabel = "DEV: Output Birth Quality Now",
				action = delegate
				{
					Messages.Message("Birth ritual quality for " + pawn.LabelShort + " is " + GetBirthQualityFor(pawn).ToStringPercent(), pawn, MessageTypeDefOf.NeutralEvent);
				}
			};
		}

		private static void SpawnBirthFilth(Pawn mother, IntVec3 center, ThingDef filth, int radius)
		{
			int randomInRange = BirthFilthCount.RandomInRange;
			for (int i = 0; i < randomInRange; i++)
			{
				FilthMaker.TryMakeFilth(CellFinder.RandomClosewalkCellNear(center, mother.Map, radius), mother.Map, filth, mother.LabelIndefinite());
			}
		}

		public static GeneSet GetInheritedGeneSet(Pawn father, Pawn mother)
		{
			GeneSet geneSet = new GeneSet();
			foreach (GeneDef inheritedGene in GetInheritedGenes(father, mother))
			{
				geneSet.AddGene(inheritedGene);
			}
			if (GeneUtility.SameHeritableXenotype(father, mother))
			{
				geneSet.SetNameDirect(father.genes.xenotypeName);
			}
			return geneSet;
		}

		public static List<GeneDef> GetInheritedGenes(Pawn father, Pawn mother)
		{
			tmpGenes.Clear();
			if (father?.genes != null)
			{
				foreach (Gene endogene in father.genes.Endogenes)
				{
					if (endogene.def.endogeneCategory != EndogeneCategory.Melanin && endogene.def.biostatArc <= 0)
					{
						tmpGeneChances.SetOrAdd(endogene.def, 0.5f);
					}
				}
			}
			if (mother?.genes != null)
			{
				foreach (Gene endogene2 in mother.genes.Endogenes)
				{
					if (endogene2.def.endogeneCategory != EndogeneCategory.Melanin && endogene2.def.biostatArc <= 0)
					{
						if (tmpGeneChances.ContainsKey(endogene2.def))
						{
							tmpGeneChances[endogene2.def] = 1f;
						}
						else
						{
							tmpGeneChances.Add(endogene2.def, 0.5f);
						}
					}
				}
			}
			int metMax = MetabolismRangeChances.RandomElementByWeight(((int, float) x) => x.Item2).Item1;
			bool sameXenotype = GeneUtility.SameHeritableXenotype(mother, father);
			foreach (KeyValuePair<GeneDef, float> tmpGeneChance in tmpGeneChances)
			{
				if (CanAddGeneWithChance(tmpGeneChance))
				{
					tmpGenes.Add(tmpGeneChance.Key);
				}
			}
			if (PawnSkinColors.SkinColorsFromParents(father, mother).TryRandomElement(out var result))
			{
				tmpGenes.Add(result);
			}
			if (!tmpGenes.Contains(GeneDefOf.Inbred) && Rand.Value < InbredChanceFromParents(mother, father, out var _))
			{
				tmpGenes.Add(GeneDefOf.Inbred);
			}
			tmpGeneChances.Clear();
			return tmpGenes;
			bool CanAddGeneWithChance(KeyValuePair<GeneDef, float> kvp)
			{
				GeneDef key = kvp.Key;
				if (tmpGenes.Contains(key))
				{
					return false;
				}
				if (!Rand.Chance(kvp.Value))
				{
					return false;
				}
				if (!sameXenotype && Mathf.Abs(tmpGenes.Sum((GeneDef x) => x.biostatMet) + key.biostatMet) > metMax)
				{
					return false;
				}
				return true;
			}
		}

		public static float InbredChanceFromParents(Pawn mother, Pawn father, out PawnRelationDef relation)
		{
			relation = null;
			if (mother != null && father != null)
			{
				float num = 0f;
				{
					foreach (PawnRelationDef relation2 in father.GetRelations(mother))
					{
						if (relation2.inbredChanceOnChild > num)
						{
							num = relation2.inbredChanceOnChild;
							relation = relation2;
						}
						num = Mathf.Max(num, relation2.inbredChanceOnChild);
					}
					return num;
				}
			}
			return 0f;
		}

		private static bool TryGetInheritedXenotype(Pawn mother, Pawn father, out XenotypeDef xenotype)
		{
			bool flag = mother?.genes != null;
			bool flag2 = father?.genes != null;
			if (flag && flag2 && mother.genes.Xenotype.inheritable && father.genes.Xenotype.inheritable && mother.genes.Xenotype == father.genes.Xenotype)
			{
				xenotype = mother.genes.Xenotype;
				return true;
			}
			if (flag && !flag2 && mother.genes.Xenotype.inheritable)
			{
				xenotype = mother.genes.Xenotype;
				return true;
			}
			if (flag2 && !flag && father.genes.Xenotype.inheritable)
			{
				xenotype = father.genes.Xenotype;
				return true;
			}
			xenotype = null;
			return false;
		}

		private static bool ShouldByHybrid(Pawn mother, Pawn father)
		{
			bool flag = mother?.genes != null;
			bool flag2 = father?.genes != null;
			if (flag && flag2)
			{
				if (mother.genes.hybrid && father.genes.hybrid)
				{
					return true;
				}
				if (mother.genes.Xenotype.inheritable && father.genes.Xenotype.inheritable)
				{
					return true;
				}
				bool num = flag && (mother.genes.Xenotype.inheritable || mother.genes.hybrid);
				bool flag3 = flag2 && (father.genes.Xenotype.inheritable || father.genes.hybrid);
				if (num || flag3)
				{
					return true;
				}
			}
			if ((flag && !flag2 && mother.genes.hybrid) || (flag2 && !flag && father.genes.hybrid))
			{
				return true;
			}
			return false;
		}

		public static bool TryTerminatePregnancy(Pawn pawn)
		{
			Hediff pregnancyHediff = GetPregnancyHediff(pawn);
			if (pregnancyHediff == null)
			{
				return false;
			}
			int curStageIndex = pregnancyHediff.CurStageIndex;
			PregnancyAttitude? attitude = ((Hediff_Pregnant)pregnancyHediff).Attitude;
			pawn.health.RemoveHediff(pregnancyHediff);
			if (pawn.needs?.mood?.thoughts?.memories != null && attitude.HasValue)
			{
				Thought_Memory thought_Memory = null;
				switch (attitude.Value)
				{
				case PregnancyAttitude.Positive:
					thought_Memory = ThoughtMaker.MakeThought(ThoughtDefOf.PregnancyTerminated, curStageIndex);
					break;
				case PregnancyAttitude.Negative:
					thought_Memory = (Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.PregnancyEnded);
					break;
				}
				if (thought_Memory != null)
				{
					pawn.needs.mood.thoughts.memories.TryGainMemory(thought_Memory);
				}
			}
			return true;
		}

		public static Hediff GetPregnancyHediff(Pawn pawn)
		{
			if (pawn.RaceProps.Humanlike)
			{
				return pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman, mustBeVisible: true);
			}
			if (pawn.RaceProps.Animal)
			{
				return pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant, mustBeVisible: true);
			}
			return null;
		}

		public static string GetLabel(this PregnancyApproach mode)
		{
			switch (mode)
			{
			case PregnancyApproach.Normal:
				return "PregnancyApproach_Normal".Translate();
			case PregnancyApproach.AvoidPregnancy:
				return "PregnancyApproach_AvoidPregnancy".Translate();
			case PregnancyApproach.TryForBaby:
				return "PregnancyApproach_TryForBaby".Translate();
			default:
				Log.Error("Undefined rhythm method.");
				return string.Empty;
			}
		}

		public static float GetPregnancyChanceFactor(this PregnancyApproach mode)
		{
			return mode switch
			{
				PregnancyApproach.AvoidPregnancy => PregnancyChanceFactor_PregnancyApproach_AvoidPregnancy, 
				PregnancyApproach.TryForBaby => PregnancyChanceFactor_PregnancyApproach_TryForBaby, 
				_ => PregnancyChanceFactor_PregnancyApproach_Normal, 
			};
		}

		public static string GetDescription(this PregnancyApproach mode)
		{
			return string.Format("{0} ({1} x{2})", mode.GetLabel().CapitalizeFirst(), "PregnancyChance".Translate(), mode.GetPregnancyChanceFactor().ToStringPercent());
		}

		public static Texture2D GetIcon(this PregnancyApproach mode)
		{
			return mode switch
			{
				PregnancyApproach.AvoidPregnancy => PregnancyApproachIcon_AvoidPregnancy.Texture, 
				PregnancyApproach.TryForBaby => PregnancyApproachIcon_TryForBaby.Texture, 
				_ => PregnancyApproachIcon_Normal.Texture, 
			};
		}
	}
}
