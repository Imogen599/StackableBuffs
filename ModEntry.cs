using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using System.Reflection;

namespace StackableBuffs
{
	// Note that, due to how this works, you can apply a buff to the max stacks, let it get down to almost 0, then reapply it to jump
	// back up to the extended 4 stacks timer. I don't see this has a major issue though, as the days in stardew aren't terribly long.
	// TODO: Fix at some point in the future by jumping into the buff update code, and keeping track of whereabouts in what extension
	// timer ranges the buff is, and updating the stored extension to match that. Not urgent.
	public sealed class ModEntry : Mod
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

			// Initialize and store the default length for each buff. This should not need to be per player, however it was tested in multiplayer
			// with this, so safe over sorry.
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

			// Initialize and store  the amount of extensions on the current buff.
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

			// Store whether the buff was already applied. Could also be done by passing a state to the postfix?
			BuffWasAlreadyApplied[player.UniqueMultiplayerID] = __instance.IsApplied(buff.id);
		}

		private static void Apply_PostfixPatch(BuffManager __instance, Buff buff)
		{
			var player = (Farmer)BuffManager_Player.GetValue(__instance);

			// Only try and add an extension if the buff was already applied.
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
			// Reset the buff extension count if this buff was not already applied.
			else
			{
				BuffLengthExtensions[player.UniqueMultiplayerID][buff.id] = 1;
			}
		}
	}
}
