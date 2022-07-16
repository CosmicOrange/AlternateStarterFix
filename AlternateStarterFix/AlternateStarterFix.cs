using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace AlternateStarterFix
{
	public class AlternateStarterFix : ETGModule
	{
		public static readonly string MOD_NAME = "AlternateStarterFix";
		public static readonly string VERSION = "1.0.0";
		public static readonly string TEXT_COLOR = "#00FFFF";

		public override void Init()
		{
			Hook hook = new Hook(
				typeof(GameManager).GetMethod("QuickRestart", BindingFlags.Public | BindingFlags.Instance),
				typeof(AlternateStarterFix).GetMethod("QuickRestartHook", BindingFlags.Public | BindingFlags.Instance),
				this
			);
		}

		public void QuickRestartHook(Action<GameManager, QuickRestartOptions> orig, GameManager self,
			QuickRestartOptions options = default(QuickRestartOptions))
		{
			if ((bool)AlternateStarterFix.m_paused.GetValue(self))
			{
				self.ForceUnpause();
			}

			AlternateStarterFix.m_loadingLevel.SetValue(self, true);
			ChallengeManager componentInChildren = self.GetComponentInChildren<ChallengeManager>();

			if (componentInChildren)
			{
				UnityEngine.Object.Destroy(componentInChildren.gameObject);
			}

			SaveManager.DeleteCurrentSlotMidGameSave(null);

			if (options.BossRush)
			{
				self.CurrentGameMode = GameManager.GameMode.BOSSRUSH;
			}
			else if (self.CurrentGameMode == GameManager.GameMode.BOSSRUSH)
			{
				self.CurrentGameMode = GameManager.GameMode.NORMAL;
			}

			bool flag = GameManager.Instance.CurrentGameMode == GameManager.GameMode.SHORTCUT;

			if (self.PrimaryPlayer != null)
			{
				GameManager.ForceQuickRestartAlternateGunP1 = false;
				self.PrimaryPlayer.UsingAlternateStartingGuns = false;

				for (int i = 0; i < self.PrimaryPlayer.inventory.GunCountModified; i++)
				{
					int gunId = self.PrimaryPlayer.inventory.AllGuns[i].PickupObjectId;

					if (self.PrimaryPlayer.startingAlternateGunIds.Contains(gunId) && gunId != 12 && gunId != 202)
					{
						Log($"Alternate starter gun found, ID: " + gunId);
						GameManager.ForceQuickRestartAlternateGunP1 = true;
						self.PrimaryPlayer.UsingAlternateStartingGuns = true;
						break;
					}
				}
				GameManager.ForceQuickRestartAlternateCostumeP1 = self.PrimaryPlayer.IsUsingAlternateCostume;
			}

			if (self.CurrentGameType == GameManager.GameType.COOP_2_PLAYER && self.SecondaryPlayer != null)
			{
				GameManager.ForceQuickRestartAlternateGunP2 = false;
				self.SecondaryPlayer.UsingAlternateStartingGuns = false;

				for (int i = 0; i < self.SecondaryPlayer.inventory.GunCountModified; i++)
				{
					int gunId = self.SecondaryPlayer.inventory.AllGuns[i].PickupObjectId;

					if (self.SecondaryPlayer.startingAlternateGunIds.Contains(gunId) && gunId != 12 && gunId != 202)
					{
						Log($"Alternate starter gun found, ID: " + gunId, TEXT_COLOR);
						GameManager.ForceQuickRestartAlternateGunP2 = true;
						self.SecondaryPlayer.UsingAlternateStartingGuns = true;
						break;
					}
				}
				GameManager.ForceQuickRestartAlternateCostumeP2 = self.SecondaryPlayer.IsUsingAlternateCostume;
			}

			self.ClearPerLevelData();
			self.FlushAudio();
			self.ClearActiveGameData(false, true);
			if (self.TargetQuickRestartLevel != -1)
			{
				self.SetNextLevelIndex(self.TargetQuickRestartLevel);
			}
			else
			{
				self.SetNextLevelIndex(1);

				if (flag)
				{
					self.SetNextLevelIndex(self.LastShortcutFloorLoaded + 1);
					self.IsLoadingFirstShortcutFloor = true;

				}
			}

			if (GameManager.LastUsedPlayerPrefab != null)
			{
				GameManager.PlayerPrefabForNewGame = GameManager.LastUsedPlayerPrefab;
				PlayerController component = GameManager.PlayerPrefabForNewGame.GetComponent<PlayerController>();
				GameStatsManager.Instance.BeginNewSession(component);
			}

			if (self.CurrentGameType == GameManager.GameType.COOP_2_PLAYER && GameManager.LastUsedCoopPlayerPrefab != null)
			{
				GameManager.CoopPlayerPrefabForNewGame = GameManager.LastUsedCoopPlayerPrefab;
			}

			UnityEngine.Object.DontDestroyOnLoad(self.gameObject);
			AlternateStarterFix.m_preventUnpausing.SetValue(self, false);

			if ((int)AlternateStarterFix.m_currentRunSeed.GetValue(self) != 0)
			{
				AlternateStarterFix.m_forceSeedUpdate.SetValue(self, true);
				self.CurrentRunSeed = self.CurrentRunSeed;
			}

			UnityEngine.Debug.Log("Quick Restarting...");
			if (self.CurrentGameMode == GameManager.GameMode.BOSSRUSH)
			{
				self.SetNextLevelIndex(1);
				AlternateStarterFix.InstantLoadBossRushFloor.Invoke(self, new object[] { 1 });
				self.SetNextLevelIndex(2);

			}
			else
			{
				GameManager.Instance.LoadNextLevel();
			}
			self.StartCoroutine(
				(System.Collections.IEnumerator)AlternateStarterFix.PostQuickStartCR.Invoke(self, new object[]
				{ options }));
		}

		private static FieldInfo m_paused =
			typeof(GameManager).GetField("m_paused", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo m_loadingLevel =
			typeof(GameManager).GetField("m_loadingLevel", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo m_preventUnpausing =
			typeof(GameManager).GetField("m_preventUnpausing", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo m_currentRunSeed =
			typeof(GameManager).GetField("m_currentRunSeed", BindingFlags.Instance | BindingFlags.NonPublic);

		private static FieldInfo m_forceSeedUpdate =
			typeof(GameManager).GetField("m_forceSeedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

		private static MethodInfo InstantLoadBossRushFloor =
			typeof(GameManager).GetMethod("InstantLoadBossRushFloor", BindingFlags.Instance | BindingFlags.NonPublic);

		private static MethodInfo PostQuickStartCR =
			typeof(GameManager).GetMethod("PostQuickStartCR", BindingFlags.Instance | BindingFlags.NonPublic);

		public override void Start()
		{
			Log($"{MOD_NAME} v{VERSION} started successfully.", TEXT_COLOR);
		}
		public override void Exit()
		{

		}

		public static void Log(string text, string color = "#FFFFFF")
		{
			ETGModConsole.Log($"<color={color}>{text}</color>");
		}
	}
}