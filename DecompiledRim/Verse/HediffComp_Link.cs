using RimWorld;

namespace Verse
{
	public class HediffComp_Link : HediffComp
	{
		public Thing other;

		private MoteDualAttached mote;

		public bool drawConnection;

		public HediffCompProperties_Link Props => (HediffCompProperties_Link)props;

		public Pawn OtherPawn => (Pawn)other;

		public override bool CompShouldRemove
		{
			get
			{
				if (base.CompShouldRemove)
				{
					return true;
				}
				if (other == null || !parent.pawn.Spawned || !other.Spawned)
				{
					return true;
				}
				if (Props.maxDistance > 0f && !parent.pawn.Position.InHorDistOf(other.Position, Props.maxDistance))
				{
					return true;
				}
				if (Props.requireLinkOnOtherPawn)
				{
					Pawn pawn;
					if ((pawn = other as Pawn) == null)
					{
						Log.Error("HediffComp_Link requires link on other pawn, but other thing is not a pawn!");
					}
					else
					{
						foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
						{
							HediffWithComps hediffWithComps = hediff as HediffWithComps;
							if (hediffWithComps != null && hediffWithComps.comps.FirstOrDefault(delegate(HediffComp c)
							{
								HediffComp_Link hediffComp_Link = c as HediffComp_Link;
								return hediffComp_Link != null && hediffComp_Link.other == parent.pawn && hediffComp_Link.parent.def == parent.def;
							}) != null)
							{
								return false;
							}
						}
					}
					return true;
				}
				return false;
			}
		}

		public override string CompLabelInBracketsExtra
		{
			get
			{
				if (!Props.showName || other == null)
				{
					return null;
				}
				return other.LabelShort;
			}
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			base.CompPostTick(ref severityAdjustment);
			if (drawConnection)
			{
				ThingDef moteDef = Props.customMote ?? ThingDefOf.Mote_PsychicLinkLine;
				if (mote == null)
				{
					mote = MoteMaker.MakeInteractionOverlay(moteDef, parent.pawn, other);
				}
				mote.Maintain();
			}
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_References.Look(ref other, "other");
			Scribe_Values.Look(ref drawConnection, "drawConnection", defaultValue: false);
		}
	}
}
