using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Discreet footer debug strip for the Training scene showing 60 Hz tick, render FPS,
    /// fighter states/HP/positions, and F1 debug hitbox/hurtbox toggle status.
    /// </summary>
    public class TrainingDebugHUD : MonoBehaviour
    {
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private TextMeshProUGUI debugText;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps;
        private bool _debugBoxesOn;

        public void Configure(FightSimulation sim, TextMeshProUGUI text)
        {
            simulation = sim;
            debugText = text;
        }

        public void SetDebugBoxesIndicator(bool enabled)
        {
            _debugBoxesOn = enabled;
        }

        private void Update()
        {
            _fpsFrames++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.25f)
            {
                _currentFps = _fpsFrames / _fpsTimer;
                _fpsFrames = 0;
                _fpsTimer = 0f;
            }

            if (debugText == null || simulation == null)
            {
                return;
            }

            FighterController p1 = simulation.Fighter1;
            FighterController p2 = simulation.Fighter2;
            MatchManager match = simulation.MatchManager;

            string timerStr = match != null && match.IsTrainingMode ? "\u221E" : (match != null ? match.RemainingSeconds.ToString("00") : "--");
            string matchLabel = match != null
                ? $"R{match.CurrentRound} [{(match.IsTrainingMode ? "Training" : match.Phase.ToString())}] ({match.P1RoundsWon}-{match.P2RoundsWon}) | T={timerStr}"
                : "Free";

            string p1Info = FormatFighterLine("P1", p1);
            string p2Info = FormatFighterLine("P2", p2);
            string boxes = _debugBoxesOn ? "<color=#55FF66>ON</color>" : "OFF";

            debugText.text =
                $"<b>Sim 60Hz</b> #{simulation.CurrentTick} | FPS {_currentFps:F0} | {matchLabel} | Hitstop={simulation.HitstopTicksRemaining} | [F1] Boxes: {boxes}\n" +
                $"{p1Info}   ||   {p2Info}   |   <color=#B8C2CC>P1: WASD+JKUI/L | P2: Arrows+Num4512/6</color>";
        }

        private static string FormatFighterLine(string label, FighterController f)
        {
            if (f == null)
            {
                return $"{label}: <none>";
            }

            string name = f.Data != null ? f.Data.fighterName : f.name;
            string atk = f.State == FighterState.Attack ? $"({f.CurrentAttack})" : string.Empty;
            return $"{label} {name}: HP={f.CurrentHealth}/{f.MaxHealth} <b>{f.State}{atk}</b>(t={f.StateTicks}) X={f.SimPosition.x:F2}";
        }
    }
}
