using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWorld.QuestGen
{
	public class QuestNode_Root_PollutionDump : QuestNode
	{
		private const int DropPodsDelayTicks = 2500;

		private static readonly SimpleCurve WastepackCountOverPointsCurve = new SimpleCurve
		{
			new CurvePoint(200f, 90f),
			new CurvePoint(400f, 150f),
			new CurvePoint(800f, 225f),
			new CurvePoint(1600f, 325f),
			new CurvePoint(3200f, 400f),
			new CurvePoint(20000f, 1000f)
		};

		protected override bool TestRunInt(Slate slate)
		{
			return QuestGen_Get.GetMap() != null;
		}

		protected override void RunInt()
		{
			Quest quest = QuestGen.quest;
			Slate slate = QuestGen.slate;
			Map map = QuestGen_Get.GetMap();
			slate.Set("map", map);
			float x = slate.Get("points", 0f);
			Pawn asker = FindAsker();
			int wastepackCount = Mathf.RoundToInt(WastepackCountOverPointsCurve.Evaluate(x));
			quest.Delay(2500, delegate
			{
				List<Thing> list = new List<Thing>();
				for (int i = 0; i < wastepackCount; i++)
				{
					list.Add(ThingMaker.MakeThing(ThingDefOf.Wastepack));
				}
				quest.DropPods(map.Parent, list, "[wastepacksLetterLabel]", null, "[wastepacksLetterText]", null, true, useTradeDropSpot: false, joinPlayer: false, makePrisoners: false, null, null, QuestPart.SignalListenMode.OngoingOnly, null, destroyItemsOnCleanup: true, dropAllInSamePod: true);
				QuestScriptDefOf.Util_GetDefaultRewardValueFromPoints.Run();
				float rewardValue = slate.Get("rewardValue", 0f);
				quest.GiveRewards(new RewardsGeneratorParams
				{
					rewardValue = rewardValue,
					thingRewardItemsOnly = true
				}, null, null, null, null, null, null, null, null, addCampLootReward: false, asker);
				quest.End(QuestEndOutcome.Success, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
			});
			slate.Set("asker", asker);
			slate.Set("askerIsNull", asker == null);
			slate.Set("wastepackCount", wastepackCount);
		}

		private Pawn FindAsker()
		{
			if (Find.FactionManager.AllFactionsVisible.Where((Faction f) => !f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && (int)f.def.techLevel > 2).TryRandomElement(out var result))
			{
				return result.leader;
			}
			return null;
		}
	}
}
