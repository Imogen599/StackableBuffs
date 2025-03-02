using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using System.Reflection;

namespace StackableBuffs
{
	public class ModEntry : Mod
	{
		private static Harmony HarmonyInstance;

		private static readonly Dictionary<long, bool> BuffWasAlreadyApplied = new();

		private static readonly Dictionary<long, Dictionary<string, int>> DefaultBuffLengths = new();

		private static readonly Dictionary<long, Dictionary<string, int>> BuffLengthExtensions = new();

		private static readonly FieldInfo BuffManager_Player = typeof(BuffManager).GetField("Player", BindingFlags.Instance | BindingFlags.NonPublic);

		public override void Entry(IModHelper helper)
		{
			HarmonyInstance = new Harmony(ModManifest.UniqueID);

			var buffMethod = AccessTools.Method(typeof(BuffManager), "Apply");
			var buffPrefixPatchMethod = new HarmonyMethod(typeof(ModEntry), nameof(Apply_PrefixPatch));
			var buffPostfixPatchMethod = new HarmonyMethod(typeof(ModEntry), nameof(Apply_PostfixPatch));

			try
			{
				HarmonyInstance.Patch(buffMethod, buffPrefixPatchMethod, buffPostfixPatchMethod);
				Monitor.Log($"Correctly applied patch for 'BuffManager.Apply(Buff buff)'!", LogLevel.Info);
			}
			catch (Exception ex)
			{
				Monitor.Log($"Failed to apply patch for 'BuffManager.Apply(Buff buff)'!:\n{ex}", LogLevel.Error);
			}
		}

		private static void Apply_PrefixPatch(BuffManager __instance, Buff buff)
		{
			var player = (Farmer)BuffManager_Player.GetValue(__instance);
			if (!DefaultBuffLengths.ContainsKey(player.UniqueMultiplayerID))
			{
				DefaultBuffLengths.Add(player.UniqueMultiplayerID, new Dictionary<string, int>
				{
					{ buff.id, buff.totalMillisecondsDuration }
				});
			}
			else if(!DefaultBuffLengths[player.UniqueMultiplayerID].ContainsKey(buff.id))
			{
				DefaultBuffLengths[player.UniqueMultiplayerID][buff.id] = buff.totalMillisecondsDuration;
			}

			if (!BuffLengthExtensions.ContainsKey(player.UniqueMultiplayerID))
			{
				BuffLengthExtensions.Add(player.UniqueMultiplayerID, new Dictionary<string, int>
				{
					{ buff.id, 1 }
				});
			}
			else if(!BuffLengthExtensions[player.UniqueMultiplayerID].ContainsKey(buff.id))
			{
				BuffLengthExtensions[player.UniqueMultiplayerID][buff.id] = 1;
			}

			BuffWasAlreadyApplied[player.UniqueMultiplayerID] = __instance.IsApplied(buff.id);
		}

		private static void Apply_PostfixPatch(BuffManager __instance, Buff buff)
		{
			var player = (Farmer)BuffManager_Player.GetValue(__instance);

			if (BuffWasAlreadyApplied[player.UniqueMultiplayerID])
			{
				var defaultLength = DefaultBuffLengths[player.UniqueMultiplayerID][buff.id];

				// Increase the extension count of the currrent buff, capping out at 4 extensions.
				BuffLengthExtensions[player.UniqueMultiplayerID][buff.id] = Math.Clamp(BuffLengthExtensions[player.UniqueMultiplayerID][buff.id] + 1, 0, 4);
				var currentBuffExtension = BuffLengthExtensions[player.UniqueMultiplayerID][buff.id];

				buff.totalMillisecondsDuration = currentBuffExtension * defaultLength;
				buff.millisecondsDuration = currentBuffExtension * defaultLength;

				BuffWasAlreadyApplied[player.UniqueMultiplayerID] = false;
			}
			// Reset the buff extension count if this buff is being applied, not reapplied.
			else
				BuffLengthExtensions[player.UniqueMultiplayerID][buff.id] = 1;
		}
	}
}
