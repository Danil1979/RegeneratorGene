using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWorld.QuestGen
{
	public class QuestNode_Root_Hospitality_Refugee : QuestNode
	{
		private static readonly FloatRange LodgerCountBasedOnColonyPopulationFactorRange = new FloatRange(0.3f, 1f);

		private static readonly QuestGen_Pawns.GetPawnParms FactionOpponentPawnParams = new QuestGen_Pawns.GetPawnParms
		{
			mustBeWorldPawn = true,
			mustBeFactionLeader = true,
			mustBeNonHostileToPlayer = true
		};

		private const float MidEventSelWeight_None = 0.5f;

		private const float MidEventSelWeight_Mutiny = 0.25f;

		private const float MidEventSelWeight_BetrayalOffer = 0.25f;

		private const float RewardPostLeaveChance = 0.5f;

		private const float RewardFactor_Postleave = 55f;

		public const float RewardFactor_BetrayalOffer = 300f;

		public const int BetrayalOfferGoodwillReward = 10;

		private static FloatRange BetrayalOfferTimeRange = new FloatRange(0.25f, 0.5f);

		private static FloatRange MutinyTimeRange = new FloatRange(0.2f, 1f);

		private static IntRange QuestDurationDaysRange = new IntRange(5, 20);

		protected override void RunInt()
		{
			if (!ModLister.CheckRoyalty("Hospitality refugee"))
			{
				return;
			}
			Quest quest = QuestGen.quest;
			Slate slate = QuestGen.slate;
			Map map = QuestGen_Get.GetMap();
			int num = (slate.Exists("population") ? slate.Get("population", 0) : map.mapPawns.FreeColonistsSpawnedCount);
			int lodgerCount = Mathf.Max(Mathf.RoundToInt(LodgerCountBasedOnColonyPopulationFactorRange.RandomInRange * (float)num), 1);
			int num2 = 0;
			bool var = false;
			if (Find.Storyteller.difficulty.ChildrenAllowed && lodgerCount >= 2)
			{
				new List<(int, float)>
				{
					(0, 0.4f),
					(Rand.Range(1, lodgerCount / 2), 0.4f),
					(lodgerCount - 1, 0.2f)
				}.TryRandomElementByWeight(((int, float) p) => p.Item2, out var result);
				num2 = result.Item1;
				var = num2 == lodgerCount - 1;
			}
			int questDurationDays = QuestDurationDaysRange.RandomInRange;
			int questDurationTicks = questDurationDays * 60000;
			List<FactionRelation> list = new List<FactionRelation>();
			foreach (Faction item3 in Find.FactionManager.AllFactionsListForReading)
			{
				if (!item3.def.permanentEnemy)
				{
					list.Add(new FactionRelation
					{
						other = item3,
						kind = FactionRelationKind.Neutral
					});
				}
			}
			FactionGeneratorParms parms = new FactionGeneratorParms(FactionDefOf.OutlanderRefugee, default(IdeoGenerationParms), true);
			if (ModsConfig.IdeologyActive)
			{
				parms.ideoGenerationParms = new IdeoGenerationParms(parms.factionDef, forceNoExpansionIdeo: false, DefDatabase<PreceptDef>.AllDefs.Where((PreceptDef p) => p.proselytizes || p.approvesOfCharity).ToList());
			}
			Faction faction = FactionGenerator.NewGeneratedFactionWithRelations(parms, list);
			faction.temporary = true;
			Find.FactionManager.Add(faction);
			string lodgerRecruitedSignal = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.Recruited");
			string text = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.Arrested");
			string inSignalDestroyed = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.Destroyed");
			string inSignalKidnapped = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.Kidnapped");
			string inSignalLeftMap = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.LeftMap");
			string inSignalBanished = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.Banished");
			List<Pawn> pawns = new List<Pawn>();
			for (int i = 0; i < lodgerCount; i++)
			{
				DevelopmentalStage developmentalStages = ((i > 0 && i >= lodgerCount - num2) ? DevelopmentalStage.Child : DevelopmentalStage.Adult);
				Pawn pawn = quest.GeneratePawn(PawnKindDefOf.Refugee, faction, allowAddictions: true, null, 0f, mustBeCapableOfViolence: true, null, 0f, 0f, ensureNonNumericName: false, forceGenerateNewPawn: true, developmentalStages, allowPregnant: true);
				pawns.Add(pawn);
				quest.PawnJoinOffer(pawn, "LetterJoinOfferLabel".Translate(pawn.Named("PAWN")), "LetterJoinOfferTitle".Translate(pawn.Named("PAWN")), "LetterJoinOfferText".Translate(pawn.Named("PAWN"), map.Parent.Named("MAP")), delegate
				{
					quest.JoinPlayer(map.Parent, Gen.YieldSingle(pawn), joinPlayer: true);
					quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, label: "LetterLabelMessageRecruitSuccess".Translate() + ": " + pawn.LabelShortCap, text: "MessageRecruitJoinOfferAccepted".Translate(pawn.Named("RECRUITEE")));
					quest.SignalPass(null, null, lodgerRecruitedSignal);
				}, delegate
				{
					quest.RecordHistoryEvent(HistoryEventDefOf.CharityRefused_ThreatReward_Joiner);
				}, null, null, null, charity: true);
			}
			slate.Set("lodgers", pawns);
			faction.leader = pawns.First();
			Pawn asker = pawns.First();
			quest.SetFactionHidden(faction);
			QuestPart_ExtraFaction extraFactionPart = quest.ExtraFaction(faction, pawns, ExtraFactionType.MiniFaction, areHelpers: false, lodgerRecruitedSignal);
			quest.PawnsArrive(pawns, null, map.Parent, null, joinPlayer: true, null, "[lodgersArriveLetterLabel]", "[lodgersArriveLetterText]");
			QuestPart_Choice questPart_Choice = quest.RewardChoice();
			QuestPart_Choice.Choice choice = new QuestPart_Choice.Choice
			{
				rewards = 
				{
					(Reward)new Reward_VisitorsHelp(),
					(Reward)new Reward_PossibleFutureReward()
				}
			};
			if (ModsConfig.IdeologyActive && Faction.OfPlayer.ideos.FluidIdeo != null)
			{
				choice.rewards.Add(new Reward_DevelopmentPoints(quest));
			}
			questPart_Choice.choices.Add(choice);
			quest.SetAllApparelLocked(pawns);
			string assaultColonySignal = QuestGen.GenerateNewSignal("AssaultColony");
			Action item = delegate
			{
				int num5 = Mathf.FloorToInt(MutinyTimeRange.RandomInRange * (float)questDurationTicks);
				quest.Delay(num5, delegate
				{
					quest.Letter(LetterDefOf.ThreatBig, null, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[mutinyLetterText]", null, "[mutinyLetterLabel]");
					quest.SignalPass(null, null, assaultColonySignal);
					QuestGen_End.End(quest, QuestEndOutcome.Unknown);
				}, null, null, null, reactivatable: false, null, null, isQuestTimeout: false, null, null, "Mutiny (" + num5.ToStringTicksToDays() + ")");
			};
			string lodgerArrestedOrRecruited = QuestGen.GenerateNewSignal("Lodger_ArrestedOrRecruited");
			Action item2 = delegate
			{
				int num4 = Mathf.FloorToInt(BetrayalOfferTimeRange.RandomInRange * (float)questDurationTicks);
				Pawn factionOpponent = quest.GetPawn(FactionOpponentPawnParams);
				quest.Delay(num4, delegate
				{
					QuestPart_AddQuest_RefugeeBetrayal part = new QuestPart_AddQuest_RefugeeBetrayal
					{
						acceptee = quest.AccepterPawn,
						lodgers = pawns,
						refugeeFaction = extraFactionPart.extraFaction,
						factionOpponent = factionOpponent,
						inSignal = QuestGen.slate.Get<string>("inSignal"),
						inSignalRemovePawn = lodgerArrestedOrRecruited,
						parent = quest,
						asker = asker,
						mapParent = map.Parent,
						sendAvailableLetter = true
					};
					quest.AddPart(part);
				}, null, null, null, reactivatable: false, null, null, isQuestTimeout: false, null, null, "BetrayalOffer (" + num4.ToStringTicksToDays() + ")");
			};
			if (Find.Storyteller.difficulty.allowViolentQuests)
			{
				List<Tuple<float, Action>> list2 = new List<Tuple<float, Action>>();
				list2.Add(Tuple.Create(0.25f, item));
				if (QuestGen_Pawns.GetPawnTest(FactionOpponentPawnParams, out var _))
				{
					list2.Add(Tuple.Create(0.25f, item2));
				}
				list2.Add(Tuple.Create<float, Action>(0.5f, delegate
				{
				}));
				if (list2.TryRandomElementByWeight((Tuple<float, Action> t) => t.Item1, out var result2))
				{
					result2.Item2();
				}
			}
			QuestPart_RefugeeInteractions questPart_RefugeeInteractions = new QuestPart_RefugeeInteractions();
			questPart_RefugeeInteractions.inSignalEnable = QuestGen.slate.Get<string>("inSignal");
			questPart_RefugeeInteractions.inSignalDestroyed = inSignalDestroyed;
			questPart_RefugeeInteractions.inSignalArrested = text;
			questPart_RefugeeInteractions.inSignalSurgeryViolation = QuestGenUtility.HardcodedSignalWithQuestID("lodgers.SurgeryViolation");
			questPart_RefugeeInteractions.inSignalKidnapped = inSignalKidnapped;
			questPart_RefugeeInteractions.inSignalRecruited = lodgerRecruitedSignal;
			questPart_RefugeeInteractions.inSignalAssaultColony = assaultColonySignal;
			questPart_RefugeeInteractions.inSignalLeftMap = inSignalLeftMap;
			questPart_RefugeeInteractions.inSignalBanished = inSignalBanished;
			questPart_RefugeeInteractions.outSignalDestroyed_AssaultColony = QuestGen.GenerateNewSignal("LodgerDestroyed_AssaultColony");
			questPart_RefugeeInteractions.outSignalDestroyed_LeaveColony = QuestGen.GenerateNewSignal("LodgerDestroyed_LeaveColony");
			questPart_RefugeeInteractions.outSignalDestroyed_BadThought = QuestGen.GenerateNewSignal("LodgerDestroyed_BadThought");
			questPart_RefugeeInteractions.outSignalArrested_AssaultColony = QuestGen.GenerateNewSignal("LodgerArrested_AssaultColony");
			questPart_RefugeeInteractions.outSignalArrested_LeaveColony = QuestGen.GenerateNewSignal("LodgerArrested_LeaveColony");
			questPart_RefugeeInteractions.outSignalArrested_BadThought = QuestGen.GenerateNewSignal("LodgerArrested_BadThought");
			questPart_RefugeeInteractions.outSignalSurgeryViolation_AssaultColony = QuestGen.GenerateNewSignal("LodgerSurgeryViolation_AssaultColony");
			questPart_RefugeeInteractions.outSignalSurgeryViolation_LeaveColony = QuestGen.GenerateNewSignal("LodgerSurgeryViolation_LeaveColony");
			questPart_RefugeeInteractions.outSignalSurgeryViolation_BadThought = QuestGen.GenerateNewSignal("LodgerSurgeryViolation_BadThought");
			questPart_RefugeeInteractions.outSignalLast_Destroyed = QuestGen.GenerateNewSignal("LastLodger_Destroyed");
			questPart_RefugeeInteractions.outSignalLast_Arrested = QuestGen.GenerateNewSignal("LastLodger_Arrested");
			questPart_RefugeeInteractions.outSignalLast_Kidnapped = QuestGen.GenerateNewSignal("LastLodger_Kidnapped");
			questPart_RefugeeInteractions.outSignalLast_Recruited = QuestGen.GenerateNewSignal("LastLodger_Recruited");
			questPart_RefugeeInteractions.outSignalLast_LeftMapAllHealthy = QuestGen.GenerateNewSignal("LastLodger_LeftMapAllHealthy");
			questPart_RefugeeInteractions.outSignalLast_LeftMapAllNotHealthy = QuestGen.GenerateNewSignal("LastLodger_LeftMapAllNotHealthy");
			questPart_RefugeeInteractions.outSignalLast_Banished = QuestGen.GenerateNewSignal("LastLodger_Banished");
			questPart_RefugeeInteractions.pawns.AddRange(pawns);
			questPart_RefugeeInteractions.faction = faction;
			questPart_RefugeeInteractions.mapParent = map.Parent;
			questPart_RefugeeInteractions.signalListenMode = QuestPart.SignalListenMode.Always;
			quest.AddPart(questPart_RefugeeInteractions);
			quest.AnySignal(new List<string> { lodgerRecruitedSignal, text }, null, new List<string> { lodgerArrestedOrRecruited });
			quest.Delay(questDurationTicks, delegate
			{
				quest.SignalPassWithFaction(faction, null, delegate
				{
					quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgersLeavingLetterText]", null, "[lodgersLeavingLetterLabel]");
				});
				quest.Leave(pawns, null, sendStandardLetter: false, leaveOnCleanup: false, lodgerArrestedOrRecruited, wakeUp: true);
			}, null, null, null, reactivatable: false, null, null, isQuestTimeout: false, "GuestsDepartsIn".Translate(), "GuestsDepartsOn".Translate(), "QuestDelay");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalDestroyed_BadThought, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerDiedMemoryThoughtLetterText]", null, "[lodgerDiedMemoryThoughtLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalDestroyed_AssaultColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerDiedAttackPlayerLetterText]", null, "[lodgerDiedAttackPlayerLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalDestroyed_LeaveColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerDiedLeaveMapLetterText]", null, "[lodgerDiedLeaveMapLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalLast_Destroyed, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgersAllDiedLetterText]", null, "[lodgersAllDiedLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalArrested_BadThought, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerArrestedMemoryThoughtLetterText]", null, "[lodgerArrestedMemoryThoughtLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalArrested_AssaultColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerArrestedAttackPlayerLetterText]", null, "[lodgerArrestedAttackPlayerLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalArrested_LeaveColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerArrestedLeaveMapLetterText]", null, "[lodgerArrestedLeaveMapLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalLast_Arrested, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgersAllArrestedLetterText]", null, "[lodgersAllArrestedLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalSurgeryViolation_BadThought, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerViolatedMemoryThoughtLetterText]", null, "[lodgerViolatedMemoryThoughtLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalSurgeryViolation_AssaultColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerViolatedAttackPlayerLetterText]", null, "[lodgerViolatedAttackPlayerLetterLabel]");
			quest.Letter(LetterDefOf.NegativeEvent, questPart_RefugeeInteractions.outSignalSurgeryViolation_LeaveColony, null, null, null, useColonistsFromCaravanArg: false, QuestPart.SignalListenMode.OngoingOnly, null, filterDeadPawnsFromLookTargets: false, "[lodgerViolatedLeaveMapLetterText]", null, "[lodgerViolatedLeaveMapLetterLabel]");
			quest.AddMemoryThought(pawns, ThoughtDefOf.OtherTravelerDied, questPart_RefugeeInteractions.outSignalDestroyed_BadThought);
			quest.AddMemoryThought(pawns, ThoughtDefOf.OtherTravelerArrested, questPart_RefugeeInteractions.outSignalArrested_BadThought);
			quest.AddMemoryThought(pawns, ThoughtDefOf.OtherTravelerSurgicallyViolated, questPart_RefugeeInteractions.outSignalSurgeryViolation_BadThought);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalDestroyed_AssaultColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalDestroyed_LeaveColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalLast_Destroyed);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalArrested_AssaultColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalArrested_LeaveColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalLast_Arrested);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalSurgeryViolation_AssaultColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalSurgeryViolation_LeaveColony, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalLast_Kidnapped, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Fail, 0, null, questPart_RefugeeInteractions.outSignalLast_Banished, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Success, 0, null, questPart_RefugeeInteractions.outSignalLast_Recruited, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.End(QuestEndOutcome.Success, 0, null, questPart_RefugeeInteractions.outSignalLast_LeftMapAllNotHealthy, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			quest.SignalPass(delegate
			{
				if (Rand.Chance(0.5f))
				{
					float num3 = (float)(lodgerCount * questDurationDays) * 55f;
					FloatRange marketValueRange = new FloatRange(0.7f, 1.3f) * num3 * Find.Storyteller.difficulty.EffectiveQuestRewardValueFactor;
					quest.AddQuestRefugeeDelayedReward(quest.AccepterPawn, faction, pawns, marketValueRange);
				}
				quest.End(QuestEndOutcome.Success, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			}, questPart_RefugeeInteractions.outSignalLast_LeftMapAllHealthy);
			slate.Set("lodgerCount", lodgerCount);
			slate.Set("lodgersCountMinusOne", lodgerCount - 1);
			slate.Set("asker", asker);
			slate.Set("map", map);
			slate.Set("questDurationTicks", questDurationTicks);
			slate.Set("faction", faction);
			slate.Set("childCount", num2);
			slate.Set("allButOneChildren", var);
		}

		protected override bool TestRunInt(Slate slate)
		{
			return QuestGen_Get.GetMap() != null;
		}
	}
}
