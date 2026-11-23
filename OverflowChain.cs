using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;

namespace OverflowChainMod;

/// <summary>
/// Rare relic. When the owner's attack kills an enemy and the damage overkills,
/// the excess damage chains to the next living enemy (leftmost first). Every
/// hop that itself kills with overkill continues the chain, until the overflow
/// runs out or no enemies remain.
/// </summary>
public sealed class OverflowChain : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != Owner.Creature || !props.IsPoweredAttack() || !result.WasTargetKilled || result.OverkillDamage <= 0)
        {
            return;
        }
        ICombatState? combatState = target.CombatState;
        if (combatState == null)
        {
            return;
        }
        // Walk the chain in a loop instead of recursing through damage hooks:
        // the chain hits are Unpowered, so this hook ignores them and each hop
        // only continues via its own overkill result.
        decimal overflow = result.OverkillDamage;
        Creature from = target;
        bool flashed = false;
        Vector2? prevCenter = OverflowVfx.GetCenter(from);
        while (overflow > 0)
        {
            Creature? next = combatState.HittableEnemies.FirstOrDefault(e => e != from);
            if (next == null)
            {
                break;
            }
            if (!flashed)
            {
                Flash();
                SfxCmd.Play(OverflowVfx.EvokeSfx);
                flashed = true;
            }
            // Custom shock layer (body glow + arcs + hop-to-hop bolts) on top of
            // the vanilla strike. Centers are captured before each hit so a dying
            // monster still anchors its bolt.
            Vector2 nextCenter = OverflowVfx.GetCenter(next) ?? prevCenter ?? Vector2.Zero;
            if (prevCenter.HasValue)
            {
                OverflowVfx.PlayBolt(prevCenter.Value, nextCenter);
            }
            OverflowVfx.PlayHop(next);
            // This overload is shared by the stable and beta game builds.
            VfxCmd.PlayOnCreatures([next], VfxCmd.lightningPath);
            prevCenter = nextCenter;
            var results = await CreatureCmd.Damage(choiceContext, next, overflow, ValueProp.Unpowered | ValueProp.SkipHurtAnim, Owner.Creature);
            DamageResult? nextResult = results.FirstOrDefault(r => r.Receiver == next);
            overflow = (nextResult != null && nextResult.WasTargetKilled) ? nextResult.OverkillDamage : 0;
            from = next;
        }
    }
}
