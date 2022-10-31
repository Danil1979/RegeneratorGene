using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimWorld
{
	public class Alert_Biostarvation : Alert
	{
		private List<GlobalTargetInfo> targets = new List<GlobalTargetInfo>();

		public List<GlobalTargetInfo> Targets
		{
			get
			{
				targets.Clear();
				List<Map> maps = Find.Maps;
				for (int i = 0; i < maps.Count; i++)
				{
					foreach (Building_GrowthVat item in maps[i].listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.GrowthVat))
					{
						if (item.Working && item.BiostarvationDailyOffset > 0f)
						{
							targets.Add(item);
						}
					}
				}
				return targets;
			}
		}

		public Alert_Biostarvation()
		{
			defaultLabel = "Biostarvation".Translate().CapitalizeFirst();
			defaultExplanation = "BiostarvationExplanation".Translate();
			defaultPriority = AlertPriority.High;
		}

		public override AlertReport GetReport()
		{
			if (!ModsConfig.BiotechActive)
			{
				return false;
			}
			return AlertReport.CulpritsAre(Targets);
		}
	}
}
