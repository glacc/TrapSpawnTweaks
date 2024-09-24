using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
using static UnityEngine.Rendering.HighDefinition.ScalableSettingLevelParameter;
using System;
using Unity.Collections.LowLevel.Unsafe;

namespace TrapSpawnTweaks
{
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	public class TrapSpawnTweaks : BaseUnityPlugin
	{
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

		public static TrapSpawnTweaks Instance { get; private set; } = null!;
		internal new static ManualLogSource Logger { get; private set; } = null!;

		#region Patches

		const string LandmineHazerdName = "Landmine";
		const string TurretHazardName = "TurretContainer";
		const string SpikeHazardName = "SpikeRoofTrapHazard";

		static float numOfSpawnMultiplier = 0.3f;

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

		static bool mapDataSaved = false;

		static void TweakValues()
		{
			if (!mapDataSaved)
			{
				SelectableLevel level;

				StartOfRound startOfRound = StartOfRound.Instance;

				/*
				origMapObjectSpawns.Clear();

				int i = 0;
				while (i < self.levels.Length)
				{
					level = self.levels[i];
					Logger.LogInfo("Level Name: " + level.name);

					SpawnableMapObject[] mapObjects = level.spawnableMapObjects;

					origMapObjectSpawns.Add(new OriginalMapObjectSpawnData(level.name, mapObjects));

					i++;
				}
				*/

				int i = 0;
				while (i < startOfRound.levels.Length)
				{
					level = startOfRound.levels[i];

					Logger.LogInfo("Level Name: " + level.name);

					Logger.LogInfo(" - Original Values:");

					int j = 0;
					while (j < level.spawnableMapObjects.Length)
					{
						SpawnableMapObject spawnableMapObject = level.spawnableMapObjects[j];
						Logger.LogInfo("    - " + spawnableMapObject.prefabToSpawn.name);

						if (true)
						{
							Keyframe[] currKeys = spawnableMapObject.numberToSpawn.keys;
							Keyframe[] newKeys = new Keyframe[currKeys.Length];

							int k = 0;
							while (k < currKeys.Length)
							{
								Logger.LogInfo("       - [" + k + "]:" + currKeys[k].value);

								//float newValue = currKeys[k].value * numOfSpawnMultiplier;

								Keyframe origKey = currKeys[k];
								Keyframe newKey = new Keyframe();
								newKey.value = origKey.value * numOfSpawnMultiplier;
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

						j++;
					}

					// List new values

					Logger.LogInfo(" - New Values:");

					j = 0;
					while (j < level.spawnableMapObjects.Length)
					{
						SpawnableMapObject spawnableMapObject = level.spawnableMapObjects[j];
						Logger.LogInfo("    - " + spawnableMapObject.prefabToSpawn.name);

						int k = 0;
						while (k < spawnableMapObject.numberToSpawn.keys.Length)
						{
							Logger.LogInfo("       - [" + k + "]:" + spawnableMapObject.numberToSpawn.keys[k].value);
							k++;
						}

						j++;
					}

					i++;
				}

				mapDataSaved = true;

				Logger.LogInfo("Map object spawning info has been updated.");
			}
			else
			{
				Logger.LogInfo("Map object spawning info was updated, no need to update again.");
			}
		}

		static void Hook_StartOfRound_Awake(On.StartOfRound.orig_Awake orig, StartOfRound self)
		{
			// 执行原版代码，完成后开始修改数据
			orig.Invoke(self);

			TweakValues();
		}

		static void Hook_RoundManager_SpawnMapObjects_Old(On.RoundManager.orig_SpawnMapObjects orig, RoundManager self)
		{
			OriginalMapObjectSpawnData origLvl = new OriginalMapObjectSpawnData();
			SelectableLevel level = self.currentLevel;

			if (level != null)
			{

				bool lvlFound = false;

				int lvlNum = 0;
				while (lvlNum < origMapObjectSpawns.Count)
				{
					if (origMapObjectSpawns[lvlNum].levelName == level.name)
					{
						origLvl = origMapObjectSpawns[lvlNum];
						lvlFound = true;
						break;
					}

					lvlNum++;
				}

				if (lvlFound)
				{
					Logger.LogInfo("Level Name: " + level.name);

					Logger.LogInfo(" - New Values:");

					SpawnableMapObject[] spawnableMapObjects = level.spawnableMapObjects;

					int objNum = 0;
					while (objNum < level.spawnableMapObjects.Length)
					{
						SpawnableMapObject currObj = spawnableMapObjects[objNum];

						Logger.LogInfo("    - " + currObj.prefabToSpawn.name);

						if (origLvl.mapObjects != null)
						{
							SpawnableMapObject origMapObj = origLvl.mapObjects[objNum];

							//if (spawnableMapObjects[objNum].prefabToSpawn.name == SpikeHazardName)
							if (true)
							{
								//Keyframe[] keys = RoundManager.Instance.currentLevel.spawnableMapObjects[objNum].numberToSpawn.keys;
								Keyframe[] newKeys = new Keyframe[origMapObj.numberToSpawn.keys.Length];

								int keyNum = 0;
								while (keyNum < currObj.numberToSpawn.keys.Length)
								{
									float newValue = origMapObj.numberToSpawn.keys[keyNum].value * numOfSpawnMultiplier;

									//RoundManager.Instance.currentLevel.spawnableMapObjects[objNum].numberToSpawn.keys[keyNum].m_Value = newValue;
									Keyframe origKey = origMapObj.numberToSpawn.keys[keyNum];
									Keyframe newKey = new Keyframe();
									newKey.value = newValue;
									newKey.time = origKey.time;
									newKey.inTangent = origKey.inTangent;
									newKey.outTangent = origKey.outTangent;
									newKey.inWeight = origKey.inWeight;
									newKey.outWeight = origKey.outWeight;
									newKey.weightedMode = origKey.weightedMode;

									newKeys[keyNum] = newKey;

									Logger.LogInfo("       - [" + keyNum + "]:" + newValue);

									keyNum++;
								}

								RoundManager.Instance.currentLevel.spawnableMapObjects[objNum].numberToSpawn.keys = newKeys;
							}
						}

						objNum++;
					}
				}

				// List current values

				Logger.LogInfo(" - Current Values:");

				SpawnableMapObject[] mapObjects = level.spawnableMapObjects;

				int j = 0;
				while (j < mapObjects.Length)
				{
					SpawnableMapObject mapObject = mapObjects[j];
					Logger.LogInfo("    - " + mapObject.prefabToSpawn.name);

					if (true)
					{
						int k = 0;
						while (k < mapObject.numberToSpawn.keys.Length)
						{
							float val = mapObject.numberToSpawn.keys[k].value;
							Logger.LogInfo("       - [" + k + "]: " + val);

							k++;
						}
					}

					j++;
				}
			}

			orig.Invoke(self);
		}

		static void Hook_RoundManager_SpawnMapObjects_Listing(On.RoundManager.orig_SpawnMapObjects orig, RoundManager self)
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
						int k = 0;
						while (k < mapObject.numberToSpawn.keys.Length)
						{
							float val = mapObject.numberToSpawn.keys[k].value;
							Logger.LogInfo("       - [" + k + "]: " + val);

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

			Hook();

			Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
		}

		internal static void Hook()
		{
			Logger.LogDebug("Hooking...");

			/*
	         *  Subscribe with 'On.Class.Method += CustomClass.CustomMethod;' for each method you're patching.
	         */

			On.StartOfRound.Awake += Hook_StartOfRound_Awake;
			On.RoundManager.SpawnMapObjects += Hook_RoundManager_SpawnMapObjects_Listing;

			Logger.LogDebug("Finished Hooking!");
		}

		internal static void Unhook()
		{
			Logger.LogDebug("Unhooking...");

			/*
	         *  Unsubscribe with 'On.Class.Method -= CustomClass.CustomMethod;' for each method you're patching.
	         */

			On.StartOfRound.Awake -= Hook_StartOfRound_Awake;
			On.RoundManager.SpawnMapObjects -= Hook_RoundManager_SpawnMapObjects_Listing;

			Logger.LogDebug("Finished Unhooking!");
		}
	}
}
