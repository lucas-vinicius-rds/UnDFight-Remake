using TMPro;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Top-anchored 2.5D fighting game HUD displaying P1 & P2 health bars with delayed damage trail,
    /// round win pips, central 99s timer, match/round announcement banner, and short Hit/Block feedback.
    /// </summary>
    public class FightHUD : MonoBehaviour
    {
        [Header("Simulation & Match")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;

        [Header("P1 Widgets (Left)")]
        [SerializeField] private UnityEngine.UI.Image p1HealthFill;
        [SerializeField] private UnityEngine.UI.Image p1DamageTrailFill;
        [SerializeField] private TextMeshProUGUI p1NameText;
        [SerializeField] private UnityEngine.UI.Image[] p1RoundPips;
        [SerializeField] private TextMeshProUGUI p1CalloutText;

        [Header("P2 Widgets (Right)")]
        [SerializeField] private UnityEngine.UI.Image p2HealthFill;
        [SerializeField] private UnityEngine.UI.Image p2DamageTrailFill;
        [SerializeField] private TextMeshProUGUI p2NameText;
        [SerializeField] private UnityEngine.UI.Image[] p2RoundPips;
        [SerializeField] private TextMeshProUGUI p2CalloutText;

        [Header("Center Timer & Banner")]
        [SerializeField] private TextMeshProUGUI timerText;
        [SerializeField] private GameObject bannerContainer;
        [SerializeField] private TextMeshProUGUI bannerText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("Visual Styling")]
        [SerializeField] private Color healthyColor = new Color(0.18f, 0.85f, 0.34f, 1f);
        [SerializeField] private Color lowHealthColor = new Color(0.95f, 0.25f, 0.18f, 1f);
        [SerializeField] private Color roundWonColor = new Color(1.0f, 0.78f, 0.16f, 1f);
        [SerializeField] private Color roundEmptyColor = new Color(0.20f, 0.23f, 0.29f, 0.9f);
        [SerializeField] private float trailHoldSeconds = 0.28f;
        [SerializeField] private float trailDecaySpeed = 0.75f;
        [SerializeField] private float calloutDurationSeconds = 0.65f;

        private float _p1TrailRatio = 1f;
        private float _p2TrailRatio = 1f;
        private float _p1TrailHoldTimer;
        private float _p2TrailHoldTimer;
        private float _p1PrevRatio = 1f;
        private float _p2PrevRatio = 1f;

        private float _p1CalloutTimer;
        private float _p2CalloutTimer;

        public void Configure(
            FightSimulation sim,
            MatchManager manager,
            UnityEngine.UI.Image p1Fill,
            UnityEngine.UI.Image p1Trail,
            TextMeshProUGUI p1Name,
            UnityEngine.UI.Image[] p1Pips,
            TextMeshProUGUI p1Callout,
            UnityEngine.UI.Image p2Fill,
            UnityEngine.UI.Image p2Trail,
            TextMeshProUGUI p2Name,
            UnityEngine.UI.Image[] p2Pips,
            TextMeshProUGUI p2Callout,
            TextMeshProUGUI timer,
            GameObject bannerObj,
            TextMeshProUGUI banner,
            TextMeshProUGUI subtitle)
        {
            if (simulation != null) simulation.OnCombatHit -= HandleCombatHit;

            simulation = sim;
            matchManager = manager;
            p1HealthFill = p1Fill;
            p1DamageTrailFill = p1Trail;
            p1NameText = p1Name;
            p1RoundPips = p1Pips;
            p1CalloutText = p1Callout;
            p2HealthFill = p2Fill;
            p2DamageTrailFill = p2Trail;
            p2NameText = p2Name;
            p2RoundPips = p2Pips;
            p2CalloutText = p2Callout;
            timerText = timer;
            bannerContainer = bannerObj;
            bannerText = banner;
            subtitleText = subtitle;

            if (simulation != null && isActiveAndEnabled)
            {
                simulation.OnCombatHit += HandleCombatHit;
            }
            RefreshImmediate();
        }

        private void Awake()
        {
            if (simulation == null) simulation = FindAnyObjectByType<FightSimulation>();
            if (matchManager == null) matchManager = FindAnyObjectByType<MatchManager>();
        }

        private void OnEnable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnCombatHit += HandleCombatHit;
            }
            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateFighterBars(dt);
            UpdateRoundAndTimerUI();
            UpdateCallouts(dt);
        }

        public void RefreshImmediate()
        {
            _p1PrevRatio = GetHealthRatio(simulation != null ? simulation.Fighter1 : null);
            _p2PrevRatio = GetHealthRatio(simulation != null ? simulation.Fighter2 : null);
            _p1TrailRatio = _p1PrevRatio;
            _p2TrailRatio = _p2PrevRatio;
            _p1TrailHoldTimer = 0f;
            _p2TrailHoldTimer = 0f;

            UpdateFighterNames();
            ApplyHealthWidgets(p1HealthFill, p1DamageTrailFill, _p1PrevRatio, _p1TrailRatio);
            ApplyHealthWidgets(p2HealthFill, p2DamageTrailFill, _p2PrevRatio, _p2TrailRatio);
            UpdateRoundAndTimerUI();

            if (p1CalloutText != null) p1CalloutText.text = string.Empty;
            if (p2CalloutText != null) p2CalloutText.text = string.Empty;
        }

        private void HandleCombatHit(CombatHitEvent hit)
        {
            if (simulation == null || hit.Defender == null)
            {
                return;
            }

            // Show feedback callout on the defender's side (HIT / BLOCK / K.O.!)
            bool defenderIsP1 = hit.Defender == simulation.Fighter1;
            TextMeshProUGUI targetCallout = defenderIsP1 ? p1CalloutText : p2CalloutText;
            if (targetCallout == null)
            {
                return;
            }

            if (hit.Outcome == HitOutcomeType.Blocked)
            {
                targetCallout.text = "BLOCK!";
                targetCallout.color = new Color(0.35f, 0.85f, 1.0f, 1f);
            }
            else if (hit.Outcome == HitOutcomeType.KnockDown && hit.Defender.CurrentHealth <= 0)
            {
                targetCallout.text = "K.O.!";
                targetCallout.color = new Color(1.0f, 0.25f, 0.20f, 1f);
            }
            else
            {
                targetCallout.text = hit.WasTrade ? "TRADE HIT!" : "HIT!";
                targetCallout.color = new Color(1.0f, 0.78f, 0.16f, 1f);
            }

            if (defenderIsP1) _p1CalloutTimer = calloutDurationSeconds;
            else _p2CalloutTimer = calloutDurationSeconds;
        }

        private void UpdateFighterBars(float dt)
        {
            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;

            UpdateSingleHealthBar(p1, p1HealthFill, p1DamageTrailFill, ref _p1PrevRatio, ref _p1TrailRatio, ref _p1TrailHoldTimer, dt);
            UpdateSingleHealthBar(p2, p2HealthFill, p2DamageTrailFill, ref _p2PrevRatio, ref _p2TrailRatio, ref _p2TrailHoldTimer, dt);
            UpdateFighterNames();
        }

        private void UpdateSingleHealthBar(
            FighterController fighter,
            UnityEngine.UI.Image fill,
            UnityEngine.UI.Image trail,
            ref float prevRatio,
            ref float trailRatio,
            ref float holdTimer,
            float dt)
        {
            float ratio = GetHealthRatio(fighter);

            // Reset trail cleanly when round restores full health
            if (ratio > trailRatio)
            {
                trailRatio = ratio;
                holdTimer = 0f;
            }
            else if (ratio < prevRatio - 0.0001f)
            {
                holdTimer = trailHoldSeconds;
            }

            prevRatio = ratio;

            if (holdTimer > 0f)
            {
                holdTimer -= dt;
            }
            else if (trailRatio > ratio)
            {
                trailRatio = Mathf.MoveTowards(trailRatio, ratio, trailDecaySpeed * dt);
            }

            ApplyHealthWidgets(fill, trail, ratio, trailRatio);
        }

        private void ApplyHealthWidgets(UnityEngine.UI.Image fill, UnityEngine.UI.Image trail, float ratio, float trailRatio)
        {
            if (fill != null)
            {
                fill.fillAmount = ratio;
                fill.color = ratio <= 0.30f ? lowHealthColor : healthyColor;
            }
            if (trail != null)
            {
                trail.fillAmount = trailRatio;
            }
        }

        private void UpdateFighterNames()
        {
            if (simulation == null) return;

            if (p1NameText != null && simulation.Fighter1 != null && simulation.Fighter1.Data != null)
            {
                p1NameText.text = simulation.Fighter1.Data.fighterName.ToUpperInvariant();
            }
            if (p2NameText != null && simulation.Fighter2 != null && simulation.Fighter2.Data != null)
            {
                p2NameText.text = simulation.Fighter2.Data.fighterName.ToUpperInvariant();
            }
        }

        private void UpdateRoundAndTimerUI()
        {
            if (matchManager == null)
            {
                return;
            }

            if (timerText != null)
            {
                if (matchManager.IsTrainingMode)
                {
                    timerText.text = "\u221E";
                    timerText.color = new Color(1.0f, 0.94f, 0.72f, 1f);
                }
                else
                {
                    int sec = matchManager.RemainingSeconds;
                    timerText.text = sec.ToString("00");
                    timerText.color = sec <= 15
                        ? new Color(1.0f, 0.32f, 0.25f, 1f)
                        : new Color(1.0f, 0.94f, 0.72f, 1f);
                }
            }

            UpdatePips(p1RoundPips, matchManager.P1RoundsWon);
            UpdatePips(p2RoundPips, matchManager.P2RoundsWon);

            bool hasBanner = !string.IsNullOrEmpty(matchManager.BannerText);
            if (bannerContainer != null && bannerContainer.activeSelf != hasBanner)
            {
                bannerContainer.SetActive(hasBanner);
            }
            if (bannerText != null)
            {
                bannerText.text = matchManager.BannerText;
            }
            if (subtitleText != null)
            {
                subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(matchManager.SubtitleText));
                subtitleText.text = matchManager.SubtitleText;
            }
        }

        private void UpdatePips(UnityEngine.UI.Image[] pips, int roundsWon)
        {
            if (pips == null) return;
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) continue;
                pips[i].color = i < roundsWon ? roundWonColor : roundEmptyColor;
            }
        }

        private void UpdateCallouts(float dt)
        {
            UpdateSingleCallout(p1CalloutText, ref _p1CalloutTimer, dt);
            UpdateSingleCallout(p2CalloutText, ref _p2CalloutTimer, dt);
        }

        private void UpdateSingleCallout(TextMeshProUGUI callout, ref float timer, float dt)
        {
            if (callout == null) return;

            if (timer > 0f)
            {
                timer -= dt;
                float alpha = Mathf.Clamp01(timer / (calloutDurationSeconds * 0.35f));
                Color c = callout.color;
                callout.color = new Color(c.r, c.g, c.b, alpha);
                if (timer <= 0f)
                {
                    callout.text = string.Empty;
                }
            }
        }

        private static float GetHealthRatio(FighterController fighter)
        {
            if (fighter == null || fighter.MaxHealth <= 0)
            {
                return 1f;
            }
            return Mathf.Clamp01(fighter.CurrentHealth / (float)fighter.MaxHealth);
        }
    }
}
