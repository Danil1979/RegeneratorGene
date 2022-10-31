using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace RimWorld
{
	public abstract class GenStep_SpecialTrees : GenStep
	{
		protected ThingDef treeDef;

		protected int minProximityToSameTree;

		private const int MinDistanceToEdge = 25;

		public override void Generate(Map map, GenStepParams parms)
		{
			if (map.Biome.isExtremeBiome)
			{
				return;
			}
			int num = DesiredTreeCountForMap(map);
			int num2 = 0;
			IntVec3 result;
			while (num > 0 && CellFinderLoose.TryFindRandomNotEdgeCellWith(25, (IntVec3 x) => CanSpawnAt(x, map, 0, 50, minProximityToSameTree), map, out result))
			{
				if (TrySpawnAt(result, map, GetGrowth(), out var _))
				{
					num--;
				}
				num2++;
				if (num2 > 1000)
				{
					Log.Error("Could not place " + treeDef.label + "; too many iterations.");
					break;
				}
			}
		}

		protected abstract float GetGrowth();

		public virtual bool TrySpawnAt(IntVec3 cell, Map map, float growth, out Thing plant)
		{
			cell.GetPlant(map)?.Destroy();
			plant = GenSpawn.Spawn(treeDef, cell, map);
			((Plant)plant).Growth = growth;
			return plant != null;
		}

		public abstract int DesiredTreeCountForMap(Map map);

		public virtual bool CanSpawnAt(IntVec3 c, Map map, int minProximityToArtificialStructures = 40, int minProximityToCenter = 0, int minFertileUnroofedCells = 22, int maxFertileUnroofedCellRadius = 10, int minProximityToSameTree = 0)
		{
			if (!c.Standable(map) || c.Fogged(map) || !c.GetRoom(map).PsychologicallyOutdoors)
			{
				return false;
			}
			Plant plant = c.GetPlant(map);
			if (plant != null && plant.def.plant.growDays > 10f)
			{
				return false;
			}
			List<Thing> thingList = c.GetThingList(map);
			for (int i = 0; i < thingList.Count; i++)
			{
				if (thingList[i].def == treeDef)
				{
					return false;
				}
			}
			if (minProximityToCenter > 0 && map.Center.InHorDistOf(c, minProximityToCenter))
			{
				return false;
			}
			if (!map.reachability.CanReachFactionBase(c, map.ParentFaction))
			{
				return false;
			}
			if (c.GetTerrain(map).avoidWander)
			{
				return false;
			}
			if (c.GetFertility(map) <= 0f)
			{
				return false;
			}
			if (c.Roofed(map))
			{
				return false;
			}
			if (minProximityToArtificialStructures != 0 && GenRadial.RadialDistinctThingsAround(c, map, minProximityToArtificialStructures, useCenter: false).Any(MeditationUtility.CountsAsArtificialBuilding))
			{
				return false;
			}
			if (minProximityToSameTree > 0 && GenRadial.RadialDistinctThingsAround(c, map, minProximityToSameTree, useCenter: false).Any((Thing t) => t.def == treeDef))
			{
				return false;
			}
			int num = GenRadial.NumCellsInRadius(maxFertileUnroofedCellRadius);
			int num2 = 0;
			for (int j = 0; j < num; j++)
			{
				IntVec3 intVec = c + GenRadial.RadialPattern[j];
				if (WanderUtility.InSameRoom(intVec, c, map))
				{
					if (intVec.InBounds(map) && !intVec.Roofed(map) && intVec.GetFertility(map) > 0f)
					{
						num2++;
					}
					if (num2 >= minFertileUnroofedCells)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
