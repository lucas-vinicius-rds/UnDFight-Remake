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
        private sealed class StagePromptWidget
        {
            public RectTransform Root;
            public UnityEngine.UI.Image Border;
            public UnityEngine.UI.Image Background;
            public TextMeshProUGUI Label;
        }

        [Header("Simulation & Match")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;
        [SerializeField] private StageInteractableManager stageInteractableManager;

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

        [Header("Super Gauge & Combo Counter")]
        [SerializeField] private UnityEngine.UI.Image p1SuperFill;
        [SerializeField] private TextMeshProUGUI p1SuperText;
        [SerializeField] private TextMeshProUGUI p1ComboText;
        [SerializeField] private UnityEngine.UI.Image p2SuperFill;
        [SerializeField] private TextMeshProUGUI p2SuperText;
        [SerializeField] private TextMeshProUGUI p2ComboText;

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
        [SerializeField] private Color superChargingColor = new Color(0.20f, 0.72f, 1.0f, 1f);
        [SerializeField] private Color superExReadyColor = new Color(0.18f, 0.88f, 1.0f, 1f);
        [SerializeField] private Color superReadyColorA = new Color(1.0f, 0.78f, 0.16f, 1f);
        [SerializeField] private Color superReadyColorB = new Color(1.0f, 0.94f, 0.58f, 1f);
        [SerializeField] private float trailHoldSeconds = 0.28f;
        [SerializeField] private float trailDecaySpeed = 0.75f;
        [SerializeField] private float calloutDurationSeconds = 0.65f;
        [SerializeField] private float comboDisplayDurationSeconds = 1.45f;

        private float _p1TrailRatio = 1f;
        private float _p2TrailRatio = 1f;
        private float _p1TrailHoldTimer;
        private float _p2TrailHoldTimer;
        private float _p1PrevRatio = 1f;
        private float _p2PrevRatio = 1f;

        private float _p1CalloutTimer;
        private float _p2CalloutTimer;
        private float _p1ComboTimer;
        private float _p2ComboTimer;
        private Sprite _fallbackWhiteSprite;
        private StagePromptWidget _p1StagePrompt;
        private StagePromptWidget _p2StagePrompt;
        private readonly Color _p1PromptColor = new Color(1.0f, 0.78f, 0.16f, 1f);
        private readonly Color _p2PromptColor = new Color(1.0f, 0.36f, 0.24f, 1f);

        public void Configure(FightSimulation sim, MatchManager manager = null)
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }
            simulation = sim;
            matchManager = manager;
            if (simulation != null && isActiveAndEnabled)
            {
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            RefreshImmediate();
        }

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
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }

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
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            RefreshImmediate();
        }

        private void Awake()
        {
            if (simulation == null) simulation = FindAnyObjectByType<FightSimulation>();
            if (matchManager == null) matchManager = FindAnyObjectByType<MatchManager>();
            if (stageInteractableManager == null) stageInteractableManager = FindAnyObjectByType<StageInteractableManager>();
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
        }

        private void OnEnable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                simulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }
            EnsureSuperAndComboWidgets();
            EnsureStagePromptWidgets();
            RefreshImmediate();
        }

        private void OnDisable()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            UpdateFighterBars(dt);
            UpdateRoundAndTimerUI();
            UpdateCallouts(dt);
            UpdateStageInteractionPrompts();
        }

        public bool IsStagePromptVisible(int playerIndex)
        {
            StagePromptWidget widget = playerIndex == 0 ? _p1StagePrompt : _p2StagePrompt;
            return widget != null && widget.Root != null && widget.Root.gameObject.activeSelf;
        }

        public string GetStagePromptText(int playerIndex)
        {
            StagePromptWidget widget = playerIndex == 0 ? _p1StagePrompt : _p2StagePrompt;
            return widget != null && widget.Label != null ? widget.Label.text : string.Empty;
        }

        public void RefreshStagePromptsNow()
        {
            EnsureStagePromptWidgets();
            UpdateStageInteractionPrompts();
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
            UpdateSingleSuperBar(simulation != null ? simulation.Fighter1 : null, p1SuperFill, p1SuperText);
            UpdateSingleSuperBar(simulation != null ? simulation.Fighter2 : null, p2SuperFill, p2SuperText);
            UpdateRoundAndTimerUI();

            if (p1CalloutText != null) p1CalloutText.text = string.Empty;
            if (p2CalloutText != null) p2CalloutText.text = string.Empty;
            if (p1ComboText != null) p1ComboText.text = string.Empty;
            if (p2ComboText != null) p2ComboText.text = string.Empty;
            _p1ComboTimer = 0f;
            _p2ComboTimer = 0f;
            UpdateStageInteractionPrompts();
        }

        private void HandleSuperFlashTriggered(FighterController caster, int freezeTicks)
        {
            if (simulation == null || caster == null)
            {
                return;
            }

            bool casterIsP1 = caster == simulation.Fighter1;
            TextMeshProUGUI targetCallout = casterIsP1 ? p1CalloutText : p2CalloutText;
            if (targetCallout != null)
            {
                targetCallout.text = "SUPER!";
                targetCallout.color = new Color(1.0f, 0.88f, 0.20f, 1f);
                if (casterIsP1) _p1CalloutTimer = calloutDurationSeconds;
                else _p2CalloutTimer = calloutDurationSeconds;
            }
        }

        private void HandleCombatHit(CombatHitEvent hit)
        {
            if (simulation == null || hit.Defender == null)
            {
                return;
            }

            // Show feedback callout on the defender's side (HIT / BLOCK / K.O.! / EX! / ARMOR! / ESPECIAL!)
            bool defenderIsP1 = hit.Defender == simulation.Fighter1;
            TextMeshProUGUI targetCallout = defenderIsP1 ? p1CalloutText : p2CalloutText;
            if (targetCallout != null)
            {
                if (hit.WasArmorAbsorbed)
                {
                    targetCallout.text = "ARMOR!";
                    targetCallout.color = new Color(0.88f, 0.94f, 1.0f, 1f);
                }
                else if (hit.Outcome == HitOutcomeType.ThrowTech)
                {
                    targetCallout.text = "TECH!";
                    targetCallout.color = new Color(0.45f, 0.96f, 1.0f, 1f);
                }
                else if (hit.Outcome == HitOutcomeType.Blocked)
                {
                    targetCallout.text = "BLOCK!";
                    targetCallout.color = new Color(0.35f, 0.85f, 1.0f, 1f);
                }
                else if (hit.Outcome == HitOutcomeType.KnockDown && hit.Defender.CurrentHealth <= 0)
                {
                    targetCallout.text = "K.O.!";
                    targetCallout.color = new Color(1.0f, 0.25f, 0.20f, 1f);
                }
                else if (hit.IsSpecial)
                {
                    string moveLabel = hit.Move != null && !string.IsNullOrWhiteSpace(hit.Move.moveName)
                        ? hit.Move.moveName.ToUpperInvariant() + "!"
                        : "ESPECIAL!";
                    targetCallout.text = moveLabel;
                    targetCallout.color = new Color(1.0f, 0.86f, 0.25f, 1f);
                }
                else if (hit.IsExMove)
                {
                    targetCallout.text = "EX!";
                    targetCallout.color = new Color(1.0f, 0.80f, 0.16f, 1f);
                }
                else
                {
                    targetCallout.text = hit.WasTrade ? "TRADE HIT!" : "HIT!";
                    targetCallout.color = new Color(1.0f, 0.78f, 0.16f, 1f);
                }

                if (defenderIsP1) _p1CalloutTimer = calloutDurationSeconds;
                else _p2CalloutTimer = calloutDurationSeconds;
            }

            // Show combo counter on the attacker's side from 2 hits onward (or when a Combo String completes)
            string comboStringLabel = !string.IsNullOrWhiteSpace(hit.ComboStringName)
                ? hit.ComboStringName.ToUpperInvariant()
                : (hit.Attacker != null && !string.IsNullOrWhiteSpace(hit.Attacker.ActiveComboStringName)
                    ? hit.Attacker.ActiveComboStringName.ToUpperInvariant()
                    : string.Empty);

            if (hit.Outcome != HitOutcomeType.Blocked &&
                hit.Outcome != HitOutcomeType.ThrowTech &&
                hit.Attacker != null &&
                (hit.ComboCount >= 2 || !string.IsNullOrEmpty(comboStringLabel)))
            {
                bool attackerIsP1 = hit.Attacker == simulation.Fighter1;
                TextMeshProUGUI comboText = attackerIsP1 ? p1ComboText : p2ComboText;
                if (comboText != null)
                {
                    if (hit.ComboCount >= 2 && !string.IsNullOrEmpty(comboStringLabel))
                    {
                        comboText.text = $"{hit.ComboCount} HITS \u2022 {comboStringLabel}!";
                    }
                    else if (hit.ComboCount >= 2)
                    {
                        comboText.text = $"{hit.ComboCount} HITS!";
                    }
                    else
                    {
                        comboText.text = $"{comboStringLabel}!";
                    }
                    comboText.color = hit.IsSpecial
                        ? new Color(1.0f, 0.88f, 0.28f, 1f)
                        : new Color(1.0f, 0.65f, 0.18f, 1f);
                    if (attackerIsP1) _p1ComboTimer = comboDisplayDurationSeconds;
                    else _p2ComboTimer = comboDisplayDurationSeconds;
                }
            }
        }

        private void UpdateFighterBars(float dt)
        {
            FighterController p1 = simulation != null ? simulation.Fighter1 : null;
            FighterController p2 = simulation != null ? simulation.Fighter2 : null;

            UpdateSingleHealthBar(p1, p1HealthFill, p1DamageTrailFill, ref _p1PrevRatio, ref _p1TrailRatio, ref _p1TrailHoldTimer, dt);
            UpdateSingleHealthBar(p2, p2HealthFill, p2DamageTrailFill, ref _p2PrevRatio, ref _p2TrailRatio, ref _p2TrailHoldTimer, dt);
            UpdateSingleSuperBar(p1, p1SuperFill, p1SuperText);
            UpdateSingleSuperBar(p2, p2SuperFill, p2SuperText);
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
            UpdateSingleCallout(p1CalloutText, ref _p1CalloutTimer, dt, calloutDurationSeconds);
            UpdateSingleCallout(p2CalloutText, ref _p2CalloutTimer, dt, calloutDurationSeconds);
            UpdateSingleCallout(p1ComboText, ref _p1ComboTimer, dt, comboDisplayDurationSeconds);
            UpdateSingleCallout(p2ComboText, ref _p2ComboTimer, dt, comboDisplayDurationSeconds);
        }

        private void UpdateSingleCallout(TextMeshProUGUI callout, ref float timer, float dt, float totalDuration)
        {
            if (callout == null) return;

            if (timer > 0f)
            {
                timer -= dt;
                float fadeWindow = Mathf.Max(0.15f, totalDuration * 0.35f);
                float alpha = Mathf.Clamp01(timer / fadeWindow);
                Color c = callout.color;
                callout.color = new Color(c.r, c.g, c.b, alpha);
                if (timer <= 0f)
                {
                    callout.text = string.Empty;
                }
            }
        }

        private void UpdateSingleSuperBar(FighterController fighter, UnityEngine.UI.Image superFill, TextMeshProUGUI superText)
        {
            float ratio = fighter != null ? fighter.SuperNormalized : 0f;
            bool isSuperReady = fighter != null && fighter.CanUseSpecial;
            bool isExReady = fighter != null && fighter.CanUseEx && !isSuperReady;

            if (superFill != null)
            {
                superFill.fillAmount = ratio;
                if (isSuperReady)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                    superFill.color = Color.Lerp(superReadyColorA, superReadyColorB, pulse);
                }
                else if (isExReady)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                    superFill.color = Color.Lerp(superExReadyColor, new Color(1.0f, 0.78f, 0.22f, 1f), pulse * 0.45f);
                }
                else
                {
                    superFill.color = superChargingColor;
                }
            }

            if (superText != null)
            {
                if (isSuperReady)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                    superText.text = "SUPER PRONTO!";
                    superText.color = Color.Lerp(superReadyColorA, superReadyColorB, pulse);
                }
                else if (isExReady)
                {
                    int pct = fighter != null ? fighter.SuperGauge : 50;
                    superText.text = $"EX PRONTO! {pct}%";
                    superText.color = new Color(0.45f, 0.96f, 1.0f, 0.98f);
                }
                else
                {
                    int pct = fighter != null ? fighter.SuperGauge : 0;
                    superText.text = $"ESPECIAL {pct}%";
                    superText.color = new Color(0.75f, 0.88f, 0.98f, 0.92f);
                }
            }
        }

        /// <summary>
        /// Ensures P1 & P2 Super Gauge bars and Combo Counter labels exist and are wired,
        /// both for Edit-Mode scene serialization and as a dynamic runtime fallback.
        /// </summary>
        public void EnsureSuperAndComboWidgets()
        {
            Transform p1Group = FindSideGroup(p1NameText, p1HealthFill, "P1_Group");
            Transform p2Group = FindSideGroup(p2NameText, p2HealthFill, "P2_Group");

            Sprite barSprite = GetHudSprite();
            TMP_FontAsset fontAsset = p1NameText != null ? p1NameText.font : (p2NameText != null ? p2NameText.font : null);

            if (p1Group != null)
            {
                EnsureSideWidgets(
                    p1Group,
                    "P1",
                    isLeft: true,
                    barSprite,
                    fontAsset,
                    p1CalloutText,
                    ref p1SuperFill,
                    ref p1SuperText,
                    ref p1ComboText);
            }

            if (p2Group != null)
            {
                EnsureSideWidgets(
                    p2Group,
                    "P2",
                    isLeft: false,
                    barSprite,
                    fontAsset,
                    p2CalloutText,
                    ref p2SuperFill,
                    ref p2SuperText,
                    ref p2ComboText);
            }
        }

        private Transform FindSideGroup(TextMeshProUGUI nameText, UnityEngine.UI.Image healthFill, string fallbackChildName)
        {
            if (nameText != null && nameText.transform.parent != null)
            {
                return nameText.transform.parent;
            }
            if (healthFill != null)
            {
                Transform cursor = healthFill.transform;
                while (cursor != null && cursor.parent != null && cursor.parent != transform)
                {
                    if (cursor.name.EndsWith("_Group", System.StringComparison.Ordinal))
                    {
                        return cursor;
                    }
                    cursor = cursor.parent;
                }
            }
            return FindDescendant(transform, fallbackChildName);
        }

        private static Transform FindDescendant(Transform root, string targetName)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == targetName) return child;
                Transform nested = FindDescendant(child, targetName);
                if (nested != null) return nested;
            }
            return null;
        }

        private void EnsureSideWidgets(
            Transform group,
            string prefix,
            bool isLeft,
            Sprite barSprite,
            TMP_FontAsset fontAsset,
            TextMeshProUGUI calloutText,
            ref UnityEngine.UI.Image superFill,
            ref TextMeshProUGUI superText,
            ref TextMeshProUGUI comboText)
        {
            Transform existingBorder = group.Find($"{prefix}_SuperBorder");
            if (existingBorder == null)
            {
                var borderGo = new GameObject($"{prefix}_SuperBorder", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                borderGo.transform.SetParent(group, false);
                var borderRt = (RectTransform)borderGo.transform;
                borderRt.anchorMin = isLeft ? new Vector2(0f, 1f) : new Vector2(0.28f, 1f);
                borderRt.anchorMax = isLeft ? new Vector2(0.72f, 1f) : new Vector2(1f, 1f);
                borderRt.pivot = new Vector2(0.5f, 1f);
                borderRt.anchoredPosition = new Vector2(0f, -96f);
                borderRt.sizeDelta = new Vector2(0f, 14f);

                var borderImg = borderGo.GetComponent<UnityEngine.UI.Image>();
                borderImg.sprite = barSprite;
                borderImg.color = new Color(0.29f, 0.25f, 0.21f, 0.95f);
                borderImg.raycastTarget = false;

                var bgGo = new GameObject($"{prefix}_SuperBg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                bgGo.transform.SetParent(borderGo.transform, false);
                var bgRt = (RectTransform)bgGo.transform;
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = new Vector2(2f, 2f);
                bgRt.offsetMax = new Vector2(-2f, -2f);

                var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
                bgImg.sprite = barSprite;
                bgImg.color = new Color(0.08f, 0.09f, 0.13f, 0.96f);
                bgImg.raycastTarget = false;

                var fillGo = new GameObject($"{prefix}_SuperFill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                fillGo.transform.SetParent(bgGo.transform, false);
                var fillRt = (RectTransform)fillGo.transform;
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;

                superFill = fillGo.GetComponent<UnityEngine.UI.Image>();
                superFill.sprite = barSprite;
                superFill.type = UnityEngine.UI.Image.Type.Filled;
                superFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                superFill.fillOrigin = isLeft ? (int)UnityEngine.UI.Image.OriginHorizontal.Left : (int)UnityEngine.UI.Image.OriginHorizontal.Right;
                superFill.fillAmount = 0f;
                superFill.color = superChargingColor;
                superFill.raycastTarget = false;
                existingBorder = borderGo.transform;
            }
            else if (superFill == null)
            {
                Transform fillTr = FindDescendant(existingBorder, $"{prefix}_SuperFill");
                if (fillTr != null) superFill = fillTr.GetComponent<UnityEngine.UI.Image>();
            }

            if (existingBorder != null && existingBorder.Find($"{prefix}_SuperHalfMarker") == null)
            {
                var markerGo = new GameObject($"{prefix}_SuperHalfMarker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                markerGo.transform.SetParent(existingBorder, false);
                var markerRt = (RectTransform)markerGo.transform;
                markerRt.anchorMin = new Vector2(0.5f, 0f);
                markerRt.anchorMax = new Vector2(0.5f, 1f);
                markerRt.pivot = new Vector2(0.5f, 0.5f);
                markerRt.anchoredPosition = Vector2.zero;
                markerRt.sizeDelta = new Vector2(2.5f, 2f);

                var markerImg = markerGo.GetComponent<UnityEngine.UI.Image>();
                markerImg.sprite = barSprite;
                markerImg.color = new Color(0.95f, 0.90f, 0.72f, 0.92f);
                markerImg.raycastTarget = false;
            }

            if (superText == null)
            {
                Transform existingLabel = group.Find($"{prefix}_SuperText");
                if (existingLabel != null)
                {
                    superText = existingLabel.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    superText = CreateHudLabel(
                        group,
                        $"{prefix}_SuperText",
                        fontAsset,
                        anchorMin: new Vector2(0f, 1f),
                        anchorMax: new Vector2(1f, 1f),
                        anchoredPos: new Vector2(0f, -112f),
                        sizeDelta: new Vector2(0f, 22f),
                        fontSize: 14f,
                        alignment: isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                        initialColor: new Color(0.75f, 0.88f, 0.98f, 0.92f));
                    superText.text = "ESPECIAL 0%";
                }
            }

            if (calloutText != null && calloutText.rectTransform.anchoredPosition.y > -130f)
            {
                calloutText.rectTransform.anchoredPosition = new Vector2(calloutText.rectTransform.anchoredPosition.x, -138f);
            }

            if (comboText == null)
            {
                Transform existingCombo = group.Find($"{prefix}_ComboText");
                if (existingCombo != null)
                {
                    comboText = existingCombo.GetComponent<TextMeshProUGUI>();
                }
                else
                {
                    comboText = CreateHudLabel(
                        group,
                        $"{prefix}_ComboText",
                        fontAsset,
                        anchorMin: new Vector2(0f, 1f),
                        anchorMax: new Vector2(1f, 1f),
                        anchoredPos: new Vector2(0f, -184f),
                        sizeDelta: new Vector2(0f, 44f),
                        fontSize: 32f,
                        alignment: isLeft ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight,
                        initialColor: new Color(1.0f, 0.65f, 0.18f, 1f));
                    comboText.text = string.Empty;
                }
            }
        }

        private static TextMeshProUGUI CreateHudLabel(
            Transform parent,
            string name,
            TMP_FontAsset fontAsset,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            float fontSize,
            TextAlignmentOptions alignment,
            Color initialColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = alignment;
            tmp.characterSpacing = 2.5f;
            tmp.color = initialColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Sprite GetHudSprite()
        {
            if (p1HealthFill != null && p1HealthFill.sprite != null) return p1HealthFill.sprite;
            if (p2HealthFill != null && p2HealthFill.sprite != null) return p2HealthFill.sprite;
            if (_fallbackWhiteSprite != null) return _fallbackWhiteSprite;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _fallbackWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _fallbackWhiteSprite;
        }

        private static float GetHealthRatio(FighterController fighter)
        {
            if (fighter == null || fighter.MaxHealth <= 0)
            {
                return 1f;
            }
            return Mathf.Clamp01(fighter.CurrentHealth / (float)fighter.MaxHealth);
        }

        private void EnsureStagePromptWidgets()
        {
            Sprite barSprite = GetHudSprite();
            TMP_FontAsset fontAsset = p1NameText != null ? p1NameText.font : (p2NameText != null ? p2NameText.font : null);

            if (_p1StagePrompt == null)
            {
                _p1StagePrompt = CreateOrReusePromptWidget("StagePrompt_P1", _p1PromptColor, barSprite, fontAsset);
            }
            if (_p2StagePrompt == null)
            {
                _p2StagePrompt = CreateOrReusePromptWidget("StagePrompt_P2", _p2PromptColor, barSprite, fontAsset);
            }
        }

        private StagePromptWidget CreateOrReusePromptWidget(string widgetName, Color accentColor, Sprite sprite, TMP_FontAsset fontAsset)
        {
            Transform existing = transform.Find(widgetName);
            if (existing != null)
            {
                var existingBorder = existing.GetComponent<UnityEngine.UI.Image>();
                Transform bgTr = existing.Find("Bg");
                var existingBg = bgTr != null ? bgTr.GetComponent<UnityEngine.UI.Image>() : null;
                Transform labelTr = bgTr != null ? bgTr.Find("Label") : existing.Find("Label");
                var existingLabel = labelTr != null ? labelTr.GetComponent<TextMeshProUGUI>() : null;
                return new StagePromptWidget
                {
                    Root = (RectTransform)existing,
                    Border = existingBorder,
                    Background = existingBg,
                    Label = existingLabel
                };
            }

            var rootGo = new GameObject(widgetName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            rootGo.transform.SetParent(transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(238f, 34f);

            var borderImg = rootGo.GetComponent<UnityEngine.UI.Image>();
            borderImg.sprite = sprite;
            borderImg.color = accentColor;
            borderImg.raycastTarget = false;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(rootGo.transform, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(2f, 2f);
            bgRt.offsetMax = new Vector2(-2f, -2f);

            var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
            bgImg.sprite = sprite;
            bgImg.color = new Color(0.06f, 0.07f, 0.10f, 0.90f);
            bgImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(bgGo.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(6f, 2f);
            labelRt.offsetMax = new Vector2(-6f, -2f);

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.fontSize = 14f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = 1.5f;
            tmp.color = accentColor;
            tmp.text = "[DEF + LP] ARREMESSAR";
            tmp.raycastTarget = false;

            rootGo.SetActive(false);

            return new StagePromptWidget
            {
                Root = rootRt,
                Border = borderImg,
                Background = bgImg,
                Label = tmp
            };
        }

        private void UpdateStageInteractionPrompts()
        {
            if (_p1StagePrompt == null && _p2StagePrompt == null)
            {
                return;
            }

            if (simulation == null || (matchManager != null && !matchManager.InputsAllowed) || simulation.IsPaused)
            {
                SetPromptVisible(_p1StagePrompt, false);
                SetPromptVisible(_p2StagePrompt, false);
                return;
            }

            if (stageInteractableManager == null)
            {
                stageInteractableManager = FindAnyObjectByType<StageInteractableManager>();
            }

            StageThrowableProp p1Prop = FindPromptPropForFighter(simulation.Fighter1);
            bool hideP2ForCpu = simulation.CpuAi != null && simulation.CpuAi.IsEnabled;
            StageThrowableProp p2Prop = hideP2ForCpu ? null : FindPromptPropForFighter(simulation.Fighter2);

            bool sameProp = p1Prop != null && p1Prop == p2Prop;
            UpdateSinglePromptWidget(_p1StagePrompt, p1Prop, _p1PromptColor, isP1: true, extraOffsetY: sameProp ? 22f : 0f);
            UpdateSinglePromptWidget(_p2StagePrompt, p2Prop, _p2PromptColor, isP1: false, extraOffsetY: sameProp ? -20f : 0f);
        }

        private StageThrowableProp FindPromptPropForFighter(FighterController fighter)
        {
            if (fighter == null || !fighter.CanAttemptStageInteract())
            {
                return null;
            }

            return stageInteractableManager != null
                ? stageInteractableManager.FindNearestReadyThrowable(fighter)
                : StageInteractableManager.FindNearestReadyThrowableInScene(fighter);
        }

        private void UpdateSinglePromptWidget(StagePromptWidget widget, StageThrowableProp prop, Color color, bool isP1, float extraOffsetY)
        {
            if (widget == null || widget.Root == null)
            {
                return;
            }

            if (prop == null)
            {
                SetPromptVisible(widget, false);
                return;
            }

            SetPromptVisible(widget, true);
            if (widget.Border != null) widget.Border.color = color;
            if (widget.Label != null)
            {
                widget.Label.color = color;
                widget.Label.text = $"[DEF + LP] {prop.PromptLabel.ToUpperInvariant()}";
            }

            Camera cam = Camera.main;
            var canvasRt = transform as RectTransform;
            if (cam != null && canvasRt != null)
            {
                Vector3 screenPos = cam.WorldToScreenPoint(prop.GetPromptWorldPosition());
                if (screenPos.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPos, null, out Vector2 localPt))
                {
                    Rect rect = canvasRt.rect;
                    float halfW = widget.Root.sizeDelta.x * 0.5f + 16f;
                    float halfH = widget.Root.sizeDelta.y * 0.5f + 16f;
                    float clampedX = Mathf.Clamp(localPt.x, rect.xMin + halfW, rect.xMax - halfW);
                    float clampedY = Mathf.Clamp(localPt.y + 28f + extraOffsetY, rect.yMin + halfH, rect.yMax - 150f);
                    widget.Root.anchoredPosition = new Vector2(clampedX, clampedY);
                    return;
                }
            }

            widget.Root.anchoredPosition = new Vector2(isP1 ? -260f : 260f, -150f + extraOffsetY);
        }

        private static void SetPromptVisible(StagePromptWidget widget, bool visible)
        {
            if (widget != null && widget.Root != null && widget.Root.gameObject.activeSelf != visible)
            {
                widget.Root.gameObject.SetActive(visible);
            }
        }
    }
}
