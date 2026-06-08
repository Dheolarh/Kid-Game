using System.Collections.Generic;
using UnityEngine;
using KidGame.Mechanics.Counting;  

namespace KidGame.Mechanics.Addition
{
    public class AdditionSlot : MonoBehaviour
    {
        [Tooltip("Parent for the LEFT group of objects (GridLayoutGroup).")]
        [SerializeField] private Transform leftGrid;

        [Tooltip("Parent for the RIGHT group of objects (GridLayoutGroup).")]
        [SerializeField] private Transform rightGrid;

        [Tooltip("The answer drop target for this slot.")]
        [SerializeField] private AnswerDropZone dropZone;

        public int CorrectSum { get; private set; }

        /// <param name="leftPrefab">Object icon for the left grid.</param>
        /// <param name="leftCount">How many left objects to spawn (1–12).</param>
        /// <param name="rightPrefab">Object icon for the right grid.</param>
        /// <param name="rightCount">How many right objects to spawn (1–12).</param>
        /// <param name="manager">Owning manager — notified on correct answer.</param>
        public void Setup(GameObject leftPrefab,  int leftCount,
                          GameObject rightPrefab, int rightCount,
                          AdditionGameManager manager)
        {
            CorrectSum = leftCount + rightCount;

            SpawnObjects(leftPrefab,  leftCount,  leftGrid);
            SpawnObjects(rightPrefab, rightCount, rightGrid);

            // Lambda keeps AnswerDropZone decoupled from Addition-specific types
            dropZone.Setup(CorrectSum, () => manager.OnSlotAnswered());
        }

        public void Setup(List<GameObject> leftDicePrefabs,
                          List<GameObject> rightDicePrefabs,
                          int leftSum, int rightSum,
                          AdditionGameManager manager)
        {
            CorrectSum = leftSum + rightSum;

            foreach (var prefab in leftDicePrefabs)
                SpawnObject(prefab, leftGrid);

            foreach (var prefab in rightDicePrefabs)
                SpawnObject(prefab, rightGrid);

            dropZone.Setup(CorrectSum, () => manager.OnSlotAnswered());
        }

        private static void SpawnObjects(GameObject prefab, int count, Transform parent)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnObject(prefab, parent);
            }
        }

        private static void SpawnObject(GameObject prefab, Transform parent)
        {
            var obj = Object.Instantiate(prefab, parent);

            // Tap animation — added automatically, no prefab changes needed
            if (obj.GetComponent<CountingObject>() == null)
                obj.AddComponent<CountingObject>();
        }
    }
}
