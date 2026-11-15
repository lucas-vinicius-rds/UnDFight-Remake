using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Debug HUD for the Training scene showing simulation tick (60 Hz), render FPS,
    /// and each fighter's state, X/Y position, and active input device.
    /// </summary>
    public class TrainingDebugHUD : MonoBehaviour
    {
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private TextMeshProUGUI debugText;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps;

        public void Configure(FightSimulation sim, TextMeshProUGUI text)
        {
            simulation = sim;
            debugText = text;
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

            string p1Info = FormatFighterLine("P1", p1);
            string p2Info = FormatFighterLine("P2", p2);

            debugText.text =
                $"<b>UnDFight Training Debug</b> | Sim: 60 Hz | Tick: {simulation.CurrentTick} | Render FPS: {_currentFps:F1}\n" +
                $"{p1Info}\n" +
                $"{p2Info}\n" +
                $"<size=80%>P1: WASD + J/K/U/I/L | P2: Setas + Num4/5/1/2/6 | Gamepad Hot-Plug Ativo</size>";
        }

        private static string FormatFighterLine(string label, FighterController f)
        {
            if (f == null)
            {
                return $"{label}: <none>";
            }

            string name = f.Data != null ? f.Data.fighterName : f.name;
            string dev = f.InputHandler != null ? f.InputHandler.ActiveDeviceLabel : "-";
            string atk = f.State == FighterState.Attack ? $" ({f.CurrentAttack})" : string.Empty;
            return $"{label} [{name}]: State=<b>{f.State}{atk}</b> (t={f.StateTicks}) | X={f.SimPosition.x:F2} Y={f.SimPosition.y:F2} | Dir={(f.FacingSign > 0 ? "R" : "L")} | {dev}";
        }
    }
}
