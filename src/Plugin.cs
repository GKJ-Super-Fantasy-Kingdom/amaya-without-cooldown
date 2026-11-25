using BepInEx;
using HarmonyLib;

namespace AmayaWithoutCooldown
{
    [BepInPlugin(Id, "Amaya Without Cooldown", "0.1.3")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private const string Id = "gkj.sfk.amayawithoutcooldown";
        private Harmony patches;

        private void Awake()
        {
            patches = new Harmony(Id);
            try
            {
                patches.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("Amaya Without Cooldown is ready.");
            }
            catch
            {
                patches.UnpatchSelf();
                throw;
            }
        }

        private void OnDestroy() => patches?.UnpatchSelf();
    }
}
