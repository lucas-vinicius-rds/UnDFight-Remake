using System;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// A single step in an MK-style Dial-a-Combo string sequence.
    /// </summary>
    [Serializable]
    public struct ComboStringStep
    {
        public FighterInputButton inputButton;
        public bool requireCrouch;
        public AttackType attackType;
        public MoveData stepMoveOverride;
    }

    /// <summary>
    /// Named 3+ hit combo string definition supporting Dial-a-Combo input queueing,
    /// blockstrings on guard, per-step MoveData overrides (Overhead / Low mix-ups),
    /// and Special/EX finisher cancels.
    /// </summary>
    [Serializable]
    public class ComboStringDefinition
    {
        public string stringName = "COMBO";
        public ComboStringStep[] steps = Array.Empty<ComboStringStep>();
        public bool allowSpecialOrExCancelOnEnd = true;
    }
}
