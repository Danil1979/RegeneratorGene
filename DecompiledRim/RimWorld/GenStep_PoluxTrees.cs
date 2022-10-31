using UnityEngine;
using Verse;

namespace RimWorld
{
	public class GenStep_PoluxTrees : GenStep_SpecialTrees
	{
		public int pollutionNone;

		public int pollutionLight;

		public int pollutionModerate = 1;

		public int pollutionExtreme = 3;

		private const float Growth = 1f;

		public override int SeedPart => 537455645;

		public override int DesiredTreeCountForMap(Map map)
		{
			PollutionLevel pollutionLevel = Find.WorldGrid[map.Tile].PollutionLevel();
			switch (pollutionLevel)
			{
			case PollutionLevel.None:
				return pollutionNone;
			case PollutionLevel.Light:
				return pollutionLight;
			case PollutionLevel.Moderate:
				return pollutionModerate;
			case PollutionLevel.Extreme:
				return pollutionExtreme;
			default:
				Debug.LogError("Unrecognized pollution level '" + pollutionLevel.ToString() + "'.");
				return 0;
			}
		}

		protected override float GetGrowth()
		{
			return 1f;
		}
	}
}
