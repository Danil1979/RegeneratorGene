using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace Verse
{
	public class PrefsData
	{
		public float volumeMaster = 0.8f;

		public float volumeGame = 1f;

		public float volumeMusic = 0.4f;

		public float volumeAmbient = 1f;

		public float volumeUI = 1f;

		public int screenWidth;

		public int screenHeight;

		public bool fullscreen;

		public float uiScale = 1f;

		public bool customCursorEnabled = true;

		public bool hatsOnlyOnMap;

		public bool plantWindSway = true;

		public float screenShakeIntensity = 1f;

		public bool textureCompression = true;

		public bool showRealtimeClock;

		public bool disableTinyText;

		public AnimalNameDisplayMode animalNameMode;

		public MechNameDisplayMode mechNameMode = MechNameDisplayMode.WhileDrafted;

		public string backgroundExpansionId;

		public bool randomBackground;

		public ShowWeaponsUnderPortraitMode showWeaponsUnderPortraitMode = ShowWeaponsUnderPortraitMode.WhileDrafted;

		[Obsolete]
		public bool extremeDifficultyUnlocked;

		public bool adaptiveTrainingEnabled = true;

		public bool steamDeckKeyboardMode;

		public List<string> preferredNames = new List<string>();

		public bool resourceReadoutCategorized;

		public bool runInBackground;

		public bool edgeScreenScroll = true;

		public TemperatureDisplayMode temperatureMode;

		public float autosaveIntervalDays = 1f;

		public bool testMapSizes;

		[LoadAlias("maxNumberOfPlayerHomes")]
		public int maxNumberOfPlayerSettlements = 1;

		public bool pauseOnLoad;

		public AutomaticPauseMode automaticPauseMode = AutomaticPauseMode.MajorThreat;

		public float mapDragSensitivity = 1.3f;

		public bool smoothCameraJumps = true;

		[Unsaved(true)]
		public bool? pauseOnUrgentLetter;

		public bool devMode;

		public List<string> debugActionPalette = new List<string>();

		public Vector2 devPalettePosition;

		public string langFolderName = "unknown";

		public bool logVerbose;

		public bool pauseOnError;

		public bool resetModsConfigOnCrash = true;

		public bool simulateNotOwningRoyalty;

		public bool simulateNotOwningIdeology;

		public bool simulateNotOwningBiotech;

		public void Apply()
		{
			if (UnityData.IsInMainThread)
			{
				if (customCursorEnabled)
				{
					CustomCursor.Activate();
				}
				else
				{
					CustomCursor.Deactivate();
				}
				AudioListener.volume = volumeMaster;
				Application.runInBackground = runInBackground;
				if (screenWidth == 0 || screenHeight == 0)
				{
					ResolutionUtility.SetNativeResolutionRaw();
				}
				else
				{
					ResolutionUtility.SetResolutionRaw(screenWidth, screenHeight, !ResolutionUtility.BorderlessFullscreen && fullscreen);
				}
			}
		}
	}
}
