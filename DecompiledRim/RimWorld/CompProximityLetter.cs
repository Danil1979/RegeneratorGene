using System.Collections.Generic;
using Verse;

namespace RimWorld
{
	public class CompProximityLetter : ThingComp
	{
		private bool letterSent;

		public CompProperties_ProximityLetter Props => (CompProperties_ProximityLetter)props;

		private void SendLetter(Pawn triggerer)
		{
			Find.LetterStack.ReceiveLetter(Props.letterLabel.Formatted(triggerer.Named("PAWN")), Props.letterText.Formatted(triggerer.Named("PAWN")), Props.letterDef, parent);
			letterSent = true;
		}

		public override void CompTick()
		{
			base.CompTick();
			if (letterSent || !parent.IsHashIntervalTick(60))
			{
				return;
			}
			Map map = parent.Map;
			int num = GenRadial.NumCellsInRadius(Props.radius);
			for (int i = 0; i < num; i++)
			{
				IntVec3 c = parent.Position + GenRadial.RadialPattern[i];
				if (!c.InBounds(map))
				{
					continue;
				}
				List<Thing> thingList = c.GetThingList(map);
				for (int j = 0; j < thingList.Count; j++)
				{
					Pawn pawn;
					if ((pawn = thingList[j] as Pawn) != null && pawn.IsColonistPlayerControlled)
					{
						SendLetter(pawn);
						return;
					}
				}
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref letterSent, "letterSent", defaultValue: false);
		}
	}
}
