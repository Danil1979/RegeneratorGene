using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorld
{
	public static class ContainingSelectionUtility
	{
		public static Gizmo SelectCarriedThingGizmo(Thing container, Thing carriedThing)
		{
			if (container == null || carriedThing == null)
			{
				return null;
			}
			if (!CanSelect(carriedThing, container))
			{
				return null;
			}
			string label = GizmoLabel(container, carriedThing);
			string description = GizmoDescription(container, carriedThing);
			return CreateSelectGizmo(label, description, carriedThing);
		}

		public static Gizmo SelectContainingThingGizmo(Thing containedThing)
		{
			Pawn_CarryTracker pawn_CarryTracker;
			if ((pawn_CarryTracker = containedThing.ParentHolder as Pawn_CarryTracker) != null)
			{
				return CreateSelectGizmo("CommandSelectCarryingPawn", "CommandSelectCarryingPawnDesc", pawn_CarryTracker.pawn);
			}
			Thing thingToSelect;
			if ((thingToSelect = containedThing.ParentHolder as Thing) != null)
			{
				return CreateSelectGizmo("CommandSelectContainerThing", "CommandSelectContainerThingDesc", thingToSelect);
			}
			ThingComp thingComp;
			if ((thingComp = containedThing.ParentHolder as ThingComp) != null)
			{
				return CreateSelectGizmo("CommandSelectContainerThing", "CommandSelectContainerThingDesc", thingComp.parent);
			}
			if (containedThing.Spawned && containedThing.def.category == ThingCategory.Item)
			{
				List<Thing> thingList = containedThing.Position.GetThingList(containedThing.Map);
				for (int i = 0; i < thingList.Count; i++)
				{
					Building_Storage building_Storage;
					if ((building_Storage = thingList[i] as Building_Storage) != null)
					{
						return CreateSelectStorageGizmo("CommandSelectContainerThing".Translate(building_Storage), "CommandSelectContainerThingDesc".Translate(), building_Storage, building_Storage);
					}
				}
			}
			return null;
		}

		public static IEnumerable<Thing> SelectableContainedThings(Thing container)
		{
			Pawn pawn;
			if ((pawn = container as Pawn) != null)
			{
				Thing thing;
				if ((thing = pawn.carryTracker?.CarriedThing) != null && CanSelect(thing, container))
				{
					yield return thing;
				}
			}
			else
			{
				if (container is MinifiedThing || !container.Faction.IsPlayerSafe())
				{
					yield break;
				}
				IThingHolder thingHolder;
				if ((thingHolder = container as IThingHolder) != null)
				{
					foreach (Thing item in (IEnumerable<Thing>)thingHolder.GetDirectlyHeldThings())
					{
						if (CanSelect(item, container))
						{
							yield return item;
						}
					}
				}
				else
				{
					ThingWithComps thingWithComps;
					if ((thingWithComps = container as ThingWithComps) == null)
					{
						yield break;
					}
					foreach (ThingComp allComp in thingWithComps.AllComps)
					{
						IThingHolder thingHolder2;
						if ((thingHolder2 = allComp as IThingHolder) == null)
						{
							continue;
						}
						foreach (Thing item2 in (IEnumerable<Thing>)thingHolder2.GetDirectlyHeldThings())
						{
							if (CanSelect(item2, container))
							{
								yield return item2;
							}
						}
					}
				}
			}
		}

		private static bool CanSelect(Thing carriedThing, Thing container)
		{
			if (!carriedThing.def.selectable)
			{
				return false;
			}
			if (carriedThing is Pawn)
			{
				return container.def.containedPawnsSelectable;
			}
			return container.def.containedItemsSelectable;
		}

		private static Gizmo CreateSelectGizmo(string label, string description, Thing thingToSelect, Thing iconThing = null)
		{
			if (!thingToSelect.def.selectable)
			{
				return null;
			}
			Texture2D icon = ((thingToSelect is Pawn) ? TexCommand.SelectCarriedPawn : TexCommand.SelectCarriedThing);
			float scale = 1f;
			float angle = 0f;
			Vector2 iconProportions = Vector2.one;
			Color color = Color.white;
			if (iconThing != null)
			{
				icon = (Texture2D)Widgets.GetIconFor(iconThing, new Vector2(75f, 75f), iconThing.def.defaultPlacingRot, stackOfOne: true, out scale, out angle, out iconProportions, out color);
			}
			return new Command_Action
			{
				defaultLabel = label.Translate(thingToSelect),
				defaultDesc = description.Translate(),
				icon = icon,
				iconDrawScale = scale,
				iconAngle = angle,
				defaultIconColor = color,
				iconProportions = iconProportions,
				action = delegate
				{
					Find.Selector.ClearSelection();
					Find.Selector.Select(thingToSelect);
				}
			};
		}

		public static Gizmo CreateSelectStorageGizmo(string label, string description, Thing thingToSelect, Thing iconThing = null, bool groupable = true)
		{
			if (!thingToSelect.def.selectable)
			{
				return null;
			}
			float scale = 1f;
			float angle = 0f;
			Vector2 iconProportions = Vector2.one;
			Color color = Color.white;
			Texture iconFor = Widgets.GetIconFor(iconThing, new Vector2(75f, 75f), iconThing.def.defaultPlacingRot, stackOfOne: false, out scale, out angle, out iconProportions, out color);
			return new Command_SelectStorage
			{
				defaultLabel = label,
				defaultDesc = description,
				icon = iconFor,
				iconDrawScale = scale * 0.85f,
				iconAngle = angle,
				defaultIconColor = color,
				iconProportions = iconProportions,
				groupable = groupable,
				groupKey = thingToSelect.thingIDNumber,
				action = delegate
				{
					Find.Selector.ClearSelection();
					Find.Selector.Select(thingToSelect);
				}
			};
		}

		private static string GizmoLabel(Thing container, Thing carriedThing)
		{
			if (container is Pawn)
			{
				if (carriedThing is Pawn)
				{
					return "CommandSelectCarriedPawn";
				}
				return "CommandSelectCarriedThing";
			}
			if (carriedThing is Pawn)
			{
				return "CommandSelectContainedPawn";
			}
			return "CommandSelectContainedThing";
		}

		private static string GizmoDescription(Thing container, Thing carriedThing)
		{
			if (container is Pawn)
			{
				if (carriedThing is Pawn)
				{
					return "CommandSelectCarriedPawnDesc";
				}
				return "CommandSelectCarriedThingDesc";
			}
			if (carriedThing is Pawn)
			{
				return "CommandSelectContainedPawnDesc";
			}
			return "CommandSelectContainedThingDesc";
		}
	}
}
