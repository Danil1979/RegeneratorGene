using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorld.BaseGen
{
	public class SymbolResolver_Interior_WorshippedTerminal : SymbolResolver
	{
		public override void Resolve(ResolveParams rp)
		{
			IntVec3 topRight = rp.rect.TopRight;
			topRight.x -= rp.rect.Width / 2;
			topRight.z--;
			new List<IntVec3>
			{
				topRight + Rot4.East.FacingCell,
				topRight + Rot4.West.FacingCell
			};
			CellRect rect = rp.rect;
			rect.maxZ -= 3;
			ResolveParams resolveParams = rp;
			if (rp.sitePart != null)
			{
				resolveParams.singleThingToSpawn = rp.sitePart.things.FirstOrDefault((Thing t) => t.def == ThingDefOf.AncientTerminal_Worshipful);
			}
			if (resolveParams.singleThingToSpawn == null)
			{
				resolveParams.singleThingToSpawn = ThingMaker.MakeThing(ThingDefOf.AncientTerminal_Worshipful);
			}
			resolveParams.rect = CellRect.CenteredOn(topRight, 1, 1);
			resolveParams.thingRot = Rot4.South;
			BaseGen.symbolStack.Push("thing", resolveParams);
			if (rect.Height > 1)
			{
				ResolveParams resolveParams2 = rp;
				resolveParams2.rect = rect;
				resolveParams2.singleThingDef = ThingDefOf.SteleLarge;
				BaseGen.symbolStack.Push("thing", resolveParams2);
				if (Rand.Chance(0.5f))
				{
					BaseGen.symbolStack.Push("thing", resolveParams2);
				}
			}
		}
	}
}
