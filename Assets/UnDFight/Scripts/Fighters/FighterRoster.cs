using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Ordered roster of playable fighters read by CharacterSelect and Training scenes.
    /// Supports arbitrary roster sizes (8+ fighters) without code changes.
    /// </summary>
    [CreateAssetMenu(fileName = "FighterRoster", menuName = "UnDFight/Fighter Roster")]
    public class FighterRoster : ScriptableObject
    {
        [SerializeField] private List<FighterData> fighters = new List<FighterData>();

        public IReadOnlyList<FighterData> Fighters => fighters;
        public int Count => fighters.Count;

        public FighterData GetFighter(int index)
        {
            if (fighters.Count == 0)
            {
                return null;
            }

            int wrapped = ((index % fighters.Count) + fighters.Count) % fighters.Count;
            return fighters[wrapped];
        }

        public int IndexOf(FighterData data)
        {
            return fighters.IndexOf(data);
        }
    }
}
