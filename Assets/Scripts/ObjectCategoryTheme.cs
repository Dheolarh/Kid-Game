using UnityEngine;

namespace KidGame
{
    [System.Serializable]
    public class ObjectCategoryTheme
    {
        [Tooltip("The name of this themed category (e.g. Ocean, Animals).")]
        public string themeName;

        [Tooltip("Toggle to enable this category. If multiple are enabled, the first active one will be used.")]
        public bool isEnabled;

        [Tooltip("Object prefabs belonging to this theme collection.")]
        public GameObject[] prefabs;
    }
}
