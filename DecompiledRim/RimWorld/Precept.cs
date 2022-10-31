using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace RimWorld
{
	public class Precept : IExposable, ILoadReferenceable
	{
		public Ideo ideo;

		public PreceptDef def;

		protected string name;

		private int ID = -1;

		public bool usesDefiniteArticle = true;

		public string descOverride;

		public int randomSeed;

		public bool nameLocked;

		protected List<Thought_Situational> tmpSituationalThoughts = new List<Thought_Situational>();

		protected string labelCapCache;

		protected string tipCached;

		public const int FloatMenuEditOrder = 2000;

		public const int FloatMenuRemoveOrder = 1900;

		public const int FloatMenuRegenerateOrder = 1800;

		public const float OutlineBrightnessOffset = 0.05f;

		protected static StringBuilder tmpCompsDesc = new StringBuilder();

		private static HashSet<string> tmpUsedWords = new HashSet<string>();

		public virtual string TipLabel => def.tipLabelOverride ?? ((string)(def.issue.LabelCap + ": " + def.LabelCap));

		public virtual string UIInfoFirstLine => def.issue.LabelCap;

		public virtual string UIInfoSecondLine => def.LabelCap;

		public virtual bool CanRegenerate => false;

		public virtual bool SortByImpact => true;

		public virtual Color LabelColor => new Color(0.8f, 0.8f, 0.8f);

		public virtual string Label => name;

		public string LabelCap
		{
			get
			{
				if (labelCapCache == null)
				{
					labelCapCache = name.CapitalizeFirst();
				}
				return labelCapCache;
			}
		}

		public string Description
		{
			get
			{
				if (!descOverride.NullOrEmpty())
				{
					return descOverride;
				}
				return def.description;
			}
		}

		public virtual string DescriptionForTip => Description;

		public virtual bool UsesGeneratedName => false;

		public virtual Texture2D Icon => def.Icon ?? ideo.Icon;

		public virtual List<PreceptApparelRequirement> ApparelRequirements
		{
			get
			{
				return null;
			}
			set
			{
			}
		}

		public int Id => ID;

		public virtual void PostMake()
		{
		}

		public virtual void Init(Ideo ideo, FactionDef generatingFor = null)
		{
			this.ideo = ideo;
			ID = Find.UniqueIDsManager.GetNextPreceptID();
			randomSeed = Rand.Int;
			name = def.issue.label;
		}

		public virtual void Regenerate(Ideo ideo, FactionDef generatingFor = null)
		{
			Init(ideo, generatingFor);
			ClearTipCache();
			ideo.anyPreceptEdited = true;
		}

		public virtual void ClearTipCache()
		{
			tipCached = null;
		}

		protected string ColorizeDescTitle(TaggedString title)
		{
			return title.Resolve().Colorize(ColoredText.TipSectionTitleColor);
		}

		protected string ColorizeWarning(TaggedString title)
		{
			return title.Resolve().Colorize(ColoredText.ThreatColor);
		}

		public virtual string GetTip()
		{
			tmpCompsDesc.Clear();
			if (!DescriptionForTip.NullOrEmpty())
			{
				tmpCompsDesc.Append(DescriptionForTip);
			}
			bool flag = true;
			for (int i = 0; i < def.comps.Count; i++)
			{
				PreceptComp_UnwillingToDo preceptComp_UnwillingToDo;
				if ((preceptComp_UnwillingToDo = def.comps[i] as PreceptComp_UnwillingToDo) == null)
				{
					continue;
				}
				string prohibitionText = preceptComp_UnwillingToDo.GetProhibitionText();
				if (!prohibitionText.NullOrEmpty())
				{
					if (flag)
					{
						flag = false;
						tmpCompsDesc.AppendLine();
						tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("Prohibitions".Translate() + ":"));
					}
					tmpCompsDesc.AppendInNewLine("  - " + prohibitionText);
				}
			}
			bool flag2 = true;
			for (int j = 0; j < def.comps.Count; j++)
			{
				PreceptComp_SelfTookMemoryThought preceptComp_SelfTookMemoryThought;
				PreceptComp preceptComp;
				PreceptComp_SituationalThought preceptComp_SituationalThought;
				PreceptComp_KnowsMemoryThought preceptComp_KnowsMemoryThought;
				if ((preceptComp_SelfTookMemoryThought = def.comps[j] as PreceptComp_SelfTookMemoryThought) != null)
				{
					preceptComp = preceptComp_SelfTookMemoryThought;
				}
				else if ((preceptComp_SituationalThought = def.comps[j] as PreceptComp_SituationalThought) != null && preceptComp_SituationalThought.AffectsMood)
				{
					preceptComp = preceptComp_SituationalThought;
				}
				else if ((preceptComp_KnowsMemoryThought = def.comps[j] as PreceptComp_KnowsMemoryThought) != null && preceptComp_KnowsMemoryThought.AffectsMood)
				{
					preceptComp = preceptComp_KnowsMemoryThought;
				}
				else
				{
					PreceptComp_BedThought preceptComp_BedThought;
					if ((preceptComp_BedThought = def.comps[j] as PreceptComp_BedThought) == null || !preceptComp_BedThought.AffectsMood)
					{
						continue;
					}
					preceptComp = preceptComp_BedThought;
				}
				if (flag2)
				{
					flag2 = false;
					tmpCompsDesc.AppendLine();
					tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("Mood".Translate() + ":"));
				}
				foreach (string description in preceptComp.GetDescriptions())
				{
					tmpCompsDesc.AppendInNewLine("  - " + description);
				}
			}
			bool flag3 = true;
			for (int k = 0; k < def.comps.Count; k++)
			{
				PreceptComp_SituationalThought preceptComp_SituationalThought2;
				PreceptComp preceptComp2;
				if ((preceptComp_SituationalThought2 = def.comps[k] as PreceptComp_SituationalThought) != null && !preceptComp_SituationalThought2.AffectsMood)
				{
					preceptComp2 = preceptComp_SituationalThought2;
				}
				else
				{
					PreceptComp_KnowsMemoryThought preceptComp_KnowsMemoryThought2;
					if ((preceptComp_KnowsMemoryThought2 = def.comps[k] as PreceptComp_KnowsMemoryThought) == null || preceptComp_KnowsMemoryThought2.AffectsMood)
					{
						continue;
					}
					preceptComp2 = preceptComp_KnowsMemoryThought2;
				}
				if (flag3)
				{
					flag3 = false;
					tmpCompsDesc.AppendLine();
					tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("Opinions".Translate() + ":"));
				}
				foreach (string description2 in preceptComp2.GetDescriptions())
				{
					tmpCompsDesc.AppendInNewLine("  - " + description2);
				}
			}
			bool flag4 = true;
			if (def.statOffsets != null)
			{
				for (int l = 0; l < def.statOffsets.Count; l++)
				{
					if (flag4)
					{
						tmpCompsDesc.AppendLine();
						tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("PreceptStats".Translate() + ":"));
						flag4 = false;
					}
					tmpCompsDesc.AppendInNewLine("  - " + def.statOffsets[l].stat.LabelCap + ": " + def.statOffsets[l].ValueToStringAsOffset);
				}
			}
			if (def.statFactors != null)
			{
				for (int m = 0; m < def.statFactors.Count; m++)
				{
					if (flag4)
					{
						tmpCompsDesc.AppendLine();
						tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("PreceptStats".Translate() + ":"));
						flag4 = false;
					}
					tmpCompsDesc.AppendInNewLine("  - " + def.statFactors[m].stat.LabelCap + ": " + def.statFactors[m].ToStringAsFactor);
				}
			}
			if (def.abilityStatFactors != null)
			{
				for (int n = 0; n < def.abilityStatFactors.Count; n++)
				{
					foreach (StatModifier modifier in def.abilityStatFactors[n].modifiers)
					{
						if (flag4)
						{
							tmpCompsDesc.AppendLine();
							tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("PreceptStats".Translate() + ":"));
							flag4 = false;
						}
						tmpCompsDesc.AppendInNewLine("  - " + def.abilityStatFactors[n].ability.LabelCap + ": " + modifier.stat.LabelCap + ": " + modifier.ToStringAsFactor);
					}
				}
			}
			if (def.TraitsAffecting.Count != 0)
			{
				tmpCompsDesc.AppendLine();
				tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("AffectedByTraits".Translate() + ":"));
				foreach (TraitDegreeData item in def.TraitsAffecting.Select((TraitRequirement x) => x.def.DataAtDegree(x.degree.HasValue ? x.degree.Value : 0)))
				{
					tmpCompsDesc.AppendInNewLine("  - " + item.GetLabelFor(Gender.Male).CapitalizeFirst());
				}
			}
			bool flag5 = true;
			if (ideo.Fluid)
			{
				for (int num = 0; num < def.comps.Count; num++)
				{
					if (!(def.comps[num] is PreceptComp_DevelopmentPoints))
					{
						continue;
					}
					if (flag5)
					{
						flag5 = false;
						tmpCompsDesc.AppendLine();
						tmpCompsDesc.AppendInNewLine(ColorizeDescTitle("DevelopmentPoints".Translate() + ":"));
					}
					foreach (string description3 in def.comps[num].GetDescriptions())
					{
						tmpCompsDesc.AppendInNewLine("  - " + description3);
					}
				}
			}
			return tmpCompsDesc.ToString();
		}

		public virtual IEnumerable<Alert> GetAlerts()
		{
			return Enumerable.Empty<Alert>();
		}

		public virtual void Tick()
		{
		}

		public virtual string GenerateNameRaw()
		{
			throw new NotImplementedException();
		}

		public virtual List<PreceptApparelRequirement> GenerateNewApparelRequirements(FactionDef generatingFor = null)
		{
			return null;
		}

		public string GenerateNewName()
		{
			tmpUsedWords.Clear();
			foreach (Precept item2 in ideo.PreceptsListForReading)
			{
				if (item2 != this && item2.GetType() == GetType() && item2.name != null)
				{
					tmpUsedWords.Add(item2.name);
					string[] array = item2.name.Split(' ');
					foreach (string item in array)
					{
						tmpUsedWords.Add(item);
					}
				}
			}
			float num = 0f;
			string text = null;
			if (def.takeNameFrom != null)
			{
				return ideo.PreceptsListForReading.First((Precept p) => p.def == def.takeNameFrom).name;
			}
			labelCapCache = null;
			if (def.ignoreNameUniqueness || ideo.hiddenIdeoMode)
			{
				text = GenerateNameRaw();
			}
			else
			{
				int num2 = 0;
				while (true)
				{
					string text2 = GenerateNameRaw();
					float num3 = NameUniqueness(text2);
					if (num3 >= 1f)
					{
						text = text2;
						num = num3;
						break;
					}
					if (num3 > num)
					{
						text = text2;
						num = num3;
					}
					if (num2++ > 50)
					{
						if (def.visible)
						{
							Log.Warning("Failed to generate a unique precept name: " + text + " at " + num.ToStringPercent() + " uniqueness");
						}
						text = text2;
						break;
					}
				}
			}
			return text;
			static float NameUniqueness(string newName)
			{
				string[] array2 = newName.Split(' ');
				int num4 = 0;
				int num5 = 0;
				string[] array3 = array2;
				foreach (string text3 in array3)
				{
					if (text3.Length >= 4)
					{
						num5++;
						if (tmpUsedWords.Contains(text3))
						{
							num4++;
						}
					}
				}
				if (num5 != 0)
				{
					return 1f - (float)num4 / (float)num5;
				}
				return 1f;
			}
		}

		public void RegenerateName()
		{
			SetName(GenerateNewName());
		}

		public void SetName(string newName)
		{
			if (newName == name)
			{
				return;
			}
			labelCapCache = null;
			name = newName;
			if (ideo == null)
			{
				return;
			}
			foreach (Precept item in ideo.PreceptsListForReading)
			{
				if (item.def.takeNameFrom == def)
				{
					item.RegenerateName();
				}
			}
		}

		public virtual IEnumerable<FloatMenuOption> EditFloatMenuOptions()
		{
			return null;
		}

		public virtual List<Thought_Situational> SituationThoughtsToAdd(Pawn pawn, List<Thought_Situational> activeThoughts)
		{
			tmpSituationalThoughts.Clear();
			return tmpSituationalThoughts;
		}

		protected void AddIdeoRulesTo(ref GrammarRequest request)
		{
			string ideoName = null;
			if (TryGuessChosenSymbolPack(out var result))
			{
				if (!result.ideoName.NullOrEmpty())
				{
					request.Rules.Add(new Rule_String("chosenIdeoName", result.ideoName));
					ideoName = result.ideoName;
				}
				if (!result.theme.NullOrEmpty())
				{
					request.Rules.Add(new Rule_String("chosenTheme", MatchIdeoNameCapitalization(result.theme, ideoName)));
				}
			}
			foreach (MemeDef meme in ideo.memes)
			{
				if (meme.generalRules != null)
				{
					request.IncludesBare.Add(meme.generalRules);
				}
				if (meme.symbolPacks == null)
				{
					continue;
				}
				foreach (IdeoSymbolPack symbolPack in meme.symbolPacks)
				{
					if (!symbolPack.theme.NullOrEmpty())
					{
						request.Rules.Add(new Rule_String("memePackTheme", MatchIdeoNameCapitalization(symbolPack.theme, ideoName)));
					}
					if (!symbolPack.adjective.NullOrEmpty())
					{
						request.Rules.Add(new Rule_String("memePackAdjective", MatchIdeoNameCapitalization(symbolPack.adjective, ideoName)));
					}
				}
			}
			if (!ideo.adjective.NullOrEmpty())
			{
				request.Rules.Add(new Rule_String("chosenAdjective", ideo.adjective));
			}
			if (ideo.KeyDeityName != null)
			{
				request.Rules.Add(new Rule_String("keyDeity", ideo.KeyDeityName));
			}
			if (!ideo.leaderTitleMale.NullOrEmpty())
			{
				request.Rules.Add(new Rule_String("leaderTitle", ideo.leaderTitleMale));
			}
		}

		private bool TryGuessChosenSymbolPack(out IdeoSymbolPack result)
		{
			foreach (MemeDef meme in ideo.memes)
			{
				if (meme.symbolPacks == null)
				{
					continue;
				}
				foreach (IdeoSymbolPack symbolPack in meme.symbolPacks)
				{
					if (symbolPack.adjective == ideo.adjective || symbolPack.member == ideo.memberName || (symbolPack.ideoName != null && ideo.name.Contains(symbolPack.ideoName)))
					{
						result = symbolPack;
						return true;
					}
				}
			}
			result = new IdeoSymbolPack();
			return false;
		}

		private string MatchIdeoNameCapitalization(string value, string ideoName)
		{
			if (value.Equals(ideoName, StringComparison.InvariantCultureIgnoreCase))
			{
				return ideoName;
			}
			return value;
		}

		public virtual void DrawPreceptBox(Rect preceptBox, IdeoEditMode editMode, bool forceHighlight = false)
		{
			GUI.color = Color.white;
			Color backgroundColor = IdeoUIUtility.GetBackgroundColor(def.impact);
			Widgets.DrawRectFast(preceptBox, backgroundColor);
			GUI.color = new Color(backgroundColor.r + 0.05f, backgroundColor.g + 0.05f, backgroundColor.b + 0.05f);
			Widgets.DrawBox(preceptBox);
			GUI.color = Color.white;
			if (Mouse.IsOver(preceptBox) || forceHighlight)
			{
				Widgets.DrawHighlight(preceptBox);
			}
			if (editMode != 0 && Widgets.ButtonInvisible(preceptBox) && IdeoUIUtility.TutorAllowsInteraction(editMode))
			{
				List<FloatMenuOption> list = new List<FloatMenuOption>();
				MemeDef memeThatRequiresPrecept = ideo.GetMemeThatRequiresPrecept(def);
				bool flag = false;
				IEnumerable<FloatMenuOption> enumerable = EditFloatMenuOptions();
				if (enumerable != null)
				{
					foreach (FloatMenuOption item in enumerable)
					{
						flag = true;
						item.orderInPriority = 2000;
						list.Add(item);
					}
				}
				FloatMenuOption floatMenuOption = null;
				if (def.canRemoveInUI && !def.issue.HasDefaultPrecept)
				{
					floatMenuOption = new FloatMenuOption("Remove".Translate().CapitalizeFirst(), delegate
					{
						ideo.RemovePrecept(this);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, playSelectionSound: true, 1900);
					MemeDef memeThatRequiresPrecept2 = ideo.GetMemeThatRequiresPrecept(def);
					if (editMode != IdeoEditMode.Dev && memeThatRequiresPrecept2 != null)
					{
						floatMenuOption.action = null;
						floatMenuOption.Label = "CannotRemove".Translate() + ": " + "RequiredByMeme".Translate(memeThatRequiresPrecept2.label);
					}
					else
					{
						flag = true;
					}
				}
				if (CanRegenerate)
				{
					FactionDef faction = ((Find.World == null) ? Find.Scenario.playerFaction.factionDef : null);
					list.Add(new FloatMenuOption("Regenerate".Translate().CapitalizeFirst(), delegate
					{
						Regenerate(ideo, faction);
					}, MenuOptionPriority.Default, null, null, 0f, null, null, playSelectionSound: true, 1800));
				}
				else if (GetType() == typeof(Precept))
				{
					foreach (PreceptDef p in DefDatabase<PreceptDef>.AllDefs.Where((PreceptDef x) => x != def && x.issue == def.issue))
					{
						AcceptanceReport acceptanceReport = IdeoUIUtility.CanListPrecept(ideo, p, editMode);
						if ((bool)acceptanceReport || !string.IsNullOrWhiteSpace(acceptanceReport.Reason))
						{
							TaggedString labelCap = p.LabelCap;
							Action action = delegate
							{
								ideo.AddPrecept(PreceptMaker.MakePrecept(p), init: true);
								ideo.RemovePrecept(this, replacing: true);
							};
							if (!acceptanceReport)
							{
								action = null;
								labelCap += " (" + acceptanceReport.Reason + ")";
							}
							FloatMenuOption floatMenuOption2 = new FloatMenuOption(labelCap, action, p.issue.Icon, IdeoUIUtility.GetIconAndLabelColor(p.impact));
							floatMenuOption2.orderInPriority = p.displayOrderInIssue;
							flag = true;
							list.Add(floatMenuOption2);
						}
					}
				}
				if (flag)
				{
					if (floatMenuOption != null)
					{
						list.Add(floatMenuOption);
					}
				}
				else if (memeThatRequiresPrecept == null)
				{
					list.Add(new FloatMenuOption("CannotEdit".Translate() + ": " + "Required".Translate(), null));
				}
				else
				{
					list.Add(new FloatMenuOption("CannotEdit".Translate() + ": " + "RequiredByMeme".Translate(memeThatRequiresPrecept.LabelCap), null));
				}
				PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.EditingPrecepts, KnowledgeAmount.Total);
				Find.WindowStack.Add(new FloatMenu(list));
			}
			GUI.color = IdeoUIUtility.GetIconAndLabelColor(def.impact);
			Rect rect = new Rect(preceptBox.x + (preceptBox.height - 50f) / 2f, preceptBox.y + (preceptBox.height - 50f) / 2f, 50f, 50f);
			DrawIcon(rect);
			Rect rect2 = new Rect(rect.xMax + 10f, preceptBox.y + 3f, preceptBox.xMax - rect.xMax - 10f, preceptBox.height / 2f);
			Text.Anchor = TextAnchor.MiddleLeft;
			GUI.color = LabelColor;
			Widgets.Label(rect2, UIInfoFirstLine);
			GUI.color = Color.white;
			GenUI.ResetLabelAlign();
			GUI.color = IdeoUIUtility.GetIconAndLabelColor(def.impact);
			Rect rect3 = new Rect(rect.xMax + 10f, preceptBox.y + preceptBox.height / 2f - 3f, preceptBox.xMax - rect.xMax - 10f, preceptBox.height / 2f);
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.LabelFit(rect3, UIInfoSecondLine);
			GenUI.ResetLabelAlign();
			PostDrawBox(preceptBox, out var anyIconTooltipActive);
			if (!anyIconTooltipActive && Mouse.IsOver(preceptBox) && Find.WindowStack.WindowOfType<FloatMenu>() == null)
			{
				TooltipHandler.TipRegion(preceptBox, TipLabel.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + GetTip() + ((editMode != 0) ? ("\n\n" + IdeoUIUtility.ClickToEdit) : string.Empty));
			}
		}

		public virtual void DrawIcon(Rect rect)
		{
			GUI.DrawTexture(rect, def.issue.Icon);
		}

		public virtual bool CompatibleWith(Precept other)
		{
			return true;
		}

		public virtual bool GetPlayerWarning(out string shortText, out string description)
		{
			shortText = (description = null);
			return false;
		}

		protected virtual void PostDrawBox(Rect rect, out bool anyIconTooltipActive)
		{
			anyIconTooltipActive = false;
		}

		public FloatMenuOption EditFloatMenuOption()
		{
			return new FloatMenuOption("Edit".Translate() + "...", delegate
			{
				Find.WindowStack.Add(new Dialog_EditPrecept(this));
			});
		}

		public virtual void Notify_MemberChangedFaction(Pawn p, Faction oldFaction, Faction newFaction)
		{
		}

		public virtual void Notify_IdeoNotPrimaryAnymore(Ideo newIdeo)
		{
		}

		public virtual void Notify_MemberLost(Pawn pawn)
		{
		}

		public virtual void Notify_MemberGained(Pawn pawn)
		{
		}

		public virtual void Notify_MemberGuestStatusChanged(Pawn pawn)
		{
		}

		public virtual void Notify_GameStarted()
		{
		}

		public virtual void Notify_IdeoReformed()
		{
		}

		public virtual void Notify_MemberGenerated(Pawn pawn, bool newborn)
		{
			for (int i = 0; i < def.comps.Count; i++)
			{
				def.comps[i].Notify_MemberGenerated(pawn, this, newborn);
			}
		}

		public virtual void Notify_HistoryEvent(HistoryEvent ev)
		{
			for (int i = 0; i < def.comps.Count; i++)
			{
				def.comps[i].Notify_HistoryEvent(ev, this);
			}
		}

		public virtual void Notify_RecachedPrecepts()
		{
		}

		public virtual bool TryGetLostByReformingWarning(out string warning)
		{
			warning = null;
			return false;
		}

		public virtual void Notify_RemovedByReforming()
		{
		}

		public virtual void ExposeData()
		{
			Scribe_Values.Look(ref name, "name");
			Scribe_Defs.Look(ref def, "def");
			Scribe_Values.Look(ref ID, "ID", -1);
			Scribe_Values.Look(ref randomSeed, "randomSeed", 0);
			Scribe_Values.Look(ref usesDefiniteArticle, "usesDefiniteArticle", defaultValue: false);
			Scribe_Values.Look(ref descOverride, "descOverride");
			Scribe_Values.Look(ref nameLocked, "nameLocked", defaultValue: false);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && (ID == -1 || GameDataSaveLoader.IsSavingOrLoadingExternalIdeo))
			{
				ID = Find.UniqueIDsManager.GetNextPreceptID();
			}
		}

		public virtual void Notify_MemberDied(Pawn p)
		{
		}

		public virtual void Notify_MemberCorpseDestroyed(Pawn p)
		{
		}

		public string GetUniqueLoadID()
		{
			return "Precept_" + ID;
		}

		public virtual void CopyTo(Precept other)
		{
			other.ideo = ideo;
			other.def = def;
			other.name = name;
			other.ID = ID;
			other.usesDefiniteArticle = usesDefiniteArticle;
			other.descOverride = descOverride;
			other.randomSeed = randomSeed;
			other.nameLocked = nameLocked;
		}
	}
}
