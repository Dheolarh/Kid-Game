using UnityEditor;
using UnityEngine;
using KidGame.Interface;

namespace KidGame.Editor
{
    /// <summary>
    /// Draws <see cref="PageData"/> showing only the field group that matches the page's
    /// <see cref="GameType"/>, plus the shared fields (game type, premade override, dialogue lines).
    ///
    /// When the game type is changed, the groups belonging to the other game types are reset to their
    /// C# defaults so the asset only ever carries meaningful data for its current type. Because the
    /// reset is driven by a change of <c>gameType</c>, merely editing an unrelated field on an existing
    /// page never destroys data - only an explicit type switch does.
    /// </summary>
    [CustomPropertyDrawer(typeof(PageData))]
    public class PageDataDrawer : PropertyDrawer
    {
        // Shared fields, always visible.
        private const string GameTypeProp = "gameType";
        private const string OverridePremadeProp = "overrideLevelPremade";
        private const string DialogueLinesProp = "dialogueLines";
        private const string CompletionDialogueLinesProp = "completionDialogueLines";

        private const string CountingHeader = "Counting Game Settings";
        private const string AdditionHeader = "Addition Game Settings";
        private const string ComparisonHeader = "Comparison Game Settings";
        private const string MatchingHeader = "Matching Game Settings";
        private const string RecallHeader = "Recall Game Settings";
        private const string TracingHeader = "Tracing Game Settings";
        private const string MapHeader = "Map / Custom Game Settings";

        /// <summary>Returns the field names that belong to a given game type.</summary>
        private static string[] GetFieldsFor(GameType type)
        {
            switch (type)
            {
                case GameType.Counting:
                    return new[]
                    {
                        "countingSlotCount", "countingMinCount", "countingMaxCount",
                        "countingDiceMode", "countingFingerMode", "countingActiveThemeName",
                        "countingPremadeSlotPrefab", "countingPremadeSlotData"
                    };

                case GameType.Addition:
                    return new[]
                    {
                        "additionSlotCount", "additionMinPerGrid", "additionMaxPerGrid",
                        "additionDiceMode", "additionFingerMode", "additionCountAddMode",
                        "additionNumbersOnlyMode", "additionMinOperandCount", "additionMaxOperandCount",
                        "additionMinNumberValue", "additionMaxNumberValue", "additionActiveThemeName"
                    };

                case GameType.Comparison:
                    return new[]
                    {
                        "comparisonSlotCount", "comparisonMinVal", "comparisonMaxVal",
                        "comparisonMixAdditionEquations", "comparisonNumbersOnlyMode",
                        "comparisonActiveThemeName"
                    };

                case GameType.Matching:
                    return new[]
                    {
                        "matchingLeftVariant", "matchingRightVariant", "matchingSlotCount",
                        "matchingMinVal", "matchingMaxVal", "matchingShuffleLeftColumn",
                        "matchingObjectSlotPrefab"
                    };

                case GameType.Recall:
                    return new[]
                    {
                        "recallSlotCount", "recallMinSequenceLength", "recallMaxSequenceLength",
                        "recallMinStartValue", "recallMaxStartValue", "recallStep",
                        "recallCountBackwards", "recallMinConsecutiveRevealed", "recallMaxConsecutiveRevealed",
                        "recallMinConsecutiveHidden", "recallMaxConsecutiveHidden",
                        "recallIsLearningMode", "recallIsSequenceFillMode",
                        "recallColorizeTextInsteadOfBox", "recallScaleDownCardOnDrag",
                        "recallPremadeSlotPrefab", "recallPremadeSlotData"
                    };

                case GameType.Tracing:
                    return new[]
                    {
                        "tracingSpellModeActive", "tracingIsLearningMode",
                        "tracingValuesToTrace", "tracingCustomSpawnCount"
                    };

                case GameType.Map:
                    return new[] { "customGamePrefab" };

                default:
                    return new string[0];
            }
        }

        private static string GetHeaderFor(GameType type)
        {
            switch (type)
            {
                case GameType.Counting:   return CountingHeader;
                case GameType.Addition:   return AdditionHeader;
                case GameType.Comparison: return ComparisonHeader;
                case GameType.Matching:   return MatchingHeader;
                case GameType.Recall:     return RecallHeader;
                case GameType.Tracing:    return TracingHeader;
                case GameType.Map:        return MapHeader;
                default:                  return "Game Settings";
            }
        }

        /// <summary>All field names owned by any game type, in declaration order.</summary>
        private static readonly string[] AllGameTypeFields = BuildAllFields();

        private static string[] BuildAllFields()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (GameType t in System.Enum.GetValues(typeof(GameType)))
            {
                list.AddRange(GetFieldsFor(t));
            }
            return list.ToArray();
        }

        /// <summary>Reset every field that does not belong to <paramref name="keep"/>, then mark the object dirty.</summary>
        private static void ResetIrrelevantGroups(SerializedProperty property, GameType keep, GameType previousType)
        {
            var keepSet = new System.Collections.Generic.HashSet<string>(GetFieldsFor(keep));

            // Record an undo step so Ctrl+Z restores the discarded values. Without this, switching
            // Counting -> Addition -> Counting would silently lose the original counting settings.
            var target = property.serializedObject.targetObject;
            if (target != null)
            {
                Undo.RecordObject(target, $"Change Page Game Type ({previousType} → {keep})");
            }

            foreach (string fieldName in AllGameTypeFields)
            {
                if (keepSet.Contains(fieldName)) continue;

                var prop = property.FindPropertyRelative(fieldName);
                if (prop == null) continue;

                ResetPropertyToDefault(prop);
            }

            property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }

        /// <summary>
        /// Resets a serialized property to the value declared by its C# field initializer in
        /// <see cref="PageData"/>. Plain zero/false would be wrong for fields with non-trivial defaults
        /// such as <c>countingSlotCount = 5</c> or <c>recallIsLearningMode = true</c>.
        /// </summary>
        private static void ResetPropertyToDefault(SerializedProperty prop)
        {
            // Field-specific defaults, mirroring PageData's initializers.
            switch (prop.name)
            {
                case "countingSlotCount":          prop.intValue = 5; break;
                case "countingMinCount":           prop.intValue = 1; break;
                case "countingMaxCount":           prop.intValue = 20; break;

                case "additionSlotCount":          prop.intValue = 5; break;
                case "additionMinPerGrid":         prop.intValue = 1; break;
                case "additionMaxPerGrid":         prop.intValue = 12; break;
                case "additionMinOperandCount":    prop.intValue = 2; break;
                case "additionMaxOperandCount":    prop.intValue = 3; break;
                case "additionMinNumberValue":     prop.intValue = 1; break;
                case "additionMaxNumberValue":     prop.intValue = 50; break;

                case "comparisonSlotCount":        prop.intValue = 3; break;
                case "comparisonMinVal":           prop.intValue = 1; break;
                case "comparisonMaxVal":           prop.intValue = 10; break;
                case "comparisonMixAdditionEquations":
                    prop.boolValue = true; return;

                case "matchingSlotCount":          prop.intValue = 5; break;
                case "matchingMinVal":             prop.intValue = 1; break;
                case "matchingMaxVal":             prop.intValue = 10; break;

                case "recallSlotCount":            prop.intValue = 4; break;
                case "recallMinSequenceLength":    prop.intValue = 5; break;
                case "recallMaxSequenceLength":    prop.intValue = 10; break;
                case "recallMinStartValue":        prop.intValue = 1; break;
                case "recallMaxStartValue":        prop.intValue = 20; break;
                case "recallStep":                 prop.intValue = 1; break;
                case "recallMinConsecutiveRevealed": prop.intValue = 1; break;
                case "recallMaxConsecutiveRevealed": prop.intValue = 2; break;
                case "recallMinConsecutiveHidden": prop.intValue = 1; break;
                case "recallMaxConsecutiveHidden": prop.intValue = 2; break;
                case "recallIsLearningMode":       prop.boolValue = true; return;

                case "tracingIsLearningMode":      prop.boolValue = true; return;
                case "tracingCustomSpawnCount":    prop.intValue = 1; break;

                // Enum with a non-zero default initializer.
                case "matchingLeftVariant":
                case "matchingRightVariant":
                    prop.enumValueIndex = 0; return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = 0;
                    break;

                case SerializedPropertyType.Float:
                    prop.floatValue = 0f;
                    break;

                case SerializedPropertyType.Boolean:
                    prop.boolValue = false;
                    break;

                case SerializedPropertyType.String:
                    prop.stringValue = "";
                    break;

                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0;
                    break;

                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null;
                    break;

                case SerializedPropertyType.Color:
                    prop.colorValue = Color.white;
                    break;

                case SerializedPropertyType.Generic:
                    // Lists (e.g. tracingValuesToTrace) - clear the array contents.
                    if (prop.isArray)
                    {
                        prop.ClearArray();
                    }
                    break;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var gameTypeProp = property.FindPropertyRelative(GameTypeProp);
            if (gameTypeProp == null)
            {
                // Field layout changed unexpectedly - fall back to the default rendering.
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            float y = position.y;

            // ── Foldout header for this page ────────────────────────────────────
            var foldoutRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                var previousType = (GameType)gameTypeProp.enumValueIndex;
                var typePropRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(gameTypeProp, true));
                EditorGUI.PropertyField(typePropRect, gameTypeProp, true);
                y += typePropRect.height + EditorGUIUtility.standardVerticalSpacing;

                var newType = (GameType)gameTypeProp.enumValueIndex;

                // Type switched: drop the groups that are no longer relevant.
                if (newType != previousType)
                {
                    ResetIrrelevantGroups(property, newType, previousType);
                }

                // ── Shared fields ──────────────────────────────────────────────
                y = DrawRelative(position, y, property, OverridePremadeProp);
                y = DrawRelative(position, y, property, DialogueLinesProp);
                y = DrawRelative(position, y, property, CompletionDialogueLinesProp);

                // ── Header for the active game type ────────────────────────────
                var headerRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(headerRect, GetHeaderFor(newType), EditorStyles.boldLabel);
                y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                // ── Only the fields that belong to this game type ──────────────
                foreach (string fieldName in GetFieldsFor(newType))
                {
                    y = DrawRelative(position, y, property, fieldName);
                }

                // Tracing has a convenience button in the Level Creator, not here.
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Draws a child property by name and returns the next free Y coordinate.</summary>
        private static float DrawRelative(Rect position, float y, SerializedProperty parent, string childName)
        {
            var child = parent.FindPropertyRelative(childName);
            if (child == null) return y;

            float height = EditorGUI.GetPropertyHeight(child, true);
            var rect = new Rect(position.x, y, position.width, height);
            EditorGUI.PropertyField(rect, child, true);
            return y + height + EditorGUIUtility.standardVerticalSpacing;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var gameTypeProp = property.FindPropertyRelative(GameTypeProp);
            if (gameTypeProp == null || !property.isExpanded)
            {
                return base.GetPropertyHeight(property, label);
            }

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // foldout
            height += EditorGUI.GetPropertyHeight(gameTypeProp, true) + EditorGUIUtility.standardVerticalSpacing;

            foreach (string fieldName in new[]
                     {
                         OverridePremadeProp, DialogueLinesProp, CompletionDialogueLinesProp
                     })
            {
                var child = property.FindPropertyRelative(fieldName);
                if (child != null)
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 3f; // header + spacing

            var newType = (GameType)gameTypeProp.enumValueIndex;
            foreach (string fieldName in GetFieldsFor(newType))
            {
                var child = property.FindPropertyRelative(fieldName);
                if (child != null)
                {
                    height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height;
        }
    }
}
