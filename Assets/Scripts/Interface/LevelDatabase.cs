using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Interface
{
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Level Select/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        public List<LevelData> allLevels = new List<LevelData>();
    }
}