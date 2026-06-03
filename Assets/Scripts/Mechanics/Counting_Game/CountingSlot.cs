using UnityEngine;

namespace KidGame.Mechanics.Counting
{
    /// <summary>
    /// One row in the counting game.
    ///
    /// Expected prefab structure:
    /// <code>
    /// CountingSlot (root — this script)
    /// ├── ObjectGrid   (Transform with GridLayoutGroup — objects spawn here)
    /// └── AnswerZone   (GameObject with AnswerDropZone component)
    /// </code>
    /// </summary>
    public class CountingSlot : MonoBehaviour
    {
        [Tooltip("Parent for the spawned object icons (should have a GridLayoutGroup).")]
        [SerializeField] private Transform objectGrid;

        [Tooltip("The answer drop target inside this slot.")]
        [SerializeField] private AnswerDropZone dropZone;

        /// <summary>The object count this slot was set up with.</summary>
        public int CorrectCount { get; private set; }

        /// <summary>
        /// Spawns <paramref name="count"/> copies of <paramref name="objectPrefab"/>
        /// into the object grid and configures the answer drop zone.
        /// </summary>
        public void Setup(GameObject objectPrefab, int count, CountingGameManager manager)
        {
            CorrectCount = count;

            for (int i = 0; i < count; i++)
                Instantiate(objectPrefab, objectGrid);

            dropZone.Setup(count, this, manager);
        }
    }
}
