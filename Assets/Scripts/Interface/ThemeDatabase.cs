using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Interface
{
    [System.Serializable]
    public class ThemePreset
    {
        public string themeName;
        public Color themeColor = Color.white;
        public Sprite backgroundSprite;
    }

    [CreateAssetMenu(fileName = "ThemeDatabase", menuName = "KidGame/Theme Database")]
    public class ThemeDatabase : ScriptableObject
    {
        public List<ThemePreset> presets = new List<ThemePreset>();

        public ThemePreset GetPreset(string name)
        {
            if (presets == null || string.IsNullOrEmpty(name)) return null;
            foreach (var preset in presets)
            {
                if (preset != null && preset.themeName == name)
                {
                    return preset;
                }
            }
            return null;
        }
    }
}
