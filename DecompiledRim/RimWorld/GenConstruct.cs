using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimWorld
{
	public static class GenConstruct
	{
		public const float ConstructionSpeedGlobalFactor = 1.7f;

		private static string SkillTooLowTrans;

		private static string IncapableOfDeconstruction;

		private static string IncapableOfMining;

		private static List<string> tmpIdeoMemberNames = new List<string>();

		public static void Reset()
		{
			SkillTooLowTrans = "SkillTooLowForConstruction".Translate();
			IncapableOfDeconstruction = "IncapableOfDeconstruction".Translate();
			IncapableOfMining = "IncapableOfMining".Translate();
		}

		public static Blueprint_Build PlaceBlueprintForBuild(BuildableDef sourceDef, IntVec3 center, Map map, Rot4 rotation, Faction faction, ThingDef stuff, Precept_ThingStyle styleSource = null, ThingStyleDef styleDef = null, bool sendBPSpawnedSignal = true)
		{
			Blueprint_Build blueprint_Build = (Blueprint_Build)ThingMaker.MakeThing(sourceDef.blueprintDef);
			blueprint_Build.SetFactionDirect(faction);
			blueprint_Build.stuffToUse = stuff;
			blueprint_Build.InheritStyle(styleSource, styleDef);
			GenSpawn.Spawn(blueprint_Build, center, map, rotation);
			if (faction != null && sendBPSpawnedSignal)
			{
				QuestUtility.SendQuestTargetSignals(faction.questTags, "PlacedBlueprint", blueprint_Build.Named("SUBJECT"));
			}
			return blueprint_Build;
		}

		public static Blueprint_Install PlaceBlueprintForInstall(MinifiedThing itemToInstall, IntVec3 center, Map map, Rot4 rotation, Faction faction, bool sendBPSpawnedSignal = true)
		{
			Blueprint_Install blueprint_Install = (Blueprint_Install)ThingMaker.MakeThing(itemToInstall.InnerThing.def.installBlueprintDef);
			blueprint_Install.SetThingToInstallFromMinified(itemToInstall);
			blueprint_Install.SetFactionDirect(faction);
			GenSpawn.Spawn(blueprint_Install, center, map, rotation);
			if (faction != null && sendBPSpawnedSignal)
			{
				QuestUtility.SendQuestTargetSignals(faction.questTags, "PlacedBlueprint", blueprint_Install.Named("SUBJECT"));
			}
			return blueprint_Install;
		}

		public static Blueprint_Install PlaceBlueprintForReinstall(Building buildingToReinstall, IntVec3 center, Map map, Rot4 rotation, Faction faction, bool sendBPSpawnedSignal = true)
		{
			Blueprint_Install blueprint_Install = (Blueprint_Install)ThingMaker.MakeThing(buildingToReinstall.def.installBlueprintDef);
			blueprint_Install.SetBuildingToReinstall(buildingToReinstall);
			blueprint_Install.SetFactionDirect(faction);
			GenSpawn.Spawn(blueprint_Install, center, map, rotation);
			if (faction != null && sendBPSpawnedSignal)
			{
				QuestUtility.SendQuestTargetSignals(faction.questTags, "PlacedBlueprint", blueprint_Install.Named("SUBJECT"));
			}
			return blueprint_Install;
		}

		public static bool CanBuildOnTerrain(BuildableDef entDef, IntVec3 c, Map map, Rot4 rot, Thing thingToIgnore = null, ThingDef stuffDef = null)
		{
			if (entDef is TerrainDef && !c.GetTerrain(map).changeable)
			{
				return false;
			}
			TerrainAffordanceDef terrainAffordanceNeed = entDef.GetTerrainAffordanceNeed(stuffDef);
			if (terrainAffordanceNeed != null)
			{
				CellRect cellRect = GenAdj.OccupiedRect(c, rot, entDef.Size);
				cellRect.ClipInsideMap(map);
				foreach (IntVec3 item in cellRect)
				{
					if (!map.terrainGrid.TerrainAt(item).affordances.Contains(terrainAffordanceNeed))
					{
						return false;
					}
					List<Thing> thingList = item.GetThingList(map);
					for (int i = 0; i < thingList.Count; i++)
					{
						if (thingList[i] != thingToIgnore)
						{
							TerrainDef terrainDef = thingList[i].def.entityDefToBuild as TerrainDef;
							if (terrainDef != null && !terrainDef.affordances.Contains(terrainAffordanceNeed))
							{
								return false;
							}
						}
					}
				}
			}
			return true;
		}

		public static Thing MiniToInstallOrBuildingToReinstall(Blueprint b)
		{
			return (b as Blueprint_Install)?.MiniToInstallOrBuildingToReinstall;
		}

		public static bool CanConstruct(Thing t, Pawn pawn, WorkTypeDef workType, bool forced = false)
		{
			if (!pawn.workSettings.WorkIsActive(workType))
			{
				JobFailReason.Is("NotAssignedToWorkType".Translate(workType.gerundLabel).CapitalizeFirst());
				return false;
			}
			return CanConstruct(t, pawn, workType == WorkTypeDefOf.Construction, forced);
		}

		public static bool CanConstruct(Thing t, Pawn p, bool checkSkills = true, bool forced = false)
		{
			tmpIdeoMemberNames.Clear();
			if (FirstBlockingThing(t, p) != null)
			{
				return false;
			}
			if (!p.CanReserveAndReach(t, PathEndMode.Touch, forced ? Danger.Deadly : p.NormalMaxDanger(), 1, -1, null, forced))
			{
				return false;
			}
			if (t.IsBurning())
			{
				return false;
			}
			if (checkSkills)
			{
				if (p.skills != null)
				{
					if (p.skills.GetSkill(SkillDefOf.Construction).Level < t.def.constructionSkillPrerequisite)
					{
						JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Construction.LabelCap));
						return false;
					}
					if (p.skills.GetSkill(SkillDefOf.Artistic).Level < t.def.artisticSkillPrerequisite)
					{
						JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Artistic.LabelCap));
						return false;
					}
				}
				if (p.IsColonyMech)
				{
					if (p.RaceProps.mechFixedSkillLevel < t.def.constructionSkillPrerequisite)
					{
						JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Construction.LabelCap));
						return false;
					}
					if (p.RaceProps.mechFixedSkillLevel < t.def.artisticSkillPrerequisite)
					{
						JobFailReason.Is(SkillTooLowTrans.Formatted(SkillDefOf.Artistic.LabelCap));
						return false;
					}
				}
			}
			bool flag = t is Blueprint_Install;
			if (p.Ideo != null && !p.Ideo.MembersCanBuild(t) && !flag)
			{
				foreach (Ideo item in Find.IdeoManager.IdeosListForReading)
				{
					if (item.MembersCanBuild(t))
					{
						tmpIdeoMemberNames.Add(item.memberName);
					}
				}
				if (tmpIdeoMemberNames.Any())
				{
					JobFailReason.Is("OnlyMembersCanBuild".Translate(tmpIdeoMemberNames.ToCommaList(useAnd: true)));
				}
				return false;
			}
			ThingDef thingDef;
			if ((t.def.IsBlueprint || t.def.IsFrame) && (thingDef = t.def.entityDefToBuild as ThingDef) != null && thingDef.building != null && thingDef.building.IsTurret && !thingDef.HasComp(typeof(CompMannable)) && !new HistoryEvent(HistoryEventDefOf.BuiltAutomatedTurret, p.Named(HistoryEventArgsNames.Doer)).Notify_PawnAboutToDo_Job())
			{
				return false;
			}
			return true;
		}

		public static int AmountNeededByOf(IConstructible c, ThingDef resDef)
		{
			foreach (ThingDefCountClass item in c.MaterialsNeeded())
			{
				if (item.thingDef == resDef)
				{
					return item.count;
				}
			}
			return 0;
		}

		public static AcceptanceReport CanPlaceBlueprintAt(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map, bool godMode = false, Thing thingToIgnore = null, Thing thing = null, ThingDef stuffDef = null)
		{
			CellRect cellRect = GenAdj.OccupiedRect(center, rot, entDef.Size);
			if (stuffDef == null && thing != null)
			{
				stuffDef = thing.Stuff;
			}
			foreach (IntVec3 item in cellRect)
			{
				if (!item.InBounds(map))
				{
					return new AcceptanceReport("OutOfBounds".Translate());
				}
				if (item.InNoBuildEdgeArea(map) && !godMode)
				{
					return "TooCloseToMapEdge".Translate();
				}
			}
			if (center.Fogged(map))
			{
				return "CannotPlaceInUndiscovered".Translate();
			}
			List<Thing> thingList = center.GetThingList(map);
			for (int i = 0; i < thingList.Count; i++)
			{
				Thing thing2 = thingList[i];
				if (thing2 == thingToIgnore || !(thing2.Position == center) || !(thing2.Rotation == rot))
				{
					continue;
				}
				if (thing2.def == entDef)
				{
					return new AcceptanceReport("IdenticalThingExists".Translate());
				}
				if (thing2.def.entityDefToBuild == entDef)
				{
					if (thing2 is Blueprint)
					{
						return new AcceptanceReport("IdenticalBlueprintExists".Translate());
					}
					return new AcceptanceReport("IdenticalThingExists".Translate());
				}
			}
			ThingDef thingDef = entDef as ThingDef;
			if (thingDef != null && thingDef.HasSingleOrMultipleInteractionCells)
			{
				foreach (IntVec3 item2 in ThingUtility.InteractionCellsWhenAt(thingDef, center, rot, map))
				{
					if (!item2.InBounds(map))
					{
						return new AcceptanceReport("InteractionSpotOutOfBounds".Translate());
					}
					List<Thing> list = map.thingGrid.ThingsListAtFast(item2);
					for (int j = 0; j < list.Count; j++)
					{
						if (list[j] != thingToIgnore)
						{
							if (list[j].def.passability != 0 || list[j].def == thingDef)
							{
								return new AcceptanceReport("InteractionSpotBlocked".Translate(list[j].LabelNoCount, list[j]).CapitalizeFirst());
							}
							BuildableDef entityDefToBuild = list[j].def.entityDefToBuild;
							if (entityDefToBuild != null && (entityDefToBuild.passability != 0 || entityDefToBuild == thingDef))
							{
								return new AcceptanceReport("InteractionSpotWillBeBlocked".Translate(list[j].LabelNoCount, list[j]).CapitalizeFirst());
							}
						}
					}
				}
			}
			foreach (IntVec3 item3 in GenAdj.CellsAdjacentCardinal(center, rot, entDef.Size))
			{
				if (!item3.InBounds(map))
				{
					continue;
				}
				thingList = item3.GetThingList(map);
				for (int k = 0; k < thingList.Count; k++)
				{
					Thing thing3 = thingList[k];
					if (thing3 == thingToIgnore)
					{
						continue;
					}
					ThingDef thingDef2 = null;
					Blueprint blueprint = thing3 as Blueprint;
					if (blueprint != null)
					{
						ThingDef thingDef3 = blueprint.def.entityDefToBuild as ThingDef;
						if (thingDef3 == null)
						{
							continue;
						}
						thingDef2 = thingDef3;
					}
					else
					{
						thingDef2 = thing3.def;
					}
					if (!thingDef2.HasSingleOrMultipleInteractionCells || (entDef.passability == Traversability.Standable && entDef != thingDef2))
					{
						continue;
					}
					foreach (IntVec3 item4 in ThingUtility.InteractionCellsWhenAt(thingDef2, thing3.Position, thing3.Rotation, thing3.Map))
					{
						if (cellRect.Contains(item4))
						{
							return new AcceptanceReport("WouldBlockInteractionSpot".Translate(entDef.label, thingDef2.label).CapitalizeFirst());
						}
					}
				}
			}
			TerrainDef terrainDef = entDef as TerrainDef;
			if (terrainDef != null)
			{
				if (map.terrainGrid.TerrainAt(center) == terrainDef)
				{
					return new AcceptanceReport("TerrainIsAlready".Translate(terrainDef.label));
				}
				if (map.designationManager.DesignationAt(center, DesignationDefOf.SmoothFloor) != null)
				{
					return new AcceptanceReport("SpaceBeingSmoothed".Translate());
				}
			}
			if (!CanBuildOnTerrain(entDef, center, map, rot, thingToIgnore, stuffDef))
			{
				if (entDef.GetTerrainAffordanceNeed(stuffDef) != null)
				{
					if (entDef.useStuffTerrainAffordance && stuffDef != null)
					{
						return new AcceptanceReport("TerrainCannotSupport_TerrainAffordanceFromStuff".Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef), stuffDef).CapitalizeFirst());
					}
					return new AcceptanceReport("TerrainCannotSupport_TerrainAffordance".Translate(entDef, entDef.GetTerrainAffordanceNeed(stuffDef)).CapitalizeFirst());
				}
				return new AcceptanceReport("TerrainCannotSupport".Translate(entDef).CapitalizeFirst());
			}
			if (ModsConfig.RoyaltyActive)
			{
				List<Thing> list2 = map.listerThings.ThingsOfDef(ThingDefOf.MonumentMarker);
				for (int l = 0; l < list2.Count; l++)
				{
					MonumentMarker monumentMarker = (MonumentMarker)list2[l];
					if (!monumentMarker.complete && !monumentMarker.AllowsPlacingBlueprint(entDef, center, rot, stuffDef))
					{
						return new AcceptanceReport("BlueprintWouldCollideWithMonument".Translate());
					}
				}
			}
			if (!godMode)
			{
				foreach (IntVec3 item5 in cellRect)
				{
					thingList = item5.GetThingList(map);
					for (int m = 0; m < thingList.Count; m++)
					{
						Thing thing4 = thingList[m];
						if (thing4 != thingToIgnore && !CanPlaceBlueprintOver(entDef, thing4.def))
						{
							return new AcceptanceReport("SpaceAlreadyOccupied".Translate());
						}
					}
				}
			}
			if (entDef.PlaceWorkers != null)
			{
				for (int n = 0; n < entDef.PlaceWorkers.Count; n++)
				{
					AcceptanceReport result = entDef.PlaceWorkers[n].AllowsPlacing(entDef, center, rot, map, thingToIgnore, thing);
					if (!result.Accepted)
					{
						return result;
					}
				}
			}
			return AcceptanceReport.WasAccepted;
		}

		public static BuildableDef BuiltDefOf(ThingDef def)
		{
			if (def.entityDefToBuild == null)
			{
				return def;
			}
			return def.entityDefToBuild;
		}

		public static bool CanPlaceBlueprintOver(BuildableDef newDef, ThingDef oldDef)
		{
			if (oldDef.EverHaulable)
			{
				return true;
			}
			TerrainDef terrainDef = newDef as TerrainDef;
			if (terrainDef != null)
			{
				if (oldDef.IsBlueprint || oldDef.IsFrame)
				{
					if (oldDef.entityDefToBuild.terrainAffordanceNeeded != null && !terrainDef.affordances.Contains(oldDef.entityDefToBuild.terrainAffordanceNeeded))
					{
						return false;
					}
				}
				else if (oldDef.category == ThingCategory.Building && oldDef.terrainAffordanceNeeded != null && !terrainDef.affordances.Contains(oldDef.terrainAffordanceNeeded))
				{
					return false;
				}
			}
			ThingDef newThingDef = newDef as ThingDef;
			ThingDef oldThingDef = oldDef;
			BuildableDef oldDefBuilt = BuiltDefOf(oldDef);
			ThingDef thingDef = oldDefBuilt as ThingDef;
			if (newDef.blocksAltitudes != null && newDef.blocksAltitudes.Contains(oldDef.altitudeLayer))
			{
				return false;
			}
			if (oldDefBuilt?.blocksAltitudes != null && oldDefBuilt.blocksAltitudes.Contains(newDef.altitudeLayer))
			{
				return false;
			}
			if (newDef.ForceAllowPlaceOver(oldDef))
			{
				return true;
			}
			if (oldDef.category == ThingCategory.Plant && oldDef.passability == Traversability.Impassable && newThingDef != null && newThingDef.category == ThingCategory.Building && !newThingDef.building.canPlaceOverImpassablePlant)
			{
				return false;
			}
			if (oldDef.category == ThingCategory.Building || oldDef.IsBlueprint || oldDef.IsFrame)
			{
				if (newThingDef != null)
				{
					if (!newThingDef.IsEdifice())
					{
						if (oldDef.building != null && !oldDef.building.canBuildNonEdificesUnder)
						{
							return false;
						}
						if (newThingDef.EverTransmitsPower && oldDef.EverTransmitsPower)
						{
							return false;
						}
						return true;
					}
					if (IsEdificeOverNonEdifice())
					{
						if (newThingDef.building != null && !newThingDef.building.canBuildNonEdificesUnder)
						{
							return false;
						}
						return true;
					}
					if (thingDef?.building != null && (thingDef.building.isPlaceOverableWall || thingDef.IsSmoothed) && newThingDef.building != null && newThingDef.building.canPlaceOverWall)
					{
						return true;
					}
					if (newDef != ThingDefOf.PowerConduit && oldDefBuilt == ThingDefOf.PowerConduit)
					{
						return true;
					}
				}
				if (newDef is TerrainDef && oldDefBuilt is ThingDef && ((ThingDef)oldDefBuilt).CoexistsWithFloors)
				{
					return true;
				}
				if (oldDefBuilt is TerrainDef && !(newDef is TerrainDef))
				{
					return true;
				}
				return false;
			}
			return true;
			bool IsEdificeOverNonEdifice()
			{
				if (!newThingDef.IsEdifice())
				{
					return false;
				}
				if (oldThingDef != null && oldThingDef.category == ThingCategory.Building && !oldThingDef.IsEdifice())
				{
					return true;
				}
				ThingDef thingDef2;
				if (oldDefBuilt != null && (thingDef2 = oldDefBuilt as ThingDef) != null && thingDef2.category == ThingCategory.Building && !thingDef2.IsEdifice())
				{
					return true;
				}
				return false;
			}
		}

		public static Thing FirstBlockingThing(Thing constructible, Pawn pawnToIgnore)
		{
			Blueprint blueprint = constructible as Blueprint;
			Thing thing = ((blueprint == null) ? null : MiniToInstallOrBuildingToReinstall(blueprint));
			foreach (IntVec3 item in constructible.OccupiedRect())
			{
				List<Thing> thingList = item.GetThingList(constructible.Map);
				for (int i = 0; i < thingList.Count; i++)
				{
					Thing thing2 = thingList[i];
					if (BlocksConstruction(constructible, thing2) && thing2 != pawnToIgnore && thing2 != thing)
					{
						return thing2;
					}
				}
			}
			return null;
		}

		public static Job HandleBlockingThingJob(Thing constructible, Pawn worker, bool forced = false)
		{
			Thing thing = FirstBlockingThing(constructible, worker);
			if (thing == null)
			{
				return null;
			}
			if (thing.def.category == ThingCategory.Plant)
			{
				if (!PlantUtility.PawnWillingToCutPlant_Job(thing, worker))
				{
					return null;
				}
				if (PlantUtility.TreeMarkedForExtraction(thing))
				{
					return null;
				}
				if (worker.CanReserveAndReach(thing, PathEndMode.ClosestTouch, worker.NormalMaxDanger(), 1, -1, null, forced))
				{
					return JobMaker.MakeJob(JobDefOf.CutPlant, thing);
				}
			}
			else if (thing.def.category == ThingCategory.Item)
			{
				if (thing.def.EverHaulable)
				{
					return HaulAIUtility.HaulAsideJobFor(worker, thing);
				}
				Log.ErrorOnce(string.Concat("Never haulable ", thing, " blocking ", constructible.ToStringSafe(), " at ", constructible.Position), 6429262);
			}
			else if (thing.def.category == ThingCategory.Building)
			{
				if (((Building)thing).DeconstructibleBy(worker.Faction))
				{
					if (worker.WorkTypeIsDisabled(WorkTypeDefOf.Construction) || (worker.workSettings != null && !worker.workSettings.WorkIsActive(WorkTypeDefOf.Construction)))
					{
						JobFailReason.Is(IncapableOfDeconstruction);
						return null;
					}
					if (worker.CanReserveAndReach(thing, PathEndMode.Touch, worker.NormalMaxDanger(), 1, -1, null, forced))
					{
						Job job = JobMaker.MakeJob(JobDefOf.Deconstruct, thing);
						job.ignoreDesignations = true;
						return job;
					}
				}
				if (thing.def.mineable)
				{
					if (worker.WorkTypeIsDisabled(WorkTypeDefOf.Mining) || (worker.workSettings != null && !worker.workSettings.WorkIsActive(WorkTypeDefOf.Mining)))
					{
						JobFailReason.Is(IncapableOfMining);
						return null;
					}
					if (worker.CanReserveAndReach(thing, PathEndMode.Touch, worker.NormalMaxDanger(), 1, -1, null, forced))
					{
						Job job2 = JobMaker.MakeJob(JobDefOf.Mine, thing);
						job2.ignoreDesignations = true;
						return job2;
					}
				}
			}
			return null;
		}

		public static bool BlocksConstruction(Thing constructible, Thing t)
		{
			if (t == constructible)
			{
				return false;
			}
			ThingDef thingDef = ((constructible is Blueprint) ? constructible.def : ((!(constructible is Frame)) ? constructible.def.blueprintDef : constructible.def.entityDefToBuild.blueprintDef));
			if (t.def.category == ThingCategory.Building && GenSpawn.SpawningWipes(thingDef.entityDefToBuild, t.def))
			{
				return true;
			}
			if (t.def.category == ThingCategory.Plant)
			{
				if (t.def.plant.harvestWork > ThingDefOf.Plant_Dandelion.plant.harvestWork)
				{
					return !(thingDef.entityDefToBuild is TerrainDef) || !t.Spawned || !(t.Position.GetEdifice(t.Map) is IPlantToGrowSettable);
				}
				return false;
			}
			if (!thingDef.clearBuildingArea)
			{
				return false;
			}
			if (thingDef.entityDefToBuild.ForceAllowPlaceOver(t.def))
			{
				return false;
			}
			ThingDef thingDef2 = thingDef.entityDefToBuild as ThingDef;
			if (thingDef2 != null)
			{
				if (thingDef2.EverTransmitsPower && t.def == ThingDefOf.PowerConduit && thingDef2 != ThingDefOf.PowerConduit)
				{
					return false;
				}
				if (t.def == ThingDefOf.Wall && thingDef2.building != null && thingDef2.building.canPlaceOverWall)
				{
					return false;
				}
				if (t.def.category == ThingCategory.Item && thingDef2.passability != 0 && thingDef2.surfaceType == SurfaceType.None)
				{
					return true;
				}
			}
			if (t.def.IsEdifice() && thingDef2.IsEdifice())
			{
				return true;
			}
			if (t.def.category == ThingCategory.Pawn || (t.def.category == ThingCategory.Item && thingDef.entityDefToBuild.passability == Traversability.Impassable))
			{
				return true;
			}
			if ((int)t.def.Fillage >= 1 && (t.def.IsEdifice() || (t.def.entityDefToBuild != null && t.def.entityDefToBuild.IsEdifice())))
			{
				if (thingDef2.blocksAltitudes == null)
				{
					return true;
				}
				return (t.def.blocksAltitudes ?? t.def.entityDefToBuild?.blocksAltitudes)?.SharesElementWith(thingDef2.blocksAltitudes) ?? false;
			}
			return false;
		}

		public static bool TerrainCanSupport(CellRect rect, Map map, ThingDef thing)
		{
			foreach (IntVec3 item in rect)
			{
				if (!item.SupportsStructureType(map, thing.terrainAffordanceNeeded))
				{
					return false;
				}
			}
			return true;
		}
	}
}
