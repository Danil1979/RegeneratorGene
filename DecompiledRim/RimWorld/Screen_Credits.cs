using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimWorld
{
	public class Screen_Credits : Window
	{
		private List<CreditsEntry> creds;

		public bool wonGame;

		public SongDef endCreditsSong;

		public bool exitToMainMenu;

		public float songStartDelay = 5f;

		private float timeUntilAutoScroll;

		private float scrollPosition;

		private bool playedMusic;

		public float creationRealtime = -1f;

		private float victoryTextHeight;

		private const int ColumnWidth = 800;

		private const float InitialAutoScrollDelay = 1f;

		private const float InitialAutoScrollDelayWonGame = 6f;

		private const float AutoScrollDelayAfterManualScroll = 3f;

		private const float VictoryTextScrollSpeed = 20f;

		private const float ScrollSpeedLerpHeight = 200f;

		private const GameFont Font = GameFont.Medium;

		public const float DefaultSongStartDelay = 5f;

		public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);

		protected override float Margin => 0f;

		private float ViewWidth => 800f;

		private float ViewHeight
		{
			get
			{
				GameFont font = Text.Font;
				Text.Font = GameFont.Medium;
				float result = creds.Sum((CreditsEntry c) => c.DrawHeight(ViewWidth)) + 20f;
				Text.Font = font;
				return result;
			}
		}

		private float MaxScrollPosition => Mathf.Max(ViewHeight - (float)UI.screenHeight / 2f, 0f);

		private float AutoScrollRate
		{
			get
			{
				if (wonGame)
				{
					if (scrollPosition < victoryTextHeight - 200f)
					{
						return 20f;
					}
					float num = EndCreditsSong.clip.length + songStartDelay - 6f - victoryTextHeight / 20f;
					float t = (scrollPosition - victoryTextHeight) / 200f;
					return Mathf.Lerp(20f, MaxScrollPosition / num, t);
				}
				return 30f;
			}
		}

		private SongDef EndCreditsSong
		{
			get
			{
				if (endCreditsSong == null)
				{
					return SongDefOf.EndCreditsSong;
				}
				return endCreditsSong;
			}
		}

		public Screen_Credits()
			: this("")
		{
		}

		public Screen_Credits(string preCreditsMessage)
		{
			doWindowBackground = false;
			doCloseButton = false;
			doCloseX = false;
			forcePause = true;
			creds = CreditsAssembler.AllCredits().ToList();
			creds.Insert(0, new CreditRecord_Space(100f));
			if (!preCreditsMessage.NullOrEmpty())
			{
				creds.Insert(1, new CreditRecord_Space(200f));
				creds.Insert(2, new CreditRecord_Text(preCreditsMessage));
				creds.Insert(3, new CreditRecord_Space(50f));
				Text.Font = GameFont.Medium;
				victoryTextHeight = creds.Take(4).Sum((CreditsEntry c) => c.DrawHeight(ViewWidth));
			}
			creds.Add(new CreditRecord_Space(300f));
			creds.Add(new CreditRecord_Text("ThanksForPlaying".Translate(), TextAnchor.UpperCenter));
			string text = string.Empty;
			foreach (CreditsEntry cred in creds)
			{
				CreditRecord_Role creditRecord_Role = cred as CreditRecord_Role;
				if (creditRecord_Role == null)
				{
					text = string.Empty;
					continue;
				}
				creditRecord_Role.displayKey = text.NullOrEmpty() || creditRecord_Role.roleKey != text;
				text = creditRecord_Role.roleKey;
			}
		}

		public override void PreOpen()
		{
			base.PreOpen();
			creationRealtime = Time.realtimeSinceStartup;
			if (wonGame)
			{
				timeUntilAutoScroll = 6f;
			}
			else
			{
				timeUntilAutoScroll = 1f;
			}
		}

		public override void PostClose()
		{
			base.PostOpen();
			if (exitToMainMenu)
			{
				GenScene.GoToMainMenu();
			}
		}

		public override void WindowUpdate()
		{
			base.WindowUpdate();
			if (timeUntilAutoScroll > 0f)
			{
				timeUntilAutoScroll -= Time.deltaTime;
			}
			else
			{
				scrollPosition += AutoScrollRate * Time.deltaTime;
			}
			if (wonGame && !playedMusic && Time.realtimeSinceStartup > creationRealtime + songStartDelay)
			{
				Find.MusicManagerPlay.ForceStartSong(EndCreditsSong, ignorePrefsVolume: true);
				playedMusic = true;
			}
		}

		public override void DoWindowContents(Rect inRect)
		{
			Rect rect = new Rect(0f, 0f, UI.screenWidth, UI.screenHeight);
			GUI.DrawTexture(rect, BaseContent.BlackTex);
			Rect rect2 = new Rect(rect);
			rect2.yMin += 30f;
			rect2.yMax -= 30f;
			rect2.xMin = rect.center.x - 400f;
			rect2.width = 800f;
			float viewWidth = ViewWidth;
			float viewHeight = ViewHeight;
			scrollPosition = Mathf.Clamp(scrollPosition, 0f, MaxScrollPosition);
			Widgets.BeginGroup(rect2);
			Rect rect3 = new Rect(0f, 0f, viewWidth, viewHeight);
			rect3.y -= scrollPosition;
			Widgets.BeginGroup(rect3);
			Text.Font = GameFont.Medium;
			float num = 0f;
			foreach (CreditsEntry cred in creds)
			{
				float num2 = cred.DrawHeight(rect3.width);
				Rect rect4 = new Rect(0f, num, rect3.width, num2);
				cred.Draw(rect4);
				num += num2;
			}
			Widgets.EndGroup();
			Widgets.EndGroup();
			if (Event.current.type == EventType.ScrollWheel)
			{
				Scroll(Event.current.delta.y * 25f);
				Event.current.Use();
			}
			if (Event.current.type == EventType.KeyDown)
			{
				if (Event.current.keyCode == KeyCode.DownArrow)
				{
					Scroll(250f);
					Event.current.Use();
				}
				if (Event.current.keyCode == KeyCode.UpArrow)
				{
					Scroll(-250f);
					Event.current.Use();
				}
			}
		}

		private void Scroll(float offset)
		{
			scrollPosition += offset;
			timeUntilAutoScroll = 3f;
		}
	}
}
