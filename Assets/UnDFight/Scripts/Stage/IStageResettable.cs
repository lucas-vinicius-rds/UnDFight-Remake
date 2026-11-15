using UnityEngine;

namespace UnDFight
{
    public enum StagePropMaterialType
    {
        WoodOrCrate = 0,
        Plastic = 1,
        Glass = 2,
        ConcreteOrMetal = 3
    }

    /// <summary>
    /// Contract for any interactive stage object that resets cleanly at the start of every round.
    /// </summary>
    public interface IStageResettable
    {
        void ResetProp();
    }

    /// <summary>
    /// Shared base component for stage props discovered automatically by StageInteractableManager.
    /// </summary>
    public abstract class StagePropBase : MonoBehaviour, IStageResettable
    {
        public abstract bool IsBroken { get; }

        public abstract void ResetProp();

        public abstract bool TryHandleCombatImpact(
            Vector2 contactPoint,
            int impactDamage,
            float pushDirX,
            bool isHeavyOrSpecial,
            Vector2 attackerPos,
            Vector2 defenderPos,
            int simulationTick = -1);

        public virtual bool TryHandleKnockdownImpact(Vector2 fighterPosition, float pushDirX, int simulationTick = -1)
        {
            return TryHandleCombatImpact(fighterPosition, 2, pushDirX, true, fighterPosition, fighterPosition, simulationTick);
        }
    }
}
