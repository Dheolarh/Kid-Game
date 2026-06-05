using UnityEngine;

namespace KidGame.Mechanics.Counting
{
    public class CountingSlot : MonoBehaviour
    {
        [Tooltip("Parent for the spawned object icons (should have a GridLayoutGroup).")]
        [SerializeField] private Transform objectGrid;

        [Tooltip("The answer drop target inside this slot.")]
        [SerializeField] private AnswerDropZone dropZone;

        /// <summary>The object count this slot was set up with.</summary>
        public int CorrectCount { get; private set; }

        public void Setup(GameObject objectPrefab, int count, CountingGameManager manager)
        {
            CorrectCount = count;

            for (int i = 0; i < count; i++)
            {
                var obj = Instantiate(objectPrefab, objectGrid);

                // Ensure every spawned icon responds to taps with a bounce animation
                if (obj.GetComponent<CountingObject>() == null)
                    obj.AddComponent<CountingObject>();
            }

            dropZone.Setup(count, this, manager);
        }
    }
}
