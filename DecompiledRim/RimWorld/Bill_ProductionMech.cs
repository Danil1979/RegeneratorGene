using System.Linq;
using System.Text;
using Verse;

namespace RimWorld
{
	public class Bill_ProductionMech : Bill_Mech
	{
		public override float BandwidthCost => recipe.ProducedThingDef.GetStatValueAbstract(StatDefOf.BandwidthCost);

		public Bill_ProductionMech()
		{
		}

		public Bill_ProductionMech(RecipeDef recipe, Precept_ThingStyle precept = null)
			: base(recipe, precept)
		{
		}

		public override Pawn ProducePawn()
		{
			Pawn pawn = base.BoundPawn;
			PawnKindDef kind = DefDatabase<PawnKindDef>.AllDefs.Where((PawnKindDef pk) => pk.race == recipe.ProducedThingDef).First();
			Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, pawn.Faction, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: false, allowDead: false, allowDowned: true, canGeneratePawnRelations: true, mustBeCapableOfViolence: false, 1f, forceAddFreeWarmLayerIfNeeded: false, allowGay: true, allowPregnant: false, allowFood: true, allowAddictions: true, inhabitant: false, certainlyBeenInCryptosleep: false, forceRedressWorldPawnIfFormerColonist: false, worldPawnFactionDoesntMatter: false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, forceNoIdeo: false, forceNoBackstory: false, forbidAnyTitle: false, forceDead: false, null, null, null, null, null, 0f, DevelopmentalStage.Newborn));
			pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, pawn2);
			return pawn2;
		}

		protected override void AppendFormingInspectionData(StringBuilder sb)
		{
			switch (base.State)
			{
			case FormingCycleState.Gathering:
				AppendCurrentIngredientCount(sb);
				break;
			case FormingCycleState.Preparing:
			case FormingCycleState.Forming:
				sb.AppendLine("GestatingMech".Translate(recipe.ProducedThingDef.LabelCap));
				break;
			case FormingCycleState.Formed:
				if (base.BoundPawn != null)
				{
					sb.AppendLine("GestatedMech".Translate(recipe.ProducedThingDef.LabelCap, base.BoundPawn.Named("PAWN")) + " (" + "GestatedMechRequiresMechanitor".Translate(base.BoundPawn.Named("PAWN")) + ")");
				}
				break;
			}
		}
	}
}
