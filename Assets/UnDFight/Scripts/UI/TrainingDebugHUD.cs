using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Discreet footer debug strip showing 60 Hz tick, render FPS, fighter states/HP/positions,
    /// and F1 hitbox/hurtbox toggle status.
    /// Hidden by default in normal combat modes (LocalVersus, VersusCpu, ArcadeTower) for a clean MKX screen,
    /// and automatically shown only in Training mode or when the player explicitly toggles [F1] hitboxes.
    /// </summary>
    public class TrainingDebugHUD : MonoBehaviour
    {
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private TextMeshProUGUI debugText;
        [SerializeField] private GameObject panelRoot;

        private float _fpsTimer;
        private int _fpsFrames;
        private float _currentFps;
        private bool _debugBoxesOn;

        public bool IsPanelVisible
        {
            get
            {
                GameObject target = ResolvePanelRoot();
                return target != null && target.activeSelf;
            }
        }

        public bool DebugBoxesOn => _debugBoxesOn;

        private void Awake()
        {
            if (simulation == null) simulation = FindAnyObjectByType<FightSimulation>();
            ResolvePanelRoot();
            RefreshVisibility();
        }

        private void OnEnable()
        {
            RefreshVisibility();
        }

        public void Configure(FightSimulation sim, TextMeshProUGUI text, GameObject panel = null)
        {
            simulation = sim;
            debugText = text;
            if (panel != null)
            {
                panelRoot = panel;
            }
            else
            {
                ResolvePanelRoot();
            }
            RefreshVisibility();
        }

        public void SetDebugBoxesIndicator(bool enabled)
        {
            _debugBoxesOn = enabled;
            RefreshVisibility();
        }

        public bool ShouldDisplayDebugStrip()
        {
            if (_debugBoxesOn)
            {
                return true;
            }

            MatchManager match = simulation != null ? simulation.MatchManager : null;
            if (match != null)
            {
                return match.IsTrainingMode;
            }

            return GameSessionState.IsTrainingMode;
        }

        public void RefreshVisibility()
        {
            GameObject target = ResolvePanelRoot();
            bool visible = ShouldDisplayDebugStrip();
            if (target != null && target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }

        private GameObject ResolvePanelRoot()
        {
            if (panelRoot != null)
            {
                return panelRoot;
            }

            Transform found = transform.Find("DebugPanel");
            if (found != null)
            {
                panelRoot = found.gameObject;
                return panelRoot;
            }

            if (debugText != null)
            {
                Transform parent = debugText.transform.parent;
                panelRoot = (parent != null && parent != transform) ? parent.gameObject : debugText.gameObject;
            }

            return panelRoot;
        }

        private void Update()
        {
            RefreshVisibility();
            if (!IsPanelVisible)
            {
                return;
            }

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
            string boxes = _debugBoxesOn ? "<color=#86C5A8>ON</color>" : "OFF";

            CpuAiController cpu = simulation.CpuAi;
            string cpuTag = string.Empty;
            if (match != null && match.IsTrainingMode)
            {
                bool cpuActive = cpu != null && cpu.IsEnabled;
                cpuTag = cpuActive
                    ? $" | [F2] CPU: <color=#86C5A8>ATIVA ({GameSessionState.GetCpuDifficultyLabel(cpu.Difficulty)})</color>"
                    : " | [F2] CPU: DUMMY";
            }
            else if (GameSessionState.CurrentMode == GameMode.VersusCpu)
            {
                cpuTag = $" | CPU: <color=#8BC9EE>{GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}</color>";
            }
            else if (GameSessionState.CurrentMode == GameMode.ArcadeTower)
            {
                ArcadeTowerRun run = GameSessionState.ActiveTowerRun;
                string step = run != null ? $"{run.CurrentMatchIndex + 1}/{run.TotalMatches}" : "?/7";
                cpuTag = $" | TORRE {step} | CPU: <color=#8BC9EE>{GameSessionState.GetCpuDifficultyLabel(GameSessionState.SelectedCpuDifficulty)}</color>";
            }

            debugText.text =
                $"<b>Sim 60Hz</b> #{simulation.CurrentTick} | FPS {_currentFps:F0} | {matchLabel} | Hitstop={simulation.HitstopTicksRemaining} | [F1] Boxes: {boxes}{cpuTag}\n" +
                $"{p1Info}   ||   {p2Info}   |   <color=#8594A8>P1: WASD+JKUI/L | P2: Arrows+Num4512/6</color>";
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
