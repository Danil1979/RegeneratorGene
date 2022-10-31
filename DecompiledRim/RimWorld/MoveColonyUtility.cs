using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWorld
{
	public static class MoveColonyUtility
	{
		private static StringBuilder cannotPlaceTileReason = new StringBuilder();

		public const int TitleAndRoleRequirementGracePeriodTicks = 600000;

		private static List<int> playerSettlementsRemoved = new List<int>();

		public static bool TitleAndRoleRequirementsGracePeriodActive => TitleAndRoleRequirementGracePeriodTicksLeft > 0;

		public static int TitleAndRoleRequirementGracePeriodTicksLeft
		{
			get
			{
				if (!Find.TickManager.HasSettledNewColony)
				{
					return 0;
				}
				return Mathf.Max(0, 600000 - Find.TickManager.TicksSinceSettle);
			}
		}

		public static void PickNewColonyTile(Action<int> targetChosen, Action noTileChosen = null)
		{
			Find.TilePicker.StartTargeting(delegate(int tile)
			{
				cannotPlaceTileReason.Clear();
				if (!TileFinder.IsValidTileForNewSettlement(tile, cannotPlaceTileReason))
				{
					Messages.Message(cannotPlaceTileReason.ToString(), MessageTypeDefOf.RejectInput, historical: false);
					return false;
				}
				return true;
			}, delegate(int tile)
			{
				Find.World.renderer.wantedMode = WorldRenderMode.None;
				targetChosen(tile);
			}, allowEscape: false, noTileChosen, "ChooseNextColonySite".Translate());
		}

		public static Settlement MoveColonyAndReset(int tile, IEnumerable<Thing> colonyThings, Faction takeoverFaction = null, WorldObjectDef worldObjectDef = null)
		{
			foreach (Quest item in Find.QuestManager.QuestsListForReading)
			{
				if (item.IsEndOnNewArchonexusSettlement())
				{
					item.hidden = true;
					item.End(QuestEndOutcome.Unknown, sendLetter: false);
				}
			}
			foreach (Pawn item2 in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_OfPlayerFaction.ToList())
			{
				if (colonyThings.Contains(item2))
				{
					continue;
				}
				item2.DeSpawnOrDeselect();
				if (item2.IsCaravanMember())
				{
					item2.GetCaravan().RemovePawn(item2);
				}
				if (item2.holdingOwner != null)
				{
					item2.holdingOwner.Remove(item2);
				}
				if (!item2.IsWorldPawn())
				{
					Find.WorldPawns.PassToWorld(item2);
				}
				if (ModsConfig.BiotechActive && item2.RaceProps.IsMechanoid)
				{
					Pawn overseer = item2.GetOverseer();
					if (overseer != null && colonyThings.Contains(overseer))
					{
						overseer.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, item2);
					}
				}
			}
			List<Caravan> caravans = Find.WorldObjects.Caravans;
			for (int num = caravans.Count - 1; num >= 0; num--)
			{
				if (caravans[num].IsPlayerControlled)
				{
					caravans[num].RemoveAllPawns();
					caravans[num].Destroy();
				}
			}
			List<TravelingTransportPods> travelingTransportPods = Find.WorldObjects.TravelingTransportPods;
			for (int num2 = travelingTransportPods.Count - 1; num2 >= 0; num2--)
			{
				travelingTransportPods[num2].Destroy();
			}
			foreach (Thing colonyThing in colonyThings)
			{
				colonyThing.DeSpawnOrDeselect();
				if (colonyThing.holdingOwner != null)
				{
					colonyThing.holdingOwner.Remove(colonyThing);
				}
			}
			List<MapParent> mapParents = Find.World.worldObjects.MapParents;
			for (int num3 = mapParents.Count - 1; num3 >= 0; num3--)
			{
				mapParents[num3].CheckRemoveMapNow();
			}
			playerSettlementsRemoved.Clear();
			List<Map> maps = Find.Maps;
			for (int num4 = maps.Count - 1; num4 >= 0; num4--)
			{
				Map map = maps[num4];
				if (map.IsPlayerHome)
				{
					playerSettlementsRemoved.Add(map.Tile);
					map.Parent.SetFaction(null);
					Current.Game.DeinitAndRemoveMap(map);
					map.Parent.Destroy();
				}
			}
			if (ModsConfig.IdeologyActive)
			{
				List<Site> sites = Find.WorldObjects.Sites;
				for (int num5 = sites.Count - 1; num5 >= 0; num5--)
				{
					if (sites[num5].parts.Any((SitePart p) => p.def == SitePartDefOf.AncientComplex))
					{
						Find.WorldObjects.Remove(sites[num5]);
					}
				}
			}
			Find.GameInfo.startingTile = tile;
			WorldObjectDef worldObjectDef2 = worldObjectDef ?? WorldObjectDefOf.Settlement;
			Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(worldObjectDef2);
			settlement.SetFaction(Faction.OfPlayer);
			settlement.Tile = tile;
			settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement, Faction.OfPlayer.def.playerInitialSettlementNameMaker);
			Find.WorldObjects.Add(settlement);
			Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, worldObjectDef2);
			IntVec3 playerStartSpot = MapGenerator.PlayerStartSpot;
			List<List<Thing>> list = new List<List<Thing>>();
			List<Pawn> list2 = new List<Pawn>();
			foreach (Thing colonyThing2 in colonyThings)
			{
				if (colonyThing2 is Pawn)
				{
					list.Add(new List<Thing> { colonyThing2 });
					list2.Add((Pawn)colonyThing2);
				}
			}
			int num6 = 0;
			foreach (Thing thing in colonyThings)
			{
				if (thing.def.CanHaveFaction && thing.Faction != Faction.OfPlayer)
				{
					Pawn pawn;
					if ((pawn = thing as Pawn) != null && pawn.IsSlaveOfColony)
					{
						continue;
					}
					thing.SetFaction(Faction.OfPlayer);
				}
				if (!list.Any((List<Thing> g) => g.Contains(thing)))
				{
					list[num6].Add(thing);
					num6 = (num6 + 1) % list.Count;
				}
			}
			foreach (Pawn item3 in list2)
			{
				item3.inventory.DestroyAll();
				item3.ownership.UnclaimThrone();
			}
			RemoveWeaponsAndUtilityItems(list2, colonyThings);
			foreach (Thing abandonedRelicsCarriedByPawn in GetAbandonedRelicsCarriedByPawns(colonyThings))
			{
				if (!abandonedRelicsCarriedByPawn.DestroyedOrNull())
				{
					abandonedRelicsCarriedByPawn.Destroy();
				}
			}
			DropPodUtility.DropThingGroupsNear(playerStartSpot, orGenerateMap, list);
			if (takeoverFaction != null)
			{
				foreach (int item4 in playerSettlementsRemoved)
				{
					SettleUtility.AddNewHome(item4, takeoverFaction);
				}
			}
			playerSettlementsRemoved.Clear();
			List<ResearchProjectDef> list3 = DefDatabase<ResearchProjectDef>.AllDefs.Where((ResearchProjectDef proj) => proj.IsFinished && proj.HasTag(ResearchProjectTagDefOf.ClassicStart)).ToList();
			Find.ResearchManager.ResetAllProgress();
			Find.StudyManager.ResetAllProgress();
			ResearchUtility.ApplyPlayerStartingResearch();
			foreach (ResearchProjectDef item5 in list3)
			{
				Find.ResearchManager.FinishProject(item5, doCompletionDialog: false, null, doCompletionLetter: false);
			}
			FactionUtility.ResetAllFactionRelations();
			if (ModsConfig.BiotechActive)
			{
				Current.Game.GetComponent<GameComponent_Bossgroup>()?.ResetProgress();
			}
			SetPawnThoughts(list2);
			ResetNeedLevels(list2);
			RemoveHediffs(list2);
			ResetStartingGracePeriods(list2);
			Find.FactionManager.OfPlayer.ideos.RecalculateIdeosBasedOnPlayerPawns();
			IdeoUtility.Notify_NewColonyStarted();
			return settlement;
		}

		public static IEnumerable<Thing> GetStartingThingsForNewColony()
		{
			foreach (ScenPart allPart in Find.Scenario.AllParts)
			{
				ScenPart_StartingThing_Defined scenPart_StartingThing_Defined;
				if ((scenPart_StartingThing_Defined = allPart as ScenPart_StartingThing_Defined) == null)
				{
					continue;
				}
				foreach (Thing item in scenPart_StartingThing_Defined.PlayerStartingThings())
				{
					yield return item;
				}
			}
		}

		public static bool IsBringableItem(Thing t)
		{
			if (t.def.destroyOnDrop)
			{
				return false;
			}
			if (t.IsRelic())
			{
				return false;
			}
			if (t.Map != null && t.Position.Fogged(t.Map))
			{
				return false;
			}
			return true;
		}

		public static bool IsDistinctArchonexusItem(ThingDef td)
		{
			if (((!td.IsWeapon && !td.IsApparel) || !td.HasComp(typeof(CompQuality))) && (td == null || td.weaponTags?.Contains("Gun") != true))
			{
				if (td == null)
				{
					return false;
				}
				return td.apparel?.LastLayer?.IsUtilityLayer == true;
			}
			return true;
		}

		public static void RemoveWeaponsAndUtilityItems(List<Pawn> pawns, IEnumerable<Thing> selectedThings)
		{
			List<Thing> list = new List<Thing>();
			foreach (Pawn pawn in pawns)
			{
				if (pawn.equipment != null)
				{
					foreach (ThingWithComps item in pawn.equipment.AllEquipmentListForReading)
					{
						if (!item.DestroyedOrNull() && !selectedThings.Contains(item))
						{
							list.Add(item);
						}
					}
				}
				if (pawn.apparel == null)
				{
					continue;
				}
				foreach (Apparel item2 in pawn.apparel.WornApparel)
				{
					ThingDef def = item2.def;
					if (def != null && def.apparel?.LastLayer?.IsUtilityLayer == true && !selectedThings.Contains(item2))
					{
						list.Add(item2);
					}
				}
			}
			foreach (Thing item3 in list)
			{
				item3.Destroy();
			}
		}

		public static List<Thing> GetAbandonedRelicsCarriedByPawns(IEnumerable<Thing> selectedThings)
		{
			List<Thing> list = new List<Thing>();
			List<Pawn> list2 = new List<Pawn>();
			foreach (Thing selectedThing in selectedThings)
			{
				if (selectedThing.IsRelic())
				{
					list.Add(selectedThing);
				}
				if (selectedThing is Pawn)
				{
					list2.Add((Pawn)selectedThing);
				}
			}
			List<Thing> list3 = new List<Thing>();
			foreach (Pawn item in list2)
			{
				foreach (Thing equippedWornOrInventoryThing in item.EquippedWornOrInventoryThings)
				{
					if (equippedWornOrInventoryThing.IsRelic() && !list.Contains(equippedWornOrInventoryThing))
					{
						list3.Add(equippedWornOrInventoryThing);
					}
				}
			}
			return list3;
		}

		private static void ResetStartingGracePeriods(List<Pawn> pawns)
		{
			Find.TickManager.ResetSettlementTicks();
			Find.StoryWatcher.watcherAdaptation.ResetAdaptDays();
			Find.StoryWatcher.watcherPopAdaptation.ResetAdaptDays();
			foreach (Pawn pawn in pawns)
			{
				if (pawn.IsColonist)
				{
					pawn.ageTracker.ResetAgeReversalDemand(Pawn_AgeTracker.AgeReversalReason.Initial);
				}
			}
		}

		private static void SetPawnThoughts(List<Pawn> pawns)
		{
			foreach (Pawn pawn in pawns)
			{
				MemoryThoughtHandler memoryThoughtHandler = pawn.needs?.mood?.thoughts?.memories;
				if (memoryThoughtHandler != null)
				{
					memoryThoughtHandler.RemoveMemoriesOfDef(ThoughtDefOf.NewColonyOptimism);
					memoryThoughtHandler.RemoveMemoriesOfDef(ThoughtDefOf.NewColonyHope);
					if (pawn.IsFreeNonSlaveColonist)
					{
						memoryThoughtHandler.TryGainMemory(ThoughtDefOf.NewColonyOptimism);
					}
				}
			}
		}

		private static void ResetNeedLevels(List<Pawn> pawns)
		{
			foreach (Pawn pawn in pawns)
			{
				pawn.needs?.food?.SetInitialLevel();
				pawn.needs?.rest?.SetInitialLevel();
			}
		}

		private static void RemoveHediffs(List<Pawn> pawns)
		{
			List<Hediff> list = new List<Hediff>();
			foreach (Pawn pawn in pawns)
			{
				list.Clear();
				foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
				{
					if (hediff.def == HediffDefOf.Hypothermia || hediff.def == HediffDefOf.Heatstroke || hediff.def == HediffDefOf.Malnutrition)
					{
						list.Add(hediff);
					}
				}
				foreach (Hediff item in list)
				{
					pawn.health.RemoveHediff(item);
				}
			}
		}
	}
}
