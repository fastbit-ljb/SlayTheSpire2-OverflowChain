using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Modding;

namespace OverflowChainMod;

/// <summary>
/// Registers the relic in the shared pool during mod initialization.
/// </summary>
[ModInitializer(initializerMethod: nameof(Initialize))]
public static class ModInit
{
    private static void Initialize()
    {
        ModHelper.AddModelToPool<SharedRelicPool, OverflowChain>();
    }
}
