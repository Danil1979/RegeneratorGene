using UnityEngine;
using Verse;

namespace RimWorld
{
	public class CompThrownFleckEmitter : ThingComp
	{
		public bool emittedBefore;

		public int ticksSinceLastEmitted;

		private CompProperties_ThrownFleckEmitter Props => (CompProperties_ThrownFleckEmitter)props;

		private Vector3 EmissionOffset => new Vector3(Rand.Range(Props.offsetMin.x, Props.offsetMax.x), Rand.Range(Props.offsetMin.y, Props.offsetMax.y), Rand.Range(Props.offsetMin.z, Props.offsetMax.z));

		private Color EmissionColor => Color.Lerp(Props.colorA, Props.colorB, Rand.Value);

		private bool IsOn
		{
			get
			{
				if (!parent.Spawned)
				{
					return false;
				}
				CompPowerTrader comp = parent.GetComp<CompPowerTrader>();
				if (comp != null && !comp.PowerOn)
				{
					return false;
				}
				CompSendSignalOnCountdown comp2 = parent.GetComp<CompSendSignalOnCountdown>();
				if (comp2 != null && comp2.ticksLeft <= 0)
				{
					return false;
				}
				Building_MusicalInstrument building_MusicalInstrument = parent as Building_MusicalInstrument;
				if (building_MusicalInstrument != null && !building_MusicalInstrument.IsBeingPlayed)
				{
					return false;
				}
				CompInitiatable comp3 = parent.GetComp<CompInitiatable>();
				if (comp3 != null && !comp3.Initiated)
				{
					return false;
				}
				CompLoudspeaker comp4 = parent.GetComp<CompLoudspeaker>();
				if (comp4 != null && !comp4.Active)
				{
					return false;
				}
				CompHackable comp5 = parent.GetComp<CompHackable>();
				if (comp5 != null && comp5.IsHacked)
				{
					return false;
				}
				Building_Crate building_Crate;
				if ((building_Crate = parent as Building_Crate) != null && !building_Crate.HasAnyContents)
				{
					return false;
				}
				return true;
			}
		}

		public override void CompTick()
		{
			if (!IsOn)
			{
				return;
			}
			if (Props.emissionInterval != -1)
			{
				if (ticksSinceLastEmitted >= Props.emissionInterval)
				{
					Emit();
					ticksSinceLastEmitted = 0;
				}
				else
				{
					ticksSinceLastEmitted++;
				}
			}
			else if (!emittedBefore)
			{
				Emit();
				emittedBefore = true;
			}
		}

		private void Emit()
		{
			for (int i = 0; i < Props.burstCount; i++)
			{
				FleckCreationData dataStatic = FleckMaker.GetDataStatic(parent.DrawPos + EmissionOffset, parent.Map, Props.fleck, Props.scale.RandomInRange);
				dataStatic.rotationRate = Props.rotationRate.RandomInRange;
				dataStatic.instanceColor = EmissionColor;
				dataStatic.velocityAngle = Props.velocityX.RandomInRange;
				dataStatic.velocitySpeed = Props.velocityY.RandomInRange;
				parent.Map.flecks.CreateFleck(dataStatic);
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Values.Look(ref ticksSinceLastEmitted, "ticksSinceLastEmitted", 0);
			Scribe_Values.Look(ref emittedBefore, "emittedBefore", defaultValue: false);
		}
	}
}
