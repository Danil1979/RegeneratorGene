using RimWorld.QuestGen;
using Verse;

namespace RimWorld
{
	public class QuestPart_SpawnSpaceDrone : QuestPart_SpawnThing
	{
		public override void Notify_QuestSignalReceived(Signal signal)
		{
			if (mapParent == null || !mapParent.HasMap)
			{
				mapParent = QuestGen_Get.GetMap().Parent;
				TryFindSpacedronePosition(mapParent.Map, out cell);
			}
			base.Notify_QuestSignalReceived(signal);
		}

		public static bool TryFindSpacedronePosition(Map map, out IntVec3 spot)
		{
			_ = ThingDefOf.Spacedrone.size;
			CellRect rect = GenAdj.OccupiedRect(IntVec3.Zero, ThingDefOf.Spacedrone.defaultPlacingRot, ThingDefOf.Spacedrone.size);
			IntVec3 interactionCellOffset = ThingDefOf.Spacedrone.interactionCellOffset;
			rect = rect.ExpandToFit(interactionCellOffset);
			if (DropCellFinder.FindSafeLandingSpot(out spot, null, map, 35, 15, 25, new IntVec2(rect.Width, rect.Height)))
			{
				return true;
			}
			return false;
		}
	}
}
