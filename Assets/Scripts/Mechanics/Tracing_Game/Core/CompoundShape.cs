using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Mechanics.Tracing
{
    public class CompoundShape : MonoBehaviour
    {
        public List<Shape> shapes = new List<Shape>();

        public int GetCurrentShapeIndex()
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].completed) continue;
                bool isNext = true;
                for (int j = 0; j < i; j++)
                    if (!shapes[j].completed) { isNext = false; break; }
                if (isNext) return i;
            }
            return -1;
        }

        public int GetShapeIndexByInstanceID(int id)
        {
            for (int i = 0; i < shapes.Count; i++)
                if (id == shapes[i].GetInstanceID()) return i;
            return -1;
        }

        public bool IsCompleted()
        {
            foreach (var s in shapes)
                if (!s.completed) return false;
            return true;
        }
    }
}
