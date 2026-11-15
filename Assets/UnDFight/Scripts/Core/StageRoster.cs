using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Ordered roster of available arenas displayed in the Stage Select step.
    /// </summary>
    [CreateAssetMenu(menuName = "UnDFight/Stage Roster", fileName = "StageRoster")]
    public class StageRoster : ScriptableObject
    {
        [SerializeField] private List<StageData> stages = new List<StageData>();

        public IReadOnlyList<StageData> Stages => stages;
        public int Count => stages != null ? stages.Count : 0;

        public StageData GetStage(int index)
        {
            if (stages == null || index < 0 || index >= stages.Count)
            {
                return null;
            }
            return stages[index];
        }

        public int IndexOf(StageData stage)
        {
            if (stages == null || stage == null)
            {
                return -1;
            }
            return stages.IndexOf(stage);
        }

        public StageData FindBySceneName(string sceneName)
        {
            if (stages == null || string.IsNullOrWhiteSpace(sceneName))
            {
                return null;
            }

            for (int i = 0; i < stages.Count; i++)
            {
                StageData s = stages[i];
                if (s != null && string.Equals(s.sceneName, sceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }
    }
}
