using UnityEngine;

namespace KidGame.Interface
{
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "Level Select/Level Data")]
    public class LevelData : ScriptableObject
    {
        public string levelName;
        public string sceneToLoad;
        public bool isUnlockedByDefault = false;
        
        [Header("Optional Stats")]
        public Sprite levelIcon;
    }
}