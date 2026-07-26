using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Interface
{
    /// <summary>
    /// Defines the interleaved curriculum sequence for the 261 levels.
    /// Interleaves Tracing chunks with Counting, Addition, Matching, and Recall breather levels
    /// so the player never experiences fatigue from 100+ consecutive tracing levels.
    /// Every breather level uses numbers the player has ALREADY traced in preceding levels!
    /// </summary>
    public static class LevelProgressionSequence
    {
        public static readonly int[] PlayOrder = new int[]
        {
            // 1. Tracing: single-digit (1-35)
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35,
            // 2. Break: Counting 1-5 (117-121)
            117, 118, 119, 120, 121,
            // 3. Tracing: teens (36-51)
            36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51,
            // 4. Break: Counting 6-10 (122-126)
            122, 123, 124, 125, 126,
            // 5. Tracing: twenties (52-67)
            52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67,
            // 6. Break: Counting 8-12 (127-131)
            127, 128, 129, 130, 131,
            // 7. Tracing: thirties (68-83)
            68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83,
            // 8. Break: Addition, small sums (145-150)
            145, 146, 147, 148, 149, 150,
            // 9. Tracing: forties (84-99)
            84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99,
            // 10. Break: Addition, bigger sums (154-159)
            154, 155, 156, 157, 158, 159,
            // 11. Tracing: Number 50 + closing recaps (100-104)
            100, 101, 102, 103, 104,
            // 12. Break: Matching intro (172-177)
            172, 173, 174, 175, 176, 177,
            // 13. Recall/Write phase (105-116)
            105, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116,
            // 14. Break: Addition, 3-operand (163-168)
            163, 164, 165, 166, 167, 168,
            // 15. Counting remainder (132-144)
            132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144,
            // 16. Addition remainder (151-153, 160-162, 169-171)
            151, 152, 153, 160, 161, 162, 169, 170, 171,
            // 17. Matching remainder (178-195)
            178, 179, 180, 181, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195,
            // 18. Spell Mode bonus (196-216)
            196, 197, 198, 199, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 216,
            // 19. Comparison (217-240)
            217, 218, 219, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 230, 231, 232, 233, 234, 235, 236, 237, 238, 239, 240,
            // 20. Full Game Review (241-261)
            241, 242, 243, 244, 245, 246, 247, 248, 249, 250, 251, 252, 253, 254, 255, 256, 257, 258, 259, 260, 261
        };

        /// <summary>
        /// Returns the next level number in the interleaved curriculum sequence.
        /// </summary>
        public static int GetNextLevelNumber(int currentLevelNumber)
        {
            int idx = System.Array.IndexOf(PlayOrder, currentLevelNumber);
            if (idx >= 0 && idx + 1 < PlayOrder.Length)
            {
                return PlayOrder[idx + 1];
            }
            return currentLevelNumber + 1; // Fallback if at the end
        }

        /// <summary>
        /// Returns the 1-indexed step number (1..261) for a given level number.
        /// </summary>
        public static int GetStepIndex(int levelNumber)
        {
            int idx = System.Array.IndexOf(PlayOrder, levelNumber);
            return (idx >= 0) ? (idx + 1) : levelNumber;
        }
    }
}
