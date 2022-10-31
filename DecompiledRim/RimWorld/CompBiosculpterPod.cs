using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace RimWorld
{
	[StaticConstructorOnStartup]
	public class CompBiosculpterPod : ThingComp, IThingHolder, ISuspendableThingHolder, IThingHolderWithDrawnPawn, IStoreSettingsParent
	{
		private const int NoPowerEjectCumulativeTicks = 60000;

		private const int BiotunedDuration = 4800000;

		private const float NutritionRequired = 5f;

		private const float CacheForSecs = 2f;

		private static readonly Texture2D InterruptCycleIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

		private static readonly Material BackgroundMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.082f, 0.078f, 0.063f));

		private const float BackgroundRect_YOff = 0.08108108f;

		private const float Pawn_YOff = 3f / 74f;

		private string currentCycleKey;

		private float currentCycleTicksRemaining;

		private int currentCyclePowerCutTicks;

		private ThingOwner innerContainer;

		private Pawn biotunedTo;

		private int biotunedCountdownTicks;

		private StorageSettings allowedNutritionSettings;

		private float liquifiedNutrition;

		public bool autoLoadNutrition = true;

		public bool devFillPodLatch;

		private bool autoAgeReversal;

		public Job queuedEnterJob;

		public Pawn queuedPawn;

		private List<ThingCount> chosenExtraItems = new List<ThingCount>();

		private List<FloatMenuOption> cycleEligiblePawnOptions = new List<FloatMenuOption>();

		private Pawn pawnEnteringBiosculpter;

		private Dictionary<CompBiosculpterPod_Cycle, List<IngredientCount>> cachedExtraIngredients = new Dictionary<CompBiosculpterPod_Cycle, List<IngredientCount>>();

		private Dictionary<CompBiosculpterPod_Cycle, CacheAnyPawnEligibleCycle> cachedAnyPawnEligible = new Dictionary<CompBiosculpterPod_Cycle, CacheAnyPawnEligibleCycle>();

		private static Dictionary<Pawn, List<CompBiosculpterPod>> cachedBiotunedPods = new Dictionary<Pawn, List<CompBiosculpterPod>>();

		private Pawn cacheReachIngredientsPawn;

		private CompBiosculpterPod_Cycle cacheReachIngredientsCycle;

		private float cacheReachIngredientsTime = float.MinValue;

		private bool cacheReachIngredientsResult;

		private Effecter progressBarEffecter;

		private Effecter operatingEffecter;

		private Effecter readyEffecter;

		private Texture2D cachedAutoAgeReverseIcon;

		private List<CompBiosculpterPod_Cycle> cachedAvailableCycles;

		private Dictionary<string, CompBiosculpterPod_Cycle> cycleLookup;

		private static string cachedAgeReversalCycleKey = null;

		private List<string> tmpIngredientsStrings = new List<string>();

		private CompPowerTrader powerTraderComp;

		private CompPower powerComp;

		public CompProperties_BiosculpterPod Props => props as CompProperties_BiosculpterPod;

		public bool IsContentsSuspended => true;

		public float RequiredNutritionRemaining => Mathf.Max(5f - liquifiedNutrition, 0f);

		public bool NutritionLoaded => RequiredNutritionRemaining <= 0f;

		public bool AutoAgeReversal => autoAgeReversal;

		private Texture2D AutoAgeReversalIcon
		{
			get
			{
				if (cachedAutoAgeReverseIcon == null)
				{
					cachedAutoAgeReverseIcon = ContentFinder<Texture2D>.Get("UI/Gizmos/BiosculpterAutoAgeReversal");
				}
				return cachedAutoAgeReverseIcon;
			}
		}

		public BiosculpterPodState State
		{
			get
			{
				if (Occupant != null)
				{
					return BiosculpterPodState.Occupied;
				}
				if (NutritionLoaded)
				{
					return BiosculpterPodState.SelectingCycle;
				}
				return BiosculpterPodState.LoadingNutrition;
			}
		}

		public Pawn Occupant
		{
			get
			{
				if (pawnEnteringBiosculpter != null)
				{
					return pawnEnteringBiosculpter;
				}
				if (currentCycleKey == null)
				{
					return null;
				}
				if (innerContainer.Count != 1)
				{
					return null;
				}
				return innerContainer[0] as Pawn;
			}
		}

		public CompBiosculpterPod_Cycle CurrentCycle
		{
			get
			{
				if (currentCycleKey == null)
				{
					return null;
				}
				foreach (CompBiosculpterPod_Cycle availableCycle in AvailableCycles)
				{
					if (availableCycle.Props.key == currentCycleKey)
					{
						return availableCycle;
					}
				}
				return null;
			}
		}

		public List<CompBiosculpterPod_Cycle> AvailableCycles
		{
			get
			{
				if (cachedAvailableCycles == null)
				{
					SetupCycleCaches();
				}
				return cachedAvailableCycles;
			}
		}

		public string AgeReversalCycleKey
		{
			get
			{
				if (cachedAgeReversalCycleKey == null)
				{
					SetupCycleCaches();
				}
				return cachedAgeReversalCycleKey;
			}
		}

		private float CycleSpeedFactorNoPawn => CleanlinessSpeedFactor * BiotunedSpeedFactor;

		public float CycleSpeedFactor
		{
			get
			{
				if (Occupant == null)
				{
					return Mathf.Max(0.1f, CycleSpeedFactorNoPawn);
				}
				return GetCycleSpeedFactorForPawn(Occupant);
			}
		}

		private float CleanlinessSpeedFactor => parent.GetStatValue(StatDefOf.BiosculpterPodSpeedFactor);

		private float BiotunedSpeedFactor
		{
			get
			{
				if (biotunedTo == null)
				{
					return 1f;
				}
				return Props.biotunedCycleSpeedFactor;
			}
		}

		public bool PowerOn => parent.TryGetComp<CompPowerTrader>().PowerOn;

		public float HeldPawnDrawPos_Y => parent.DrawPos.y - 3f / 74f;

		public float HeldPawnBodyAngle => parent.Rotation.Opposite.AsAngle;

		public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

		public bool StorageTabVisible => true;

		public CompBiosculpterPod()
		{
			innerContainer = new ThingOwner<Thing>(this);
		}

		public override void Initialize(CompProperties props)
		{
			base.Initialize(props);
			allowedNutritionSettings = new StorageSettings(this);
			if (parent.def.building.defaultStorageSettings != null)
			{
				allowedNutritionSettings.CopyFrom(parent.def.building.defaultStorageSettings);
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			if (ModLister.CheckIdeology("Biosculpter pod comp"))
			{
				base.PostSpawnSetup(respawningAfterLoad);
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			Scribe_Values.Look(ref currentCycleKey, "currentCycleKey");
			Scribe_Values.Look(ref currentCycleTicksRemaining, "currentCycleTicksRemaining", 0f);
			Scribe_Values.Look(ref currentCyclePowerCutTicks, "currentCyclePowerCutTicks", 0);
			Scribe_References.Look(ref biotunedTo, "biotunedTo");
			Scribe_Values.Look(ref biotunedCountdownTicks, "biotunedCountdownTicks", 0);
			Scribe_Deep.Look(ref allowedNutritionSettings, "allowedNutritionSettings");
			Scribe_Values.Look(ref liquifiedNutrition, "liquifiedNutrition", 0f);
			Scribe_Values.Look(ref autoLoadNutrition, "autoLoadNutrition", defaultValue: false);
			Scribe_Values.Look(ref devFillPodLatch, "devFillPodLatch", defaultValue: false);
			Scribe_Values.Look(ref autoAgeReversal, "autoAgeReversal", defaultValue: false);
			Scribe_References.Look(ref queuedEnterJob, "queuedEnterJob");
			Scribe_References.Look(ref queuedPawn, "queuedPawn");
			if (allowedNutritionSettings == null)
			{
				allowedNutritionSettings = new StorageSettings(this);
				if (parent.def.building.defaultStorageSettings != null)
				{
					allowedNutritionSettings.CopyFrom(parent.def.building.defaultStorageSettings);
				}
			}
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (currentCycleKey == "healing")
				{
					currentCycleKey = "medic";
				}
				if (biotunedTo != null)
				{
					SetBiotuned(biotunedTo);
				}
				LiquifyNutrition();
			}
		}

		public CompBiosculpterPod_Cycle GetCycle(string key)
		{
			if (cycleLookup == null)
			{
				SetupCycleCaches();
			}
			return cycleLookup[key];
		}

		public float GetCycleSpeedFactorForPawn(Pawn p)
		{
			return Mathf.Max(0.1f, CycleSpeedFactorNoPawn * p.GetStatValue(StatDefOf.BiosculpterOccupantSpeed));
		}

		private void SetupCycleCaches()
		{
			cachedAvailableCycles = new List<CompBiosculpterPod_Cycle>();
			cachedAvailableCycles.AddRange(parent.AllComps.OfType<CompBiosculpterPod_Cycle>());
			cycleLookup = new Dictionary<string, CompBiosculpterPod_Cycle>();
			foreach (CompBiosculpterPod_Cycle cachedAvailableCycle in cachedAvailableCycles)
			{
				CompBiosculpterPod_AgeReversalCycle compBiosculpterPod_AgeReversalCycle;
				if ((compBiosculpterPod_AgeReversalCycle = cachedAvailableCycle as CompBiosculpterPod_AgeReversalCycle) != null)
				{
					cachedAgeReversalCycleKey = compBiosculpterPod_AgeReversalCycle.Props.key;
				}
				cycleLookup[cachedAvailableCycle.Props.key] = cachedAvailableCycle;
			}
		}

		public void SetBiotuned(Pawn newBiotunedTo)
		{
			if (newBiotunedTo != biotunedTo)
			{
				autoAgeReversal = false;
			}
			if (biotunedTo != null && cachedBiotunedPods.ContainsKey(biotunedTo))
			{
				cachedBiotunedPods[biotunedTo].Remove(this);
			}
			if (newBiotunedTo != null && !cachedBiotunedPods.ContainsKey(newBiotunedTo))
			{
				cachedBiotunedPods[newBiotunedTo] = new List<CompBiosculpterPod>();
			}
			if (newBiotunedTo != null && !cachedBiotunedPods[newBiotunedTo].Contains(this))
			{
				cachedBiotunedPods[newBiotunedTo].Add(this);
			}
			if (newBiotunedTo != null && newBiotunedTo != biotunedTo)
			{
				biotunedCountdownTicks = 4800000;
			}
			biotunedTo = newBiotunedTo;
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			SetBiotuned(null);
			if (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize)
			{
				EjectContents(interrupted: true, playSounds: false, previousMap);
			}
			innerContainer.ClearAndDestroyContents();
			base.PostDestroy(mode, previousMap);
		}

		public override void PostDeSpawn(Map map)
		{
			EjectContents(interrupted: true, playSounds: false, map);
			progressBarEffecter?.Cleanup();
			progressBarEffecter = null;
			operatingEffecter?.Cleanup();
			operatingEffecter = null;
			readyEffecter?.Cleanup();
			readyEffecter = null;
			currentCycleKey = null;
		}

		public override void DrawGUIOverlay()
		{
			base.DrawGUIOverlay();
			if (biotunedTo != null || Occupant != null)
			{
				GenMapUI.DrawThingLabel(parent, biotunedTo?.LabelShort ?? Occupant.LabelShort, GenMapUI.DefaultThingLabelColor);
			}
		}

		public override string CompInspectStringExtra()
		{
			StringBuilder stringBuilder = new StringBuilder();
			BiosculpterPodState state = State;
			if (parent.Spawned)
			{
				CompBiosculpterPod_Cycle currentCycle = CurrentCycle;
				if (currentCycle != null)
				{
					stringBuilder.AppendLineIfNotEmpty().Append("BiosculpterPodCycleLabel".Translate()).Append(": ")
						.Append(currentCycle.Props.LabelCap);
					if (biotunedTo == null)
					{
						stringBuilder.Append(" " + "BiosculpterPodCycleWillBiotune".Translate());
					}
				}
				else if (state == BiosculpterPodState.SelectingCycle)
				{
					if (PowerOn)
					{
						if (queuedEnterJob != null && !queuedEnterJob.biosculpterCycleKey.NullOrEmpty())
						{
							stringBuilder.Append("BiosculpterPodCycleStandby".Translate(GetCycle(queuedEnterJob.biosculpterCycleKey).Props.label.Named("CYCLE"), queuedPawn.Named("PAWN")));
						}
						else
						{
							stringBuilder.Append("BiosculpterPodCycleSelection".Translate().CapitalizeFirst());
						}
					}
					else
					{
						stringBuilder.Append("BiosculpterPodCycleSelectionNoPower".Translate().CapitalizeFirst());
					}
				}
				if (state == BiosculpterPodState.LoadingNutrition)
				{
					stringBuilder.Append("BiosculpterPodCycleLabelLoading".Translate().CapitalizeFirst());
					stringBuilder.AppendLineIfNotEmpty().Append("Nutrition".Translate()).Append(": ")
						.Append(liquifiedNutrition.ToStringByStyle(ToStringStyle.FloatMaxOne))
						.Append(" / ")
						.Append(5f);
				}
				if (state == BiosculpterPodState.Occupied)
				{
					float num = currentCycleTicksRemaining / CycleSpeedFactor;
					stringBuilder.AppendLineIfNotEmpty().Append("Contains".Translate()).Append(": ")
						.Append(Occupant.NameShortColored.Resolve());
					if (!PowerOn)
					{
						stringBuilder.AppendLine().Append("BiosculpterCycleNoPowerInterrupt".Translate((60000 - currentCyclePowerCutTicks).ToStringTicksToPeriod().Named("TIME")).Colorize(ColorLibrary.RedReadable));
					}
					stringBuilder.AppendLine().Append("BiosculpterCycleTimeRemaining".Translate()).Append(": ")
						.Append(((int)num).ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor));
					if (Occupant.Ideo?.HasPrecept(PreceptDefOf.Biosculpting_Accelerated) ?? false)
					{
						stringBuilder.Append(" (" + "BiosculpterCycleAccelerated".Translate() + ")");
					}
					if (biotunedTo != null)
					{
						stringBuilder.AppendLine().Append("BiosculpterBiotunedSpeedFactor".Translate()).Append(": ")
							.Append(BiotunedSpeedFactor.ToStringPercent());
					}
					stringBuilder.AppendLine().Append("BiosculpterCleanlinessSpeedFactor".Translate()).Append(": ")
						.Append(CleanlinessSpeedFactor.ToStringPercent());
				}
			}
			if (biotunedTo != null && state != BiosculpterPodState.Occupied)
			{
				stringBuilder.AppendLineIfNotEmpty().Append("BiosculpterBiotunedTo".Translate()).Append(": ")
					.Append(biotunedTo.LabelShort)
					.Append(" (")
					.Append(biotunedCountdownTicks.ToStringTicksToPeriod())
					.Append(")");
			}
			if (stringBuilder.Length <= 0)
			{
				return null;
			}
			return stringBuilder.ToString();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			BiosculpterPodState state = State;
			string cycleIndependentCannotUseReason = CannotUseNowReason();
			foreach (CompBiosculpterPod_Cycle cycle in AvailableCycles)
			{
				string text = cycleIndependentCannotUseReason ?? CannotUseNowCycleReason(cycle);
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "BiosculpterPodCycleCommand".Translate(cycle.Props.label) + ((biotunedTo != null) ? (" (" + biotunedTo.LabelShort + ")") : "");
				command_Action.defaultDesc = CycleDescription(cycle);
				command_Action.icon = cycle.Props.Icon;
				command_Action.action = delegate
				{
					SelectPawnsForCycleOptions(cycle, out var options2);
					if (biotunedTo != null && options2.Count > 0)
					{
						options2[0].action();
						if (!(cycle is CompBiosculpterPod_HealingCycle))
						{
							Messages.Message("BiosculpterEnteringMessage".Translate(biotunedTo.Named("PAWN"), cycle.Props.label.Named("CYCLE")).CapitalizeFirst(), parent, MessageTypeDefOf.SilentInput, historical: false);
						}
					}
					else
					{
						Find.WindowStack.Add(new FloatMenu(options2));
					}
				};
				command_Action.activateSound = SoundDefOf.Tick_Tiny;
				command_Action.disabled = text != null;
				List<FloatMenuOption> options;
				if (text != null)
				{
					command_Action.Disable(text);
				}
				else if (!SelectPawnsForCycleOptions(cycle, out options, shortCircuit: true))
				{
					command_Action.Disable((biotunedTo != null) ? "BiosculpterNoEligiblePawnsBiotuned".Translate(biotunedTo.Named("PAWN")) : "BiosculpterNoEligiblePawns".Translate());
				}
				yield return command_Action;
			}
			if (state == BiosculpterPodState.Occupied)
			{
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "BiosculpterInteruptCycle".Translate();
				command_Action2.defaultDesc = "BiosculpterInteruptCycleDesc".Translate();
				command_Action2.icon = InterruptCycleIcon;
				command_Action2.action = delegate
				{
					EjectContents(interrupted: true, playSounds: true);
				};
				command_Action2.activateSound = SoundDefOf.Designate_Cancel;
				yield return command_Action2;
			}
			Command_Toggle command_Toggle = new Command_Toggle();
			command_Toggle.defaultLabel = "BiosculpterAutoLoadNutritionLabel".Translate();
			command_Toggle.defaultDesc = "BiosculpterAutoLoadNutritionDescription".Translate();
			command_Toggle.icon = (autoLoadNutrition ? TexCommand.ForbidOff : TexCommand.ForbidOn);
			command_Toggle.isActive = () => autoLoadNutrition;
			command_Toggle.toggleAction = delegate
			{
				autoLoadNutrition = !autoLoadNutrition;
			};
			yield return command_Toggle;
			if (biotunedTo != null && biotunedTo.Ideo.HasPrecept(PreceptDefOf.AgeReversal_Demanded))
			{
				Command_Toggle command_Toggle2 = new Command_Toggle();
				command_Toggle2.defaultLabel = "BiosculpterAutoAgeReversalLabel".Translate(biotunedTo.Named("PAWN"));
				TaggedString taggedString = ((biotunedTo.ageTracker.AgeReversalDemandedDeadlineTicks > 0) ? "BiosculpterAutoAgeReversalDescriptionFuture".Translate(biotunedTo.Named("PAWN"), ((int)biotunedTo.ageTracker.AgeReversalDemandedDeadlineTicks).ToStringTicksToPeriodVague().Named("TIME")) : "BiosculpterAutoAgeReversalDescriptionNow".Translate(biotunedTo.Named("PAWN")));
				command_Toggle2.defaultDesc = "BiosculpterAutoAgeReversalDescription".Translate(biotunedTo.Named("PAWN"), taggedString.Named("NEXTTREATMENT"));
				command_Toggle2.icon = AutoAgeReversalIcon;
				command_Toggle2.isActive = () => AutoAgeReversal;
				command_Toggle2.toggleAction = delegate
				{
					autoAgeReversal = !autoAgeReversal;
				};
				if (!CanAgeReverse(biotunedTo))
				{
					command_Toggle2.Disable("UnderMinBiosculpterAgeReversalAge".Translate(biotunedTo.ageTracker.AdultMinAge.Named("ADULTAGE")).CapitalizeFirst());
					autoAgeReversal = false;
				}
				yield return command_Toggle2;
			}
			foreach (Gizmo item in StorageSettingsClipboard.CopyPasteGizmosFor(allowedNutritionSettings))
			{
				yield return item;
			}
			Gizmo gizmo;
			if ((gizmo = Building.SelectContainedItemGizmo(parent, Occupant)) != null)
			{
				yield return gizmo;
			}
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "DEV: complete cycle",
					action = delegate
					{
						currentCycleTicksRemaining = 10f;
					},
					disabled = (State != BiosculpterPodState.Occupied)
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: advance cycle +1 day",
					action = delegate
					{
						currentCycleTicksRemaining -= 60000f;
					},
					disabled = (State != BiosculpterPodState.Occupied)
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: complete biotune timer",
					action = delegate
					{
						biotunedCountdownTicks = 10;
					},
					disabled = (biotunedCountdownTicks <= 0)
				};
				yield return new Command_Action
				{
					defaultLabel = "DEV: fill nutrition and cycle ingredients",
					action = delegate
					{
						liquifiedNutrition = 5f;
						devFillPodLatch = true;
					},
					disabled = (State == BiosculpterPodState.Occupied || (devFillPodLatch && liquifiedNutrition == 5f))
				};
			}
		}

		private string IngredientsDescription(CompBiosculpterPod_Cycle cycle)
		{
			tmpIngredientsStrings.Clear();
			if (!cycle.Props.extraRequiredIngredients.NullOrEmpty() && !devFillPodLatch)
			{
				for (int i = 0; i < cycle.Props.extraRequiredIngredients.Count; i++)
				{
					tmpIngredientsStrings.Add(cycle.Props.extraRequiredIngredients[i].Summary);
				}
			}
			return tmpIngredientsStrings.ToCommaList(useAnd: true);
		}

		private string CycleDescription(CompBiosculpterPod_Cycle cycle)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(cycle.Description(biotunedTo));
			float num = cycle.Props.durationDays / CycleSpeedFactor;
			float num2 = num / PreceptDefOf.Biosculpting_Accelerated.biosculpterPodCycleSpeedFactor;
			stringBuilder.AppendLine("\n\n" + "BiosculpterPodCycleDuration".Translate() + ": " + ((int)(num * 60000f)).ToStringTicksToDays());
			if (!Find.IdeoManager.classicMode)
			{
				stringBuilder.Append("BiosculpterPodCycleDurationTranshumanists".Translate() + ": " + ((int)(num2 * 60000f)).ToStringTicksToDays());
			}
			return stringBuilder.ToString();
		}

		public bool PawnCanUseNow(Pawn pawn, CompBiosculpterPod_Cycle cycle)
		{
			return (CannotUseNowReason() ?? CannotUseNowPawnReason(pawn) ?? CannotUseNowCycleReason(cycle) ?? CannotUseNowPawnCycleReason(pawn, cycle)) == null;
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			if (selPawn.IsQuestLodger())
			{
				yield return new FloatMenuOption("CannotEnter".Translate() + ": " + "CryptosleepCasketGuestsNotAllowed".Translate().CapitalizeFirst(), null);
				yield break;
			}
			string cycleIndependentfailureReason = CannotUseNowReason() ?? CannotUseNowPawnReason(selPawn);
			foreach (CompBiosculpterPod_Cycle cycle in AvailableCycles)
			{
				string text = cycleIndependentfailureReason ?? CannotUseNowCycleReason(cycle) ?? CannotUseNowPawnCycleReason(selPawn, cycle);
				if (text != null)
				{
					yield return new FloatMenuOption(CannotStartText(cycle, text), null);
					continue;
				}
				string label = "EnterBiosculpterPod".Translate(cycle.Props.label, ((int)(cycle.Props.durationDays / GetCycleSpeedFactorForPawn(selPawn) * 60000f)).ToStringTicksToDays());
				Action action = delegate
				{
					PrepareCycleJob(selPawn, selPawn, cycle, EnterBiosculpterJob());
				};
				yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), selPawn, parent);
			}
		}

		public static bool CanAgeReverse(Pawn biosculptee)
		{
			return biosculptee.ageTracker.Adult;
		}

		public static List<CompBiosculpterPod> BiotunedPods(Pawn pawn)
		{
			return cachedBiotunedPods.TryGetValue(pawn);
		}

		public static bool HasBiotunedAutoAgeReversePod(Pawn pawn)
		{
			List<CompBiosculpterPod> list = cachedBiotunedPods.TryGetValue(pawn);
			if (list == null)
			{
				return false;
			}
			foreach (CompBiosculpterPod item in list)
			{
				if (item.AutoAgeReversal)
				{
					return true;
				}
			}
			return false;
		}

		private static string CannotStartText(CompBiosculpterPod_Cycle cycle, string translatedReason)
		{
			return "BiosculpterCannotStartCycle".Translate(cycle.Props.label) + ": " + translatedReason.CapitalizeFirst();
		}

		public string CannotUseNowCycleReason(CompBiosculpterPod_Cycle cycle)
		{
			List<string> list = cycle.MissingResearchLabels();
			if (list.Any())
			{
				return "MissingRequiredResearch".Translate() + " " + list.ToCommaList();
			}
			return null;
		}

		public string CannotUseNowPawnCycleReason(Pawn p, CompBiosculpterPod_Cycle cycle, bool checkIngredients = true)
		{
			return CannotUseNowPawnCycleReason(p, p, cycle, checkIngredients);
		}

		private bool CanReachOrHasIngredients(Pawn hauler, Pawn biosculptee, CompBiosculpterPod_Cycle cycle, bool useCache = false)
		{
			if (!PawnCarryingExtraCycleIngredients(biosculptee, cycle) && (biosculptee == hauler || !PawnCarryingExtraCycleIngredients(hauler, cycle)))
			{
				return CanReachRequiredIngredients(hauler, cycle, useCache);
			}
			return true;
		}

		public string CannotUseNowPawnCycleReason(Pawn hauler, Pawn biosculptee, CompBiosculpterPod_Cycle cycle, bool checkIngredients = true)
		{
			if (AgeReversalCycleKey != null && cycle.Props.key == AgeReversalCycleKey && !CanAgeReverse(biosculptee))
			{
				return "UnderMinBiosculpterAgeReversalAge".Translate(biosculptee.ageTracker.AdultMinAge.Named("ADULTAGE")).CapitalizeFirst();
			}
			if (checkIngredients && !CanReachOrHasIngredients(hauler, biosculptee, cycle, useCache: true))
			{
				return "BiosculpterMissingIngredients".Translate(IngredientsDescription(cycle).Named("INGREDIENTS")).CapitalizeFirst();
			}
			return null;
		}

		public string CannotUseNowPawnReason(Pawn p)
		{
			if (biotunedTo != null && biotunedTo != p)
			{
				return "BiosculpterBiotunedToAnother".Translate().CapitalizeFirst();
			}
			if (!p.CanReach(parent, PathEndMode.InteractionCell, Danger.Deadly))
			{
				return "NoPath".Translate().CapitalizeFirst();
			}
			return null;
		}

		public string CannotUseNowReason()
		{
			if (!PowerOn)
			{
				return "NoPower".Translate().CapitalizeFirst();
			}
			if (State == BiosculpterPodState.LoadingNutrition)
			{
				return "BiosculpterNutritionNotLoaded".Translate().CapitalizeFirst();
			}
			if (State == BiosculpterPodState.Occupied)
			{
				return "BiosculpterOccupied".Translate().CapitalizeFirst();
			}
			return null;
		}

		private List<IngredientCount> RequiredIngredients(CompBiosculpterPod_Cycle cycle)
		{
			List<ThingDefCountClass> extraRequiredIngredients = cycle.Props.extraRequiredIngredients;
			if (extraRequiredIngredients == null || devFillPodLatch)
			{
				return null;
			}
			if (!cachedExtraIngredients.ContainsKey(cycle))
			{
				cachedExtraIngredients[cycle] = extraRequiredIngredients.Select((ThingDefCountClass tc) => tc.ToIngredientCount()).ToList();
			}
			return cachedExtraIngredients[cycle];
		}

		private bool CanReachRequiredIngredients(Pawn pawn, CompBiosculpterPod_Cycle cycle, bool useCache = false)
		{
			chosenExtraItems.Clear();
			if (cycle.Props.extraRequiredIngredients == null || devFillPodLatch)
			{
				return true;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (useCache && cacheReachIngredientsPawn == pawn && cacheReachIngredientsCycle == cycle && realtimeSinceStartup < cacheReachIngredientsTime + 2f)
			{
				return cacheReachIngredientsResult;
			}
			cacheReachIngredientsPawn = pawn;
			cacheReachIngredientsCycle = cycle;
			cacheReachIngredientsTime = realtimeSinceStartup;
			cacheReachIngredientsResult = WorkGiver_DoBill.TryFindBestFixedIngredients(RequiredIngredients(cycle), pawn, parent, chosenExtraItems);
			return cacheReachIngredientsResult;
		}

		private bool SelectPawnCycleOption(Pawn pawn, CompBiosculpterPod_Cycle cycle, out FloatMenuOption option)
		{
			string text = CannotUseNowPawnReason(pawn) ?? CannotUseNowPawnCycleReason(pawn, cycle, checkIngredients: false);
			string label = pawn.Label + ((text == null) ? "" : (": " + text));
			Action action = null;
			if (text == null)
			{
				action = delegate
				{
					PrepareCycleJob(pawn, pawn, cycle, EnterBiosculpterJob());
				};
			}
			option = new FloatMenuOption(label, action);
			return text == null;
		}

		private bool SelectPawnsForCycleOptions(CompBiosculpterPod_Cycle cycle, out List<FloatMenuOption> options, bool shortCircuit = false)
		{
			cycleEligiblePawnOptions.Clear();
			options = cycleEligiblePawnOptions;
			if (!cachedAnyPawnEligible.ContainsKey(cycle))
			{
				cachedAnyPawnEligible[cycle] = new CacheAnyPawnEligibleCycle
				{
					gameTime = float.MinValue
				};
			}
			int ticksGame = Find.TickManager.TicksGame;
			if (shortCircuit && (float)ticksGame < cachedAnyPawnEligible[cycle].gameTime + 2f)
			{
				return cachedAnyPawnEligible[cycle].anyEligible;
			}
			cachedAnyPawnEligible[cycle].gameTime = ticksGame;
			if (biotunedTo != null)
			{
				if (biotunedTo.Dead || !biotunedTo.Spawned || biotunedTo.Map != parent.Map)
				{
					cachedAnyPawnEligible[cycle].anyEligible = false;
					return cachedAnyPawnEligible[cycle].anyEligible;
				}
				if (SelectPawnCycleOption(biotunedTo, cycle, out var option) && shortCircuit)
				{
					cachedAnyPawnEligible[cycle].anyEligible = true;
					return cachedAnyPawnEligible[cycle].anyEligible;
				}
				cycleEligiblePawnOptions.Add(option);
			}
			else
			{
				foreach (Pawn item in parent.Map.mapPawns.FreeColonistsSpawned)
				{
					if (SelectPawnCycleOption(item, cycle, out var option2) && shortCircuit)
					{
						cachedAnyPawnEligible[cycle].anyEligible = true;
						return cachedAnyPawnEligible[cycle].anyEligible;
					}
					cycleEligiblePawnOptions.Add(option2);
				}
			}
			cachedAnyPawnEligible[cycle].anyEligible = cycleEligiblePawnOptions.Count > 0;
			return cachedAnyPawnEligible[cycle].anyEligible;
		}

		public Job EnterBiosculpterJob()
		{
			return JobMaker.MakeJob(JobDefOf.EnterBiosculpterPod, parent);
		}

		private Job MakeCarryToBiosculpterJob(Pawn willBeCarried)
		{
			return JobMaker.MakeJob(JobDefOf.CarryToBiosculpterPod, willBeCarried, LocalTargetInfo.Invalid, parent);
		}

		public void ConfigureJobForCycle(Job job, CompBiosculpterPod_Cycle cycle, List<ThingCount> extraIngredients)
		{
			if (!extraIngredients.NullOrEmpty())
			{
				job.targetQueueB = new List<LocalTargetInfo>(extraIngredients.Count);
				job.countQueue = new List<int>(extraIngredients.Count);
				foreach (ThingCount extraIngredient in extraIngredients)
				{
					job.targetQueueB.Add(extraIngredient.Thing);
					job.countQueue.Add(extraIngredient.Count);
				}
			}
			job.haulMode = HaulMode.ToCellNonStorage;
			job.biosculpterCycleKey = cycle.Props.key;
		}

		public void PrepareCycleJob(Pawn hauler, Pawn biosculptee, CompBiosculpterPod_Cycle cycle, Job job)
		{
			OrderToPod(cycle, biosculptee, delegate
			{
				chosenExtraItems.Clear();
				if (!CanReachOrHasIngredients(hauler, biosculptee, cycle))
				{
					Messages.Message("BiosculpterMissingIngredients".Translate(IngredientsDescription(cycle).Named("INGREDIENTS")).CapitalizeFirst(), parent, MessageTypeDefOf.NegativeEvent, historical: false);
				}
				else
				{
					ConfigureJobForCycle(job, cycle, chosenExtraItems);
					if (cycle.Props.extraRequiredIngredients != null && !devFillPodLatch)
					{
						if (job.def == JobDefOf.CarryToBiosculpterPod)
						{
							Messages.Message("BiosculpterCarryStartedMessage".Translate(hauler.Named("PAWN"), IngredientsDescription(cycle).Named("INGREDIENTS"), biosculptee.Named("DOWNED"), cycle.Props.label.Named("CYCLE")), parent, MessageTypeDefOf.SilentInput, historical: false);
						}
						else
						{
							Messages.Message("BiosculpterLoadingStartedMessage".Translate(hauler.Named("PAWN"), IngredientsDescription(cycle).Named("INGREDIENTS"), cycle.Props.label.Named("CYCLE")), parent, MessageTypeDefOf.SilentInput, historical: false);
						}
					}
					if (hauler.jobs.TryTakeOrderedJob(job, JobTag.Misc))
					{
						SetQueuedInformation(job, biosculptee);
					}
				}
			});
		}

		public void ClearQueuedInformation()
		{
			SetQueuedInformation(null, null);
		}

		public void SetQueuedInformation(Job job, Pawn biosculptee)
		{
			queuedEnterJob = job;
			queuedPawn = biosculptee;
		}

		public bool CanAcceptNutrition(Thing thing)
		{
			return allowedNutritionSettings.AllowedToAccept(thing);
		}

		public bool CanAcceptOnceCycleChosen(Pawn pawn)
		{
			if (State != BiosculpterPodState.SelectingCycle || !PowerOn)
			{
				return false;
			}
			if (biotunedTo != null && biotunedTo != pawn)
			{
				return false;
			}
			return true;
		}

		public bool PawnCarryingExtraCycleIngredients(Pawn pawn, string cycleKey, bool remove = false)
		{
			return PawnCarryingExtraCycleIngredients(pawn, GetCycle(cycleKey), remove);
		}

		public bool PawnCarryingExtraCycleIngredients(Pawn pawn, CompBiosculpterPod_Cycle cycle, bool remove = false)
		{
			if (cycle.Props.extraRequiredIngredients.NullOrEmpty() || devFillPodLatch)
			{
				return true;
			}
			foreach (ThingDefCountClass extraRequiredIngredient in cycle.Props.extraRequiredIngredients)
			{
				if (pawn.inventory.Count(extraRequiredIngredient.thingDef) < extraRequiredIngredient.count)
				{
					return false;
				}
			}
			if (remove)
			{
				foreach (ThingDefCountClass extraRequiredIngredient2 in cycle.Props.extraRequiredIngredients)
				{
					pawn.inventory.RemoveCount(extraRequiredIngredient2.thingDef, extraRequiredIngredient2.count);
				}
			}
			return true;
		}

		public bool TryAcceptPawn(Pawn pawn, string cycleKey)
		{
			return TryAcceptPawn(pawn, GetCycle(cycleKey));
		}

		public bool TryAcceptPawn(Pawn pawn, CompBiosculpterPod_Cycle cycle)
		{
			if (!CanAcceptOnceCycleChosen(pawn))
			{
				return false;
			}
			if (!PawnCarryingExtraCycleIngredients(pawn, cycle, remove: true))
			{
				return false;
			}
			currentCycleKey = cycle.Props.key;
			innerContainer.ClearAndDestroyContents();
			pawnEnteringBiosculpter = pawn;
			bool num = pawn.DeSpawnOrDeselect();
			if (pawn.holdingOwner != null)
			{
				pawn.holdingOwner.TryTransferToContainer(pawn, innerContainer);
			}
			else
			{
				innerContainer.TryAdd(pawn);
			}
			if (num)
			{
				Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
			}
			pawnEnteringBiosculpter = null;
			currentCycleTicksRemaining = cycle.Props.durationDays * 60000f;
			liquifiedNutrition = 0f;
			devFillPodLatch = false;
			ClearQueuedInformation();
			return true;
		}

		public void EjectContents(bool interrupted, bool playSounds, Map destMap = null)
		{
			if (destMap == null)
			{
				destMap = parent.Map;
			}
			Pawn occupant = Occupant;
			currentCycleKey = null;
			currentCycleTicksRemaining = 0f;
			currentCyclePowerCutTicks = 0;
			liquifiedNutrition = 0f;
			devFillPodLatch = false;
			innerContainer.TryDropAll(parent.InteractionCell, destMap, ThingPlaceMode.Near);
			if (occupant != null)
			{
				FilthMaker.TryMakeFilth(parent.InteractionCell, destMap, ThingDefOf.Filth_PodSlime, new IntRange(3, 6).RandomInRange);
				if (interrupted)
				{
					occupant.needs?.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SoakingWet);
					occupant.health?.AddHediff(HediffDefOf.BiosculptingSickness);
				}
			}
			if (playSounds)
			{
				Props.exitSound?.PlayOneShot(SoundInfo.InMap(new TargetInfo(parent.Position, parent.Map)));
			}
		}

		private void CycleCompleted()
		{
			Pawn occupant = Occupant;
			CompBiosculpterPod_Cycle currentCycle = CurrentCycle;
			SetBiotuned(occupant);
			currentCycle.CycleCompleted(occupant);
			EjectContents(interrupted: false, playSounds: true);
			if (occupant != null)
			{
				Need_Food need_Food = occupant.needs?.food;
				if (need_Food != null)
				{
					need_Food.CurLevelPercentage = 1f;
				}
				Need_Rest need_Rest = occupant.needs?.rest;
				if (need_Rest != null)
				{
					need_Rest.CurLevelPercentage = 1f;
				}
				if (currentCycle.Props.gainThoughtOnCompletion != null)
				{
					occupant.needs?.mood?.thoughts.memories.TryGainMemory(ThoughtDefOf.AgeReversalReceived);
				}
				Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.UsedBiosculpterPod, occupant.Named(HistoryEventArgsNames.Doer)));
			}
		}

		private void LiquifyNutrition()
		{
			foreach (Thing item in (IEnumerable<Thing>)innerContainer)
			{
				float num = item.GetStatValue(StatDefOf.Nutrition) * (float)item.stackCount;
				if (num > 0f && !(item is Pawn))
				{
					liquifiedNutrition = Mathf.Min(5f, liquifiedNutrition + num);
					item.Destroy();
				}
			}
		}

		public override void CompTick()
		{
			if (!ModLister.CheckIdeology("Biosculpting"))
			{
				return;
			}
			base.CompTick();
			innerContainer.ThingOwnerTick();
			if (State != BiosculpterPodState.SelectingCycle || !PowerOn)
			{
				readyEffecter?.Cleanup();
				readyEffecter = null;
			}
			else if (Props.readyEffecter != null)
			{
				if (readyEffecter == null)
				{
					readyEffecter = Props.readyEffecter.Spawn();
					ColorizeEffecter(readyEffecter, Props.selectCycleColor);
					readyEffecter.Trigger(parent, new TargetInfo(parent.InteractionCell, parent.Map));
				}
				readyEffecter.EffectTick(parent, new TargetInfo(parent.InteractionCell, parent.Map));
			}
			if (State != BiosculpterPodState.Occupied)
			{
				progressBarEffecter?.Cleanup();
				progressBarEffecter = null;
				operatingEffecter?.Cleanup();
				operatingEffecter = null;
			}
			else
			{
				Pawn occupant = Occupant;
				biotunedCountdownTicks = 4800000;
				if (PowerOn)
				{
					int num = 1;
					currentCycleTicksRemaining -= (float)num * CycleSpeedFactor;
					if (currentCycleTicksRemaining <= 0f)
					{
						CycleCompleted();
					}
				}
				else
				{
					currentCyclePowerCutTicks++;
					if (currentCyclePowerCutTicks >= 60000)
					{
						EjectContents(interrupted: true, playSounds: true);
						Messages.Message("BiosculpterNoPowerEjectedMessage".Translate(occupant.Named("PAWN")), occupant, MessageTypeDefOf.NegativeEvent, historical: false);
					}
				}
				if (currentCycleTicksRemaining > 0f)
				{
					if (progressBarEffecter == null)
					{
						progressBarEffecter = EffecterDefOf.ProgressBar.Spawn();
					}
					progressBarEffecter.EffectTick(parent, TargetInfo.Invalid);
					MoteProgressBar moteProgressBar = (progressBarEffecter.children[0] as SubEffecter_ProgressBar)?.mote;
					if (moteProgressBar != null)
					{
						float num2 = CurrentCycle.Props.durationDays * 60000f;
						moteProgressBar.progress = 1f - Mathf.Clamp01(currentCycleTicksRemaining / num2);
						int num3 = (parent.RotatedSize.z - 1) / 2;
						moteProgressBar.offsetZ = 0f - ((float)num3 + 0.5f);
					}
					if (Props.operatingEffecter != null)
					{
						if (!PowerOn)
						{
							operatingEffecter?.Cleanup();
							operatingEffecter = null;
						}
						else
						{
							if (operatingEffecter == null)
							{
								operatingEffecter = Props.operatingEffecter.Spawn();
								ColorizeEffecter(operatingEffecter, CurrentCycle.Props.operatingColor);
								operatingEffecter.Trigger(parent, new TargetInfo(parent.InteractionCell, parent.Map));
							}
							operatingEffecter.EffectTick(parent, new TargetInfo(parent.InteractionCell, parent.Map));
						}
					}
				}
			}
			if (PowerOn && biotunedCountdownTicks > 0)
			{
				biotunedCountdownTicks--;
			}
			if (biotunedCountdownTicks <= 0)
			{
				SetBiotuned(null);
			}
			SetPower();
		}

		private void SetPower()
		{
			if (powerTraderComp == null)
			{
				powerTraderComp = parent.TryGetComp<CompPowerTrader>();
			}
			if (powerComp == null)
			{
				powerComp = parent.TryGetComp<CompPower>();
			}
			if (State == BiosculpterPodState.Occupied)
			{
				powerTraderComp.PowerOutput = -1f * powerComp.Props.PowerConsumption;
			}
			else
			{
				powerTraderComp.PowerOutput = -1f * Props.powerConsumptionStandby;
			}
		}

		private void ColorizeEffecter(Effecter effecter, Color color)
		{
			foreach (SubEffecter child in effecter.children)
			{
				SubEffecter_Sprayer subEffecter_Sprayer;
				if ((subEffecter_Sprayer = child as SubEffecter_Sprayer) != null)
				{
					subEffecter_Sprayer.colorOverride = color * child.def.color;
				}
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			Rot4 rotation = parent.Rotation;
			Vector3 s = new Vector3(parent.def.graphicData.drawSize.x * 0.9f, 1f, parent.def.graphicData.drawSize.y * 0.9f);
			Vector3 drawPos = parent.DrawPos;
			drawPos.y -= 0.08108108f;
			Matrix4x4 matrix = default(Matrix4x4);
			matrix.SetTRS(drawPos, rotation.AsQuat, s);
			Graphics.DrawMesh(MeshPool.plane10, matrix, BackgroundMat, 0);
			if (State == BiosculpterPodState.Occupied)
			{
				Pawn occupant = Occupant;
				Vector3 drawLoc = parent.DrawPos + FloatingOffset(currentCycleTicksRemaining + (float)currentCyclePowerCutTicks);
				Rot4 rotation2 = parent.Rotation;
				if (rotation2 == Rot4.East || rotation2 == Rot4.West)
				{
					drawLoc.z += 0.2f;
				}
				occupant.Drawer.renderer.RenderPawnAt(drawLoc, null, neverAimWeapon: true);
			}
		}

		public static Vector3 FloatingOffset(float tickOffset)
		{
			float num = tickOffset % 500f / 500f;
			float num2 = Mathf.Sin((float)Math.PI * num);
			float z = num2 * num2 * 0.04f;
			return new Vector3(0f, 0f, z);
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public StorageSettings GetStoreSettings()
		{
			return allowedNutritionSettings;
		}

		public StorageSettings GetParentStoreSettings()
		{
			return parent.def.building.fixedStorageSettings;
		}

		public void Notify_SettingsChanged()
		{
		}

		private static void OrderToPod(CompBiosculpterPod_Cycle cycle, Pawn pawn, Action giveJobAct)
		{
			CompBiosculpterPod_HealingCycle compBiosculpterPod_HealingCycle;
			if ((compBiosculpterPod_HealingCycle = cycle as CompBiosculpterPod_HealingCycle) != null)
			{
				string healingDescriptionForPawn = compBiosculpterPod_HealingCycle.GetHealingDescriptionForPawn(pawn);
				string text = (healingDescriptionForPawn.NullOrEmpty() ? "BiosculpterNoCoditionsToHeal".Translate(pawn.Named("PAWN"), compBiosculpterPod_HealingCycle.Props.label.Named("CYCLE")).Resolve() : ("OnCompletionOfCycle".Translate(compBiosculpterPod_HealingCycle.Props.label.Named("CYCLE")).Resolve() + ":\n\n" + healingDescriptionForPawn));
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(text, delegate
				{
					giveJobAct();
				}, healingDescriptionForPawn.NullOrEmpty()));
			}
			else
			{
				giveJobAct();
			}
		}

		public static Thing FindPodFor(Pawn pawn, Pawn traveller, bool biotuned)
		{
			foreach (ThingDef item in DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => def.GetCompProperties<CompProperties_BiosculpterPod>() != null))
			{
				Thing thing = GenClosest.ClosestThingReachable(traveller.Position, pawn.Map, ThingRequest.ForDef(item), PathEndMode.InteractionCell, TraverseParms.For(pawn), 9999f, Validator);
				if (thing != null)
				{
					return thing;
				}
			}
			return null;
			bool Validator(Thing t)
			{
				CompBiosculpterPod compBiosculpterPod = t.TryGetComp<CompBiosculpterPod>();
				if (biotuned && compBiosculpterPod.biotunedTo != traveller)
				{
					return false;
				}
				return compBiosculpterPod.CanAcceptOnceCycleChosen(traveller);
			}
		}

		public static void AddCarryToPodJobs(List<FloatMenuOption> opts, Pawn pawn, Pawn traveller)
		{
			if (!pawn.CanReserveAndReach(traveller, PathEndMode.OnCell, Danger.Deadly, 1, -1, null, ignoreOtherReservations: true))
			{
				return;
			}
			Thing thing = FindPodFor(pawn, traveller, biotuned: true) ?? FindPodFor(pawn, traveller, biotuned: false);
			if (thing == null)
			{
				return;
			}
			CompBiosculpterPod podComp = thing.TryGetComp<CompBiosculpterPod>();
			foreach (CompBiosculpterPod_Cycle cycle in podComp.AvailableCycles)
			{
				string text = "CarryToBiosculpterPod".Translate(traveller.Named("PAWN"), cycle.Props.label.Named("CYCLE"));
				Action action = delegate
				{
					if (!podComp.CanAcceptOnceCycleChosen(traveller))
					{
						Messages.Message("CannotCarryToBiosculpterPod".Translate() + ": " + "NoBiosculpterPod".Translate(), traveller, MessageTypeDefOf.RejectInput, historical: false);
					}
					else
					{
						podComp.PrepareCycleJob(pawn, traveller, cycle, podComp.MakeCarryToBiosculpterJob(traveller));
					}
				};
				if (traveller.IsQuestLodger())
				{
					text += " (" + "CryptosleepCasketGuestsNotAllowed".Translate() + ")";
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, null, MenuOptionPriority.Default, null, traveller), pawn, traveller));
					continue;
				}
				if (traveller.GetExtraHostFaction() != null)
				{
					text += " (" + "CryptosleepCasketGuestPrisonersNotAllowed".Translate() + ")";
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, null, MenuOptionPriority.Default, null, traveller), pawn, traveller));
					continue;
				}
				string text2 = podComp.CannotUseNowPawnCycleReason(pawn, traveller, cycle);
				if (text2 != null)
				{
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(CannotStartText(cycle, text2), null, MenuOptionPriority.Default, null, traveller), pawn, traveller));
				}
				else
				{
					opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, action, MenuOptionPriority.Default, null, traveller), pawn, traveller));
				}
			}
		}

		public static bool WasLoadingCanceled(Thing thing)
		{
			CompBiosculpterPod compBiosculpterPod = thing.TryGetComp<CompBiosculpterPod>();
			if (compBiosculpterPod != null && compBiosculpterPod.State != 0)
			{
				return true;
			}
			return false;
		}

		public void ClearCycle()
		{
			currentCycleKey = null;
		}

		public void Notify_NutritionAdded()
		{
			LiquifyNutrition();
			SoundDefOf.Standard_Drop.PlayOneShot(parent);
		}
	}
}
