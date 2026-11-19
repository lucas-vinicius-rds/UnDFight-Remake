using System;
using System.Collections.Generic;

namespace UnDFight
{
    [Serializable]
    public class ArcadeTowerStageEntry
    {
        public FighterData opponent;
        public string stageSceneName;
        public string stageDisplayName;
        public StageWeatherVariant weather;
        public CpuDifficulty cpuDifficulty;
        public bool isBossMatch;
        public string ladderLabel;

        public string GetWeatherDisplayLabel(StageRoster stageRoster = null)
        {
            StageData stage = stageRoster != null ? stageRoster.FindBySceneName(stageSceneName) : null;
            if (stage != null && !stage.supportsWeatherVariants)
            {
                return !string.IsNullOrWhiteSpace(stage.dayVariantLabel)
                    ? stage.dayVariantLabel.ToUpperInvariant()
                    : "INTERIOR";
            }

            if (string.Equals(stageSceneName, "Arena_Cadeia", StringComparison.OrdinalIgnoreCase))
            {
                return "INTERIOR";
            }

            if (stage == null && string.Equals(stageSceneName, "Arena_Favela", StringComparison.OrdinalIgnoreCase) &&
                weather == StageWeatherVariant.Day)
            {
                return "PÔR DO SOL";
            }

            return GameSessionState.GetWeatherLabel(weather, stage);
        }
    }

    /// <summary>
    /// Full 7-match Arcade Tower ladder state: 6 unique regular opponents escalating through
    /// Favela Carioca and Frente da UnDF across Day -> Night -> RainyNight, culminating in
    /// the Secret Final Boss (Ibaneis) inside Arena_Cadeia (Bloco de Celas).
    /// </summary>
    public class ArcadeTowerRun
    {
        public const int TotalTowerMatches = 7;
        public const int RegularMatchesCount = 6;
        public const string FavelaSceneName = "Arena_Favela";
        public const string UndfSceneName = "Arena_UnDF";
        public const string BossStageSceneName = "Arena_Cadeia";

        public FighterData PlayerFighter { get; private set; }
        public CpuDifficulty BaseDifficulty { get; private set; }
        public List<ArcadeTowerStageEntry> Matches { get; private set; } = new List<ArcadeTowerStageEntry>();
        public int CurrentMatchIndex { get; private set; }
        public int ContinuesUsed { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool UnlockedBossThisRun { get; private set; }
        public bool VictoryRecorded { get; private set; }

        public int TotalMatches => Matches != null ? Matches.Count : 0;
        public ArcadeTowerStageEntry CurrentMatch =>
            Matches != null && CurrentMatchIndex >= 0 && CurrentMatchIndex < Matches.Count
                ? Matches[CurrentMatchIndex]
                : null;
        public ArcadeTowerStageEntry NextMatch =>
            Matches != null && CurrentMatchIndex + 1 >= 0 && CurrentMatchIndex + 1 < Matches.Count
                ? Matches[CurrentMatchIndex + 1]
                : null;
        public bool HasNextMatch => Matches != null && CurrentMatchIndex + 1 < Matches.Count;
        public bool IsCurrentMatchBoss => CurrentMatch != null && CurrentMatch.isBossMatch;
        public FighterData BossFighter => Matches != null && Matches.Count > 0 ? Matches[Matches.Count - 1].opponent : null;

        public static ArcadeTowerRun BuildRun(
            FighterData playerFighter,
            FighterRoster fighterRoster,
            StageRoster stageRoster,
            CpuDifficulty baseDifficulty,
            int? randomSeed = null)
        {
            if (fighterRoster == null || fighterRoster.Count == 0)
            {
                throw new ArgumentException("FighterRoster must contain fighters to build an ArcadeTowerRun.", nameof(fighterRoster));
            }

            FighterData resolvedPlayer = playerFighter != null ? playerFighter : fighterRoster.GetFighter(0);
            FighterData bossFighter = FindSecretBoss(fighterRoster);
            var regularPool = new List<FighterData>();

            for (int i = 0; i < fighterRoster.Count; i++)
            {
                FighterData f = fighterRoster.GetFighter(i);
                if (f == null || f.isSecretBoss || f == resolvedPlayer)
                {
                    continue;
                }
                if (!regularPool.Contains(f))
                {
                    regularPool.Add(f);
                }
            }

            // Deterministic Fisher-Yates shuffle when randomSeed is provided
            var rng = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            for (int i = regularPool.Count - 1; i > 0; i--)
            {
                int swapIdx = rng.Next(i + 1);
                FighterData tmp = regularPool[i];
                regularPool[i] = regularPool[swapIdx];
                regularPool[swapIdx] = tmp;
            }

            // Build exactly 6 regular opponents (unique whenever pool has >= 6)
            var chosenRegulars = new List<FighterData>(RegularMatchesCount);
            if (regularPool.Count > 0)
            {
                for (int i = 0; i < RegularMatchesCount; i++)
                {
                    chosenRegulars.Add(regularPool[i % regularPool.Count]);
                }
            }
            else
            {
                for (int i = 0; i < RegularMatchesCount; i++)
                {
                    chosenRegulars.Add(resolvedPlayer);
                }
            }

            // Stage + atmospheric progression across the 6 regular matches:
            // 1-2: Favela (Day) -> UnDF (Day)
            // 3-4: UnDF (Night) -> Favela (Night)
            // 5-6: Favela (RainyNight) -> UnDF (RainyNight)
            // 7:   Boss Ibaneis @ Arena_Cadeia (Day / Fixed Indoor)
            var stagePlan = new (string sceneName, StageWeatherVariant weather, string ladderLabel)[]
            {
                (FavelaSceneName, StageWeatherVariant.Day, "LUTA 1/7"),
                (UndfSceneName, StageWeatherVariant.Day, "LUTA 2/7"),
                (UndfSceneName, StageWeatherVariant.Night, "LUTA 3/7"),
                (FavelaSceneName, StageWeatherVariant.Night, "LUTA 4/7"),
                (FavelaSceneName, StageWeatherVariant.RainyNight, "LUTA 5/7"),
                (UndfSceneName, StageWeatherVariant.RainyNight, "SEMIFINAL 6/7"),
            };

            var matches = new List<ArcadeTowerStageEntry>(TotalTowerMatches);
            for (int i = 0; i < RegularMatchesCount; i++)
            {
                var step = stagePlan[i];
                matches.Add(new ArcadeTowerStageEntry
                {
                    opponent = chosenRegulars[i],
                    stageSceneName = step.sceneName,
                    stageDisplayName = ResolveStageDisplayName(stageRoster, step.sceneName),
                    weather = step.weather,
                    cpuDifficulty = ComputeStepDifficulty(baseDifficulty, i, isBoss: false),
                    isBossMatch = false,
                    ladderLabel = step.ladderLabel
                });
            }

            matches.Add(new ArcadeTowerStageEntry
            {
                opponent = bossFighter != null ? bossFighter : chosenRegulars[chosenRegulars.Count - 1],
                stageSceneName = BossStageSceneName,
                stageDisplayName = ResolveStageDisplayName(stageRoster, BossStageSceneName),
                weather = StageWeatherVariant.Day,
                cpuDifficulty = ComputeStepDifficulty(baseDifficulty, RegularMatchesCount, isBoss: true),
                isBossMatch = true,
                ladderLabel = "CHEFE FINAL"
            });

            return new ArcadeTowerRun
            {
                PlayerFighter = resolvedPlayer,
                BaseDifficulty = baseDifficulty,
                Matches = matches,
                CurrentMatchIndex = 0,
                ContinuesUsed = 0,
                IsCompleted = false,
                UnlockedBossThisRun = false,
                VictoryRecorded = false
            };
        }

        public bool AdvanceToNextMatch()
        {
            if (!HasNextMatch)
            {
                return false;
            }

            CurrentMatchIndex++;
            return true;
        }

        public void RecordContinueUsed()
        {
            ContinuesUsed++;
        }

        public void MarkCompleted(bool unlockedBossForFirstTime)
        {
            IsCompleted = true;
            VictoryRecorded = true;
            UnlockedBossThisRun = unlockedBossForFirstTime;
        }

        public void ResetToFirstMatch()
        {
            CurrentMatchIndex = 0;
            ContinuesUsed = 0;
            IsCompleted = false;
            VictoryRecorded = false;
            UnlockedBossThisRun = false;
        }

        public void ApplyCurrentMatchToSession()
        {
            GameSessionState.SelectMode(GameMode.ArcadeTower);
            GameSessionState.ActiveTowerRun = this;
            GameSessionState.OpenStageSelectOnCharacterSelect = false;

            ArcadeTowerStageEntry entry = CurrentMatch;
            if (entry == null)
            {
                return;
            }

            GameSessionState.SelectFighters(PlayerFighter, entry.opponent);
            GameSessionState.SelectStageScene(entry.stageSceneName);
            GameSessionState.SelectWeather(entry.weather);
            GameSessionState.SelectedCpuDifficulty = entry.cpuDifficulty;
        }

        public static CpuDifficulty ComputeStepDifficulty(CpuDifficulty baseDifficulty, int matchIndex, bool isBoss)
        {
            if (isBoss)
            {
                return baseDifficulty == CpuDifficulty.Easy
                    ? CpuDifficulty.Medium
                    : CpuDifficulty.Hard;
            }

            return baseDifficulty switch
            {
                CpuDifficulty.Easy => matchIndex >= 4 ? CpuDifficulty.Medium : CpuDifficulty.Easy,
                CpuDifficulty.Medium => matchIndex >= 4 ? CpuDifficulty.Hard : CpuDifficulty.Medium,
                _ => CpuDifficulty.Hard
            };
        }

        private static FighterData FindSecretBoss(FighterRoster roster)
        {
            FighterData byName = null;
            for (int i = 0; i < roster.Count; i++)
            {
                FighterData f = roster.GetFighter(i);
                if (f == null) continue;
                if (f.isSecretBoss)
                {
                    return f;
                }
                if (string.Equals(f.fighterName, "Ibaneis", StringComparison.OrdinalIgnoreCase))
                {
                    byName = f;
                }
            }
            return byName;
        }

        private static string ResolveStageDisplayName(StageRoster stageRoster, string sceneName)
        {
            if (stageRoster != null)
            {
                StageData stage = stageRoster.FindBySceneName(sceneName);
                if (stage != null && !string.IsNullOrWhiteSpace(stage.displayName))
                {
                    return stage.displayName.ToUpperInvariant();
                }
            }

            return sceneName switch
            {
                FavelaSceneName => "FAVELA CARIOCA",
                UndfSceneName => "FRENTE DA UnDF",
                BossStageSceneName => "BLOCO DE CELAS",
                _ => sceneName.ToUpperInvariant()
            };
        }
    }
}
