using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnDFight
{
    /// <summary>
    /// Synthesized audio manager driving ambient music loop, UI SFX, combat whooshes/impacts,
    /// and round gong announcements, synced live with SettingsManager volumes.
    /// </summary>
    public class FightAudioManager : MonoBehaviour
    {
        public static FightAudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private AudioClip _uiMoveClip;
        private AudioClip _uiConfirmClip;
        private AudioClip _uiBackClip;
        private AudioClip _whooshLightClip;
        private AudioClip _whooshHeavyClip;
        private AudioClip _whooshSpecialClip;
        private AudioClip _superFlashClip;
        private AudioClip _exActivationClip;
        private AudioClip _projectileFireClip;
        private AudioClip _armorAbsorbClip;
        private AudioClip _dashClip;
        private AudioClip _throwTechClip;
        private AudioClip _hitLightClip;
        private AudioClip _hitHeavyClip;
        private AudioClip _hitSpecialClip;
        private AudioClip _blockImpactClip;
        private AudioClip _knockdownClip;
        private AudioClip _propImpactClip;
        private AudioClip _propBreakWoodPlasticClip;
        private AudioClip _propBreakGlassClip;
        private AudioClip _roundGongClip;
        private AudioClip _ambientMusicClip;

        private FightSimulation _boundSimulation;
        private MatchManager _boundMatchManager;
        private float _sfxVolume = 0.85f;
        private string _lastGongBanner = string.Empty;
        private bool _clipsGenerated;

        public AudioSource MusicSource => musicSource;
        public AudioSource SfxSource => sfxSource;
        public float SfxVolume => _sfxVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            EnsureInstance();
        }

        public static FightAudioManager EnsureInstance()
        {
            if (Instance != null)
            {
                Instance.EnsureAudioReady();
                return Instance;
            }

            FightAudioManager existing = FindAnyObjectByType<FightAudioManager>();
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureAudioReady();
                return existing;
            }

            var go = new GameObject("FightAudioManager");
            Instance = go.AddComponent<FightAudioManager>();
            Instance.EnsureAudioReady();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureAudioReady();
        }

        private void OnEnable()
        {
            SettingsManager.OnSettingsApplied += HandleSettingsApplied;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            FighterController.OnAnyAttackStarted += HandleAnyAttackStarted;
            FighterController.OnAnyDashStarted += HandleAnyDashStarted;
            FighterController.OnAnyProjectileFired += HandleAnyProjectileFired;
            MenuNavigationGroup.OnAnySelectionMoved += HandleUiMove;
            MenuNavigationGroup.OnAnySubmitted += HandleUiConfirm;
            MenuNavigationGroup.OnAnyCancelled += HandleUiBack;

            EnsureAudioReady();
            BindSceneGameplaySources();
        }

        private void OnDisable()
        {
            SettingsManager.OnSettingsApplied -= HandleSettingsApplied;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            FighterController.OnAnyAttackStarted -= HandleAnyAttackStarted;
            FighterController.OnAnyDashStarted -= HandleAnyDashStarted;
            FighterController.OnAnyProjectileFired -= HandleAnyProjectileFired;
            MenuNavigationGroup.OnAnySelectionMoved -= HandleUiMove;
            MenuNavigationGroup.OnAnySubmitted -= HandleUiConfirm;
            MenuNavigationGroup.OnAnyCancelled -= HandleUiBack;
            UnbindGameplaySources();
        }

        private void OnDestroy()
        {
            SettingsManager.OnSettingsApplied -= HandleSettingsApplied;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            FighterController.OnAnyAttackStarted -= HandleAnyAttackStarted;
            FighterController.OnAnyDashStarted -= HandleAnyDashStarted;
            FighterController.OnAnyProjectileFired -= HandleAnyProjectileFired;
            MenuNavigationGroup.OnAnySelectionMoved -= HandleUiMove;
            MenuNavigationGroup.OnAnySubmitted -= HandleUiConfirm;
            MenuNavigationGroup.OnAnyCancelled -= HandleUiBack;
            UnbindGameplaySources();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void EnsureAudioReady()
        {
            SettingsManager.OnSettingsApplied -= HandleSettingsApplied;
            SettingsManager.OnSettingsApplied += HandleSettingsApplied;

            EnsureSources();
            GenerateClipsIfNeeded();
            ApplyVolumes(SettingsManager.Current);

            if (Application.isPlaying && musicSource != null && _ambientMusicClip != null && !musicSource.isPlaying)
            {
                musicSource.clip = _ambientMusicClip;
                musicSource.loop = true;
                musicSource.Play();
            }
        }

        public void ApplyVolumes(GameSettingsData settings)
        {
            if (settings == null)
            {
                return;
            }

            AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
            _sfxVolume = Mathf.Clamp01(settings.sfxVolume);

            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(settings.musicVolume) * 0.45f;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = 1f;
            }
        }

        public void PlayUiMove() => PlaySfx(_uiMoveClip, 0.75f);
        public void PlayUiConfirm() => PlaySfx(_uiConfirmClip, 0.90f);
        public void PlayUiBack() => PlaySfx(_uiBackClip, 0.85f);
        public void PlayRoundGong() => PlaySfx(_roundGongClip, 0.85f);

        public void PlayPropImpact(StagePropMaterialType materialType = StagePropMaterialType.WoodOrCrate)
        {
            PlaySfx(_propImpactClip, materialType == StagePropMaterialType.Glass ? 0.72f : 0.80f);
        }

        public void PlayPropBreak(StagePropMaterialType materialType = StagePropMaterialType.WoodOrCrate)
        {
            AudioClip clip = materialType == StagePropMaterialType.Glass
                ? _propBreakGlassClip
                : _propBreakWoodPlasticClip;
            PlaySfx(clip, 0.92f);
        }

        public void PlayDash(bool isForward)
        {
            PlaySfx(_dashClip, isForward ? 0.72f : 0.65f);
        }

        public void PlaySuperFlash() => PlaySfx(_superFlashClip, 0.96f);
        public void PlayExActivation() => PlaySfx(_exActivationClip, 0.85f);
        public void PlayProjectileFire() => PlaySfx(_projectileFireClip, 0.88f);
        public void PlayArmorAbsorb() => PlaySfx(_armorAbsorbClip, 0.95f);

        public void PlayAttackWhoosh(AttackType attack)
        {
            if (attack == AttackType.Special)
            {
                PlaySfx(_whooshSpecialClip, 0.95f);
                return;
            }
            if (attack == AttackType.ExHeavyPunch || attack == AttackType.ExHeavyKick)
            {
                PlayExActivation();
                PlaySfx(_whooshHeavyClip, 0.85f);
                return;
            }
            bool heavy = attack == AttackType.HeavyPunch ||
                         attack == AttackType.HeavyKick ||
                         attack == AttackType.CrouchKick ||
                         attack == AttackType.JumpAttack;
            PlaySfx(heavy ? _whooshHeavyClip : _whooshLightClip, 0.75f);
        }

        public void PlayCombatHit(CombatHitEvent hit)
        {
            if (hit.WasArmorAbsorbed)
            {
                PlayArmorAbsorb();
                return;
            }

            if (hit.Outcome == HitOutcomeType.ThrowTech)
            {
                PlaySfx(_throwTechClip, 0.95f);
                return;
            }

            if (hit.Outcome == HitOutcomeType.Blocked)
            {
                PlaySfx(_blockImpactClip, hit.IsSpecial ? 0.98f : 0.90f);
                return;
            }

            if (hit.IsSpecial || (hit.Move != null && hit.Move.attackType == AttackType.Special))
            {
                PlaySfx(_hitSpecialClip, 1.0f);
                if (hit.Outcome == HitOutcomeType.KnockDown)
                {
                    PlaySfx(_knockdownClip, 0.85f);
                }
                return;
            }

            if (hit.Outcome == HitOutcomeType.KnockDown)
            {
                PlaySfx(_hitHeavyClip, 0.95f);
                PlaySfx(_knockdownClip, 0.90f);
                return;
            }

            bool isHeavyMove = hit.Move != null &&
                               (hit.Move.attackType == AttackType.HeavyPunch ||
                                hit.Move.attackType == AttackType.HeavyKick ||
                                hit.Move.damage >= 90);
            PlaySfx(isHeavyMove ? _hitHeavyClip : _hitLightClip, 0.92f);
        }

        private void PlaySfx(AudioClip clip, float gain)
        {
            if (clip == null || sfxSource == null || !Application.isPlaying)
            {
                return;
            }

            float finalScale = Mathf.Clamp01(_sfxVolume * gain);
            if (finalScale > 0.001f)
            {
                sfxSource.PlayOneShot(clip, finalScale);
            }
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        private void GenerateClipsIfNeeded()
        {
            if (_clipsGenerated)
            {
                return;
            }

            _uiMoveClip = ProceduralAudioFactory.CreateUiMoveClip();
            _uiConfirmClip = ProceduralAudioFactory.CreateUiConfirmClip();
            _uiBackClip = ProceduralAudioFactory.CreateUiBackClip();
            _whooshLightClip = ProceduralAudioFactory.CreateWhooshLightClip();
            _whooshHeavyClip = ProceduralAudioFactory.CreateWhooshHeavyClip();
            _whooshSpecialClip = ProceduralAudioFactory.CreateWhooshSpecialClip();
            _superFlashClip = ProceduralAudioFactory.CreateSuperFlashClip();
            _exActivationClip = ProceduralAudioFactory.CreateExActivationClip();
            _projectileFireClip = ProceduralAudioFactory.CreateProjectileFireClip();
            _armorAbsorbClip = ProceduralAudioFactory.CreateArmorAbsorbClip();
            _dashClip = ProceduralAudioFactory.CreateDashClip();
            _throwTechClip = ProceduralAudioFactory.CreateThrowTechClip();
            _hitLightClip = ProceduralAudioFactory.CreateHitLightClip();
            _hitHeavyClip = ProceduralAudioFactory.CreateHitHeavyClip();
            _hitSpecialClip = ProceduralAudioFactory.CreateHitSpecialClip();
            _blockImpactClip = ProceduralAudioFactory.CreateBlockImpactClip();
            _knockdownClip = ProceduralAudioFactory.CreateKnockdownClip();
            _propImpactClip = ProceduralAudioFactory.CreatePropImpactClip();
            _propBreakWoodPlasticClip = ProceduralAudioFactory.CreatePropBreakWoodPlasticClip();
            _propBreakGlassClip = ProceduralAudioFactory.CreatePropBreakGlassClip();
            _roundGongClip = ProceduralAudioFactory.CreateRoundGongClip();
            _ambientMusicClip = ProceduralAudioFactory.CreateAmbientMusicLoopClip();
            _clipsGenerated = true;
        }

        private void HandleSettingsApplied(GameSettingsData settings)
        {
            ApplyVolumes(settings);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _lastGongBanner = string.Empty;
            BindSceneGameplaySources();
        }

        private void BindSceneGameplaySources()
        {
            UnbindGameplaySources();

            _boundSimulation = FindAnyObjectByType<FightSimulation>();
            if (_boundSimulation != null)
            {
                _boundSimulation.OnCombatHit += HandleCombatHit;
                _boundSimulation.OnSuperFlashTriggered += HandleSuperFlashTriggered;
            }

            _boundMatchManager = FindAnyObjectByType<MatchManager>();
            if (_boundMatchManager != null)
            {
                _boundMatchManager.OnMatchStateChanged += HandleMatchStateChanged;
            }
        }

        private void UnbindGameplaySources()
        {
            if (_boundSimulation != null)
            {
                _boundSimulation.OnCombatHit -= HandleCombatHit;
                _boundSimulation.OnSuperFlashTriggered -= HandleSuperFlashTriggered;
                _boundSimulation = null;
            }

            if (_boundMatchManager != null)
            {
                _boundMatchManager.OnMatchStateChanged -= HandleMatchStateChanged;
                _boundMatchManager = null;
            }
        }

        private void HandleCombatHit(CombatHitEvent hit) => PlayCombatHit(hit);
        private void HandleAnyAttackStarted(FighterController fighter, AttackType attack) => PlayAttackWhoosh(attack);
        private void HandleAnyDashStarted(FighterController fighter, bool isForward) => PlayDash(isForward);
        private void HandleAnyProjectileFired(FighterController fighter, MoveData move) => PlayProjectileFire();
        private void HandleSuperFlashTriggered(FighterController caster, int freezeTicks) => PlaySuperFlash();
        private void HandleUiMove() => PlayUiMove();
        private void HandleUiConfirm() => PlayUiConfirm();
        private void HandleUiBack() => PlayUiBack();

        private void HandleMatchStateChanged(MatchManager match)
        {
            if (match == null)
            {
                return;
            }

            string banner = match.BannerText ?? string.Empty;
            if (string.IsNullOrEmpty(banner) || banner == _lastGongBanner)
            {
                if (string.IsNullOrEmpty(banner)) _lastGongBanner = string.Empty;
                return;
            }

            if (banner.StartsWith("ROUND") || banner == "FIGHT!" || banner == "K.O.!" || banner == "TIME OVER!")
            {
                _lastGongBanner = banner;
                PlayRoundGong();
            }
        }
    }
}
