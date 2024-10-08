using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static UnityEngine.Rendering.HighDefinition.ScalableSettingLevelParameter;

namespace TrapSpawnTweaks
{
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class TrapSpawnTweaks : BaseUnityPlugin
	{
		#region Config

		static ConfigFile configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "TrapSpawnTweaks.cfg"), false);

		struct LevelConfig
		{
			//public string levelName;
			public ConfigEntry<float> landmineSpawnMultiplier;
			public ConfigEntry<float> turretSpawnMultiplier;
			public ConfigEntry<float> spikeTrapSpawnMultiplier;
		}

		static float landmineSpawnMultiplier = 1f;
		static float turretSpawnMultiplier = 1f;
		static float spikeTrapSpawnMultiplier = 1f;

		static bool limitNumberToSpawn = false;
		static float maxNumberToSpawn = 100f;

		static void LoadGlobalConfig()
		{
			//ConfigFile configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "TrapSpawnTweaks.cfg"), false);

			LevelConfig globalCfg = new LevelConfig();

			const string globalCfgSection = "- Global -";

			globalCfg.landmineSpawnMultiplier = configFile.Bind(globalCfgSection, "Landmine Spawn Multiplier", 1f);
			globalCfg.turretSpawnMultiplier = configFile.Bind(globalCfgSection, "Turret Spawn Multiplier", 1f);
			globalCfg.spikeTrapSpawnMultiplier = configFile.Bind(globalCfgSection, "Spike Trap Spawn Multiplier", 1f);

			ConfigEntry<bool> limitNumberToSpawnCfg = configFile.Bind(globalCfgSection, "Limit Number To Spawn", false, "(Experimental) Limits max spawns of each type of trap.");
			ConfigEntry<float> maxNumberToSpawnCfg = configFile.Bind(globalCfgSection, "Max Number To Spawn", 100f);
			limitNumberToSpawn = limitNumberToSpawnCfg.Value;
			maxNumberToSpawn = maxNumberToSpawnCfg.Value;

			landmineSpawnMultiplier = globalCfg.landmineSpawnMultiplier.Value;
			turretSpawnMultiplier = globalCfg.turretSpawnMultiplier.Value;
			spikeTrapSpawnMultiplier = globalCfg.spikeTrapSpawnMultiplier.Value;

			configFile.Save();
		}

		#endregion

#if UnityExplorerScript
		#region Script_For_Unity_Explorer

		/* Code for listing spawnableMapObjects of each level in Unity Explorer: */

		// Dummy function for no error
		static void Log(System.Object obj) { }

		static void LoggingForUnityExplorer()
		{
			SelectableLevel level = RoundManager.Instance.currentLevel;
			Log("Level Name: " + level.name);

			SpawnableMapObject[] mapObjects = level.spawnableMapObjects;

			int j = 0;
			while (j < mapObjects.Length)
			{
				SpawnableMapObject mapObject = mapObjects[j];
				Log(" - " + mapObject.prefabToSpawn.name);

				if (true)
				{
					int k = 0;
					while (k < mapObject.numberToSpawn.keys.Length)
					{
						float val = mapObject.numberToSpawn.keys[k].value;
						Log("    - [" + k + "] curr: " + val);

						k++;
					}
				}

				j++;
			}
		}

		#endregion
#endif

		public static TrapSpawnTweaks Instance { get; private set; } = null!;
		internal new static ManualLogSource Logger { get; private set; } = null!;

		#region Patches

		const string landmineHazerdName = "Landmine";
		const string turretHazardName = "TurretContainer";
		const string spikeHazardName = "SpikeRoofTrapHazard";

		//static float numOfSpawnMultiplier = 0.4f;

#if none
		struct OriginalMapObjectSpawnData
		{
			public string levelName;
			public SpawnableMapObject[] mapObjects;

			public OriginalMapObjectSpawnData(string levelName, SpawnableMapObject[] mapObjects)
			{
				this.levelName = levelName;
				this.mapObjects = mapObjects;
			}
		}

		static List<OriginalMapObjectSpawnData> origMapObjectSpawns = new List<OriginalMapObjectSpawnData>();
#endif

		static int clampResolution = 100;

		static Keyframe[] ClampValues(AnimationCurve animationCurve, float multiplier)
		{
			List<Keyframe> newKeys = new List<Keyframe>();

			float startVal = 0f;
			float endVal = 1f;
			int i = 0;
			while (i < clampResolution)
			{
				float currentTime = startVal + (endVal - startVal) * (i / (float)clampResolution);

				float currentValue = animationCurve.Evaluate(currentTime) * multiplier;
				if (currentValue > maxNumberToSpawn)
					currentValue = maxNumberToSpawn;

				Keyframe keyframe = new Keyframe(currentTime, currentValue);
				keyframe.tangentModeInternal = 2;
				newKeys.Add(keyframe);

				i++;
			}

			return newKeys.ToArray();
		}

		static bool mapDataSaved = false;

		static void TweakValues()
		{
			if (!mapDataSaved)
			{
				//ConfigFile configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "TrapSpawnTweaks.cfg"), false);

				SelectableLevel level;

				StartOfRound startOfRound = StartOfRound.Instance;

				int i = 0;
				while (i < startOfRound.levels.Length)
				{
					level = startOfRound.levels[i];

					Logger.LogInfo("Level Name: " + level.name);

					LevelConfig lvlCfg = new LevelConfig();
					//lvlCfg.levelName = level.name;
					lvlCfg.landmineSpawnMultiplier = configFile.Bind(level.name, "Landmine Spawn Multiplier", 1f);
					lvlCfg.turretSpawnMultiplier = configFile.Bind(level.name, "Turret Spawn Multiplier", 1f);
					lvlCfg.spikeTrapSpawnMultiplier = configFile.Bind(level.name, "Spike Trap Spawn Multiplier", 1f);

					Logger.LogInfo(" - Original Values:");
					int j = 0;
					while (j < level.spawnableMapObjects.Length)
					{
						SpawnableMapObject spawnableMapObject = level.spawnableMapObjects[j];
						Logger.LogInfo("    - " + spawnableMapObject.prefabToSpawn.name);

						Keyframe[] currKeys = spawnableMapObject.numberToSpawn.keys;

						int k = 0;
						while (k < currKeys.Length)
						{
							Logger.LogInfo("       - [" + k + "]:" + currKeys[k].value);
							k++;
						}

						float multiplier = 1.0f;
						switch (spawnableMapObject.prefabToSpawn.name)
						{
							case landmineHazerdName:
								multiplier = lvlCfg.landmineSpawnMultiplier.Value * landmineSpawnMultiplier;
								break;
							case turretHazardName:
								multiplier = lvlCfg.turretSpawnMultiplier.Value * turretSpawnMultiplier;
								break;
							case spikeHazardName:
								multiplier = lvlCfg.spikeTrapSpawnMultiplier.Value * spikeTrapSpawnMultiplier;
								break;
						}

						//if (level.name == "EmbrionLevel" && spawnableMapObject.prefabToSpawn.name == spikeHazardName)
						if (true)
						{
							if (!limitNumberToSpawn)
							{
								Keyframe[] newKeys = new Keyframe[currKeys.Length];

								k = 0;
								while (k < currKeys.Length)
								{
									//float newValue = currKeys[k].value * numOfSpawnMultiplier;

									Keyframe origKey = currKeys[k];
									Keyframe newKey = new Keyframe();
									newKey.value = origKey.value * multiplier;
									newKey.time = origKey.time;
									newKey.inTangent = origKey.inTangent;
									newKey.outTangent = origKey.outTangent;
									newKey.inWeight = origKey.inWeight;
									newKey.outWeight = origKey.outWeight;
									newKey.weightedMode = origKey.weightedMode;

									newKeys[k] = newKey;

									k++;
								}

								spawnableMapObject.numberToSpawn.keys = newKeys;
							}
							else
							{
								spawnableMapObject.numberToSpawn.keys = ClampValues(spawnableMapObject.numberToSpawn, multiplier);
							}
						}

						j++;
					}

					// List new values

					Logger.LogInfo(" - New Values:");

					bool secondLargerThanMax = false;
					j = 0;
					while (j < level.spawnableMapObjects.Length)
					{
						SpawnableMapObject spawnableMapObject = level.spawnableMapObjects[j];
						Logger.LogInfo("    - " + spawnableMapObject.prefabToSpawn.name);

						int k = 0;
						while (k < spawnableMapObject.numberToSpawn.keys.Length)
						{
							float newValue = spawnableMapObject.numberToSpawn.keys[k].value;

							if (newValue < maxNumberToSpawn)
								secondLargerThanMax = false;

							if (!secondLargerThanMax)
								Logger.LogInfo("       - [" + k + "]:" + newValue);

							if (newValue >= maxNumberToSpawn)
								secondLargerThanMax = true;

							k++;
						}

						j++;
					}

					i++;
				}

				mapDataSaved = true;

				configFile.Save();

				Logger.LogInfo("Map object spawning info has been updated.");
			}
			else
			{
				Logger.LogInfo("Map object spawning info was updated, no need to update again.");
			}
		}

		static void Hook_StartOfRound_Awake(On.StartOfRound.orig_Awake orig, StartOfRound self)
		{
			orig.Invoke(self);

			TweakValues();
		}

		static void Hook_RoundManager_SpawnMapObjects(On.RoundManager.orig_SpawnMapObjects orig, RoundManager self)
		{
			TweakValues();

			SelectableLevel level = self.currentLevel;

			if (level != null)
			{
				// List current values
				Logger.LogInfo("Level Name: " + level.name);
				Logger.LogInfo(" - Current Values:");

				SpawnableMapObject[] mapObjects = level.spawnableMapObjects;

				int j = 0;
				while (j < mapObjects.Length)
				{
					SpawnableMapObject mapObject = mapObjects[j];
					Logger.LogInfo("    - " + mapObject.prefabToSpawn.name);

					if (true)
					{
						bool secondLargerThanMax = false;
						int k = 0;
						while (k < mapObject.numberToSpawn.keys.Length)
						{
							float val = mapObject.numberToSpawn.keys[k].value;

							if (val < maxNumberToSpawn)
								secondLargerThanMax = false;

							if (!secondLargerThanMax)
								Logger.LogInfo("       - [" + k + "]:" + val);

							if (val >= maxNumberToSpawn)
								secondLargerThanMax = true;

							k++;
						}
					}

					j++;
				}
			}

			orig.Invoke(self);
		}

		#endregion

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			LoadGlobalConfig();

			Hook();

			Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
		}

		internal static void Hook()
		{
			Logger.LogDebug("Hooking...");

			/*
	         *  Subscribe with 'On.Class.Method += CustomClass.CustomMethod;' for each method you're patching.
	         */

			On.RoundManager.SpawnMapObjects += Hook_RoundManager_SpawnMapObjects;

			Logger.LogDebug("Finished Hooking!");
		}

		internal static void Unhook()
		{
			Logger.LogDebug("Unhooking...");

			/*
	         *  Unsubscribe with 'On.Class.Method -= CustomClass.CustomMethod;' for each method you're patching.
	         */

			On.RoundManager.SpawnMapObjects -= Hook_RoundManager_SpawnMapObjects;

			Logger.LogDebug("Finished Unhooking!");
		}
	}
}
