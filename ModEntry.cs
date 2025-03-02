using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;

namespace StackableBuffs
{
	public class ModEntry : Mod
	{
		private static Harmony HarmonyInstance;

		private static bool BuffWasAlreadyApplied;

		private static readonly Dictionary<string, int> DefaultBuffLengths = new();

		private static readonly Dictionary<string, int> BuffLengthExtensions = new();

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
			if (!DefaultBuffLengths.ContainsKey(buff.id))
				DefaultBuffLengths[buff.id] = buff.totalMillisecondsDuration;

			if (!BuffLengthExtensions.ContainsKey(buff.id))
				BuffLengthExtensions[buff.id] = 1;

			BuffWasAlreadyApplied = __instance.IsApplied(buff.id);
		}

		private static void Apply_PostfixPatch(Buff buff)
		{
			if (BuffWasAlreadyApplied)
			{
				var defaultLength = DefaultBuffLengths[buff.id];

				// Increase the extension count of the currrent buff, capping out at 4 extensions.
				BuffLengthExtensions[buff.id] = Math.Clamp(BuffLengthExtensions[buff.id] + 1, 0, 4);
				var currentBuffExtension = BuffLengthExtensions[buff.id];

				buff.totalMillisecondsDuration = currentBuffExtension * defaultLength;
				buff.millisecondsDuration = currentBuffExtension * defaultLength;

				BuffWasAlreadyApplied = false;
			}
			// Reset the buff extension count if this buff is being applied, not reapplied.
			else
				BuffLengthExtensions[buff.id] = 1;
		}
	}
}
