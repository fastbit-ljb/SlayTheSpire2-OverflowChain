using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Modding;

namespace OverflowChainMod;

/// <summary>
/// Mod entry point. The game's ModelDb.Init() discovers RelicModel subclasses
/// in mod assemblies automatically - just patch the shared relic pool so the
/// relic is obtainable.
/// </summary>
[ModInitializer(initializerMethod: nameof(Initialize))]
public static class ModInit
{
    private static void Initialize()
    {
        Harmony harmony = new Harmony("fastbit-ljb.OverflowChain");
        harmony.PatchAll(typeof(ModInit).Assembly);
    }
}

[HarmonyPatch]
internal static class SharedRelicPoolPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(SharedRelicPool), "GenerateAllRelics")]
    private static void InjectOverflowChain(ref IEnumerable<RelicModel> __result)
    {
        __result = __result.Append(ModelDb.Relic<OverflowChain>());
    }
}
