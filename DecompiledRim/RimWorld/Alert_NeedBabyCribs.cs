using Verse;

namespace RimWorld
{
	public class Alert_NeedBabyCribs : Alert
	{
		public Alert_NeedBabyCribs()
		{
			defaultLabel = "AlertNeedBabyCribs".Translate();
			defaultExplanation = "AlertNeedBabyCribsDesc".Translate();
			defaultPriority = AlertPriority.High;
		}

		public override AlertReport GetReport()
		{
			if (!ModsConfig.BiotechActive)
			{
				return false;
			}
			foreach (Map map in Find.Maps)
			{
				if (map.IsPlayerHome)
				{
					Alert_NeedColonistBeds.AvailableColonistBeds(map, includeBabies: true, out var _, out var _, out var cribs);
					if (cribs < 0)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
