
// This version has better, more stable syncing on window resizing,
// ...but requires A LOT more code (hundreds of line) to maintain.

// It's experimental:

// -> not worth the potential tech debt. for an 'OCD' fix over a cosmetic issue.

// I'm only keeping it for reference.
// > I'll probably not be updating it.

//#define GAMEVIEW_INTEGRATED_SNAPSHOT_TOOL_FULL
#if GAMEVIEW_INTEGRATED_SNAPSHOT_TOOL_FULL

using System;

using System.Collections;
using System.Collections.Generic;

using System.Reflection;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

namespace Mirza.SNAP
{
    // ...

    [InitializeOnLoad]
    static class GameViewIntegratedSnapshotTool
    {
        // ...

        const string SnapshotPreviewContainerName = "game-view-snapshot-preview-container";
        const string SnapshotToolbarAnchorName = "game-view-snapshot-toolbar-anchor";

        const double gameViewScanInterval = 0.5;

        const float toolbarButtonIconSize = 16.0f;
        const float toolbarButtonContentSpacing = 3.0f;

        const float snapshotButtonWidth = 60.0f;
        const float previewButtonWidth = 76.0f;

        const float previewOpacitySliderContainerWidth = 96.0f;
        const float previewOpacitySliderWidth = 80.0f;

        const float snapshotToolbarWidth = snapshotButtonWidth + previewButtonWidth + previewOpacitySliderContainerWidth;

        const float playFocusedPopupWidth = 110.0f;

        // ...

        const BindingFlags instancePublicBindingFlags = BindingFlags.Instance | BindingFlags.Public;
        const BindingFlags instanceNonPublicBindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        const BindingFlags instanceAnyVisibilityBindingFlags = instancePublicBindingFlags | instanceNonPublicBindingFlags;

        const BindingFlags staticNonPublicBindingFlags = BindingFlags.Static | BindingFlags.NonPublic;

        static readonly Type gameViewType = GetRequiredType("UnityEditor.GameView,UnityEditor");
        static readonly Type hostViewType = GetRequiredType("UnityEditor.HostView,UnityEditor");

        static readonly Type guiLayoutEntryType = GetRequiredType("UnityEngine.GUILayoutEntry", typeof(GUILayoutUtility).Assembly);
        static readonly Type guiLayoutGroupType = GetRequiredType("UnityEngine.GUILayoutGroup", typeof(GUILayoutUtility).Assembly);

        static readonly FieldInfo editorWindowParentField = GetRequiredMember<FieldInfo>(typeof(EditorWindow), "m_Parent", instanceNonPublicBindingFlags);
        static readonly FieldInfo hostViewOnGUIField = GetRequiredMember<FieldInfo>(hostViewType, "m_OnGUI", instanceNonPublicBindingFlags);

        static readonly PropertyInfo guiLayoutTopLevelProperty = GetRequiredMember<PropertyInfo>(typeof(GUILayoutUtility), "topLevel", staticNonPublicBindingFlags);

        static readonly FieldInfo guiLayoutGroupEntriesField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "entries", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutGroupIsVerticalField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "isVertical", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutGroupResetCoordinatesField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "resetCoords", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutGroupSpacingField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "spacing", instancePublicBindingFlags);

        static readonly FieldInfo guiLayoutGroupChildMinimumWidthField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "m_ChildMinWidth", instanceNonPublicBindingFlags);
        static readonly FieldInfo guiLayoutGroupChildMaximumWidthField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "m_ChildMaxWidth", instanceNonPublicBindingFlags);
        static readonly FieldInfo guiLayoutGroupStretchableCountXField = GetRequiredMember<FieldInfo>(guiLayoutGroupType, "m_StretchableCountX", instanceNonPublicBindingFlags);

        static readonly FieldInfo guiLayoutEntryMinimumWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "minWidth", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryMaximumWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "maxWidth", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryRectangleField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "rect", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryStretchWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "stretchWidth", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryConsideredForMarginField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "consideredForMargin", instancePublicBindingFlags);

        static readonly PropertyInfo guiLayoutEntryMarginLeftProperty = GetRequiredMember<PropertyInfo>(guiLayoutEntryType, "marginLeft", instancePublicBindingFlags);
        static readonly PropertyInfo guiLayoutEntryMarginRightProperty = GetRequiredMember<PropertyInfo>(guiLayoutEntryType, "marginRight", instancePublicBindingFlags);
        static readonly PropertyInfo guiLayoutEntryStyleProperty = GetRequiredMember<PropertyInfo>(guiLayoutEntryType, "style", instancePublicBindingFlags);

        static readonly FieldInfo gameViewRenderTextureField = GetRequiredMember<FieldInfo>(gameViewType, "m_RenderTexture", instanceNonPublicBindingFlags);
        static readonly FieldInfo gameViewZoomAreaField = GetRequiredMember<FieldInfo>(gameViewType, "m_ZoomArea", instanceNonPublicBindingFlags);

        static readonly PropertyInfo zoomAreaDrawRectProperty = GetRequiredMember<PropertyInfo>(gameViewZoomAreaField.FieldType, "drawRect", instanceAnyVisibilityBindingFlags);
        static readonly PropertyInfo deviceFlippedTargetInViewProperty = GetRequiredMember<PropertyInfo>(gameViewType, "deviceFlippedTargetInView", instanceNonPublicBindingFlags);
        static readonly PropertyInfo guiBlitMaterialProperty = GetRequiredMember<PropertyInfo>(typeof(GUI), "blitMaterial", staticNonPublicBindingFlags);

        static readonly MethodInfo drawSnapshotMethod = GetRequiredMember<MethodInfo>(typeof(SnapshotController), "DrawSnapshot", instanceNonPublicBindingFlags);

        static readonly DrawTextureWithHdrSupport drawTextureWithHdrSupport = (DrawTextureWithHdrSupport)Delegate.CreateDelegate(

            typeof(DrawTextureWithHdrSupport),
            GetRequiredMember<MethodInfo>(typeof(EditorGUIUtility), "DrawTextureHdrSupport", staticNonPublicBindingFlags)
        );

        static readonly Dictionary<EditorWindow, SnapshotController> snapshotControllers = new();
        static readonly List<EditorWindow> closedGameViews = new();

        static double nextGameViewScanTime;

        // ...

        static GameViewIntegratedSnapshotTool()
        {
            EditorApplication.update += UpdateOpenGameViews;
            EditorApplication.delayCall += UpdateOpenGameViews;

            AssemblyReloadEvents.beforeAssemblyReload += DisposeSnapshotControllers;
            EditorApplication.quitting += DisposeSnapshotControllers;
        }

        // ...

        static Type GetRequiredType(string typeName, Assembly assembly = null)
        {
            Type requiredType = assembly == null ? Type.GetType(typeName) : assembly.GetType(typeName);
            return requiredType ?? throw new TypeLoadException($"{typeName} type was not found.");
        }

        static TMember GetRequiredMember<TMember>(Type declaringType, string memberName, BindingFlags bindingFlags)
            where TMember : MemberInfo
        {
            MemberInfo requiredMember;

            if (typeof(TMember) == typeof(FieldInfo))
            {
                requiredMember = declaringType.GetField(memberName, bindingFlags);
            }
            else if (typeof(TMember) == typeof(PropertyInfo))
            {
                requiredMember = declaringType.GetProperty(memberName, bindingFlags);
            }
            else
            {
                requiredMember = declaringType.GetMethod(memberName, bindingFlags);
            }

            return (TMember)(requiredMember ?? throw new MissingMemberException(declaringType.FullName, memberName));
        }

        static void UpdateOpenGameViews()
        {
            if (EditorApplication.timeSinceStartup < nextGameViewScanTime)
            {
                return;
            }

            nextGameViewScanTime = EditorApplication.timeSinceStartup + gameViewScanInterval;
            closedGameViews.Clear();

            foreach (KeyValuePair<EditorWindow, SnapshotController> snapshotController in snapshotControllers)
            {
                if (snapshotController.Key != null)
                {
                    continue;
                }

                snapshotController.Value.Dispose();
                closedGameViews.Add(snapshotController.Key);
            }

            foreach (EditorWindow closedGameView in closedGameViews)
            {
                snapshotControllers.Remove(closedGameView);
            }

            Object[] openGameViewObjects = Resources.FindObjectsOfTypeAll(gameViewType);

            foreach (Object openGameViewObject in openGameViewObjects)
            {
                EditorWindow openGameView = (EditorWindow)openGameViewObject;

                if (snapshotControllers.TryGetValue(openGameView, out SnapshotController snapshotController))
                {
                    snapshotController.UpdateOnGUIAttachment();
                    continue;
                }

                snapshotControllers.Add(openGameView, new SnapshotController(openGameView));
            }
        }

        delegate void DrawTextureWithHdrSupport(

            Rect screenRect, Texture texture, Rect sourceRect,

            int leftBorder,
            int rightBorder,
            int topBorder,
            int bottomBorder,

            Color colour, Material material,

            int pass, bool resetLinearToSrgbIfHdrActive
        );

        // ...

        static RenderTexture GetGameViewRenderTexture(EditorWindow gameView)
        {
            return (RenderTexture)gameViewRenderTextureField.GetValue(gameView);
        }

        static Rect GetGameViewDrawRect(EditorWindow gameView)
        {
            object gameViewZoomArea = gameViewZoomAreaField.GetValue(gameView);
            return (Rect)zoomAreaDrawRectProperty.GetValue(gameViewZoomArea);
        }

        static Rect GetDeviceFlippedTargetInView(EditorWindow gameView)
        {
            return (Rect)deviceFlippedTargetInViewProperty.GetValue(gameView);
        }

        // ...

        static void SetFixedWidth(VisualElement visualElement, float width)
        {
            visualElement.style.width = width;
            visualElement.style.minWidth = width;
            visualElement.style.maxWidth = width;
        }

        static ToolbarButton CreateToolbarButton(Action clicked, string tooltip, float width, string iconName, string buttonText)
        {
            ToolbarButton toolbarButton = new(clicked)
            {
                pickingMode = PickingMode.Position,
                tooltip = tooltip
            };

            SetFixedWidth(toolbarButton, width);

            toolbarButton.style.alignItems = Align.Center;
            toolbarButton.style.justifyContent = Justify.Center;

            VisualElement buttonContent = new()
            {
                pickingMode = PickingMode.Ignore
            };

            buttonContent.style.flexGrow = 1.0f;
            buttonContent.style.flexDirection = FlexDirection.Row;
            buttonContent.style.alignItems = Align.Center;
            buttonContent.style.justifyContent = Justify.Center;

            Image buttonIcon = new()
            {
                image = EditorGUIUtility.IconContent(iconName).image,

                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit
            };

            buttonIcon.style.width = toolbarButtonIconSize;
            buttonIcon.style.height = toolbarButtonIconSize;
            buttonIcon.style.flexShrink = 0.0f;

            Label buttonLabel = new(buttonText)
            {
                pickingMode = PickingMode.Ignore
            };

            buttonLabel.style.marginLeft = toolbarButtonContentSpacing;

            buttonContent.Add(buttonIcon);
            buttonContent.Add(buttonLabel);

            toolbarButton.Add(buttonContent);
            return toolbarButton;
        }

        // ...

        static bool IsPlayFocusedLayoutEntry(object layoutEntry)
        {
            if (guiLayoutGroupType.IsInstanceOfType(layoutEntry))
            {
                return false;
            }

            float minimumWidth = (float)guiLayoutEntryMinimumWidthField.GetValue(layoutEntry);
            float maximumWidth = (float)guiLayoutEntryMaximumWidthField.GetValue(layoutEntry);

            GUIStyle layoutStyle = (GUIStyle)guiLayoutEntryStyleProperty.GetValue(layoutEntry);

            return Mathf.Approximately(minimumWidth, playFocusedPopupWidth)
                && Mathf.Approximately(maximumWidth, playFocusedPopupWidth)

                && string.Equals(layoutStyle.name, EditorStyles.toolbarDropDown.name, StringComparison.Ordinal);
        }

        static bool TryGetPlayFocusedLayoutEntries(

            object layoutEntry,

            out object toolbarSpaceLayoutEntry,

            out object toolbarLayoutGroup,
            out int playFocusedLayoutEntryIndex
        )
        {
            toolbarSpaceLayoutEntry = null;

            toolbarLayoutGroup = null;
            playFocusedLayoutEntryIndex = -1;

            if (!guiLayoutGroupType.IsInstanceOfType(layoutEntry))
            {
                return false;
            }

            IList childLayoutEntries = (IList)guiLayoutGroupEntriesField.GetValue(layoutEntry);

            for (int i = 1; i < childLayoutEntries.Count; ++i)
            {
                object currentLayoutEntry = childLayoutEntries[i - 0];

                if (!IsPlayFocusedLayoutEntry(currentLayoutEntry))
                {
                    continue;
                }

                object currentToolbarSpaceLayoutEntry = childLayoutEntries[i - 1];

                if (guiLayoutGroupType.IsInstanceOfType(currentToolbarSpaceLayoutEntry) || ((int)guiLayoutEntryStretchWidthField.GetValue(currentToolbarSpaceLayoutEntry) == 0))
                {
                    continue;
                }

                toolbarSpaceLayoutEntry = currentToolbarSpaceLayoutEntry;

                toolbarLayoutGroup = layoutEntry;
                playFocusedLayoutEntryIndex = i;

                return true;
            }

            foreach (object childLayoutEntry in childLayoutEntries)
            {
                if (TryGetPlayFocusedLayoutEntries(

                    childLayoutEntry,

                    out toolbarSpaceLayoutEntry,

                    out toolbarLayoutGroup,
                    out playFocusedLayoutEntryIndex
                ))
                {
                    return true;
                }
            }

            return false;
        }

        static void GetPlayFocusedLayoutEntries(

            out object toolbarSpaceLayoutEntry,

            out object toolbarLayoutGroup,
            out int playFocusedLayoutEntryIndex
        )
        {
            object topLevelLayoutEntry = guiLayoutTopLevelProperty.GetValue(null);

            if (!TryGetPlayFocusedLayoutEntries(

                topLevelLayoutEntry,

                out toolbarSpaceLayoutEntry,

                out toolbarLayoutGroup,
                out playFocusedLayoutEntryIndex
            ))
            {
                throw new InvalidOperationException("Unity Game View Play Focused toolbar entry was not found.");
            }
        }

        // ...

        static void DisposeSnapshotControllers()
        {
            EditorApplication.update -= UpdateOpenGameViews;
            EditorApplication.delayCall -= UpdateOpenGameViews;

            foreach (SnapshotController snapshotController in snapshotControllers.Values)
            {
                snapshotController.Dispose();
            }

            snapshotControllers.Clear();
        }

        // ...

        sealed class SnapshotController
        {
            readonly EditorWindow gameView;
            readonly VisualElement gameViewRoot;

            readonly VisualElement snapshotToolbarAnchor;
            readonly Toolbar snapshotToolbar;

            readonly ToolbarButton previewButton;
            readonly Slider previewOpacitySlider;

            readonly Delegate drawSnapshotDelegate;

            float horizontalToolbarLayoutContentX;
            float horizontalToolbarLayoutContentWidthInset;
            float horizontalToolbarLayoutContentHorizontalMargins;

            float horizontalToolbarLayoutPlayFocusedMinimumOffset;
            float horizontalToolbarLayoutPlayFocusedWidthRange;

            float horizontalToolbarLayoutTotalSpacing;
            float horizontalToolbarLayoutChildMinimumWidth;
            float horizontalToolbarLayoutChildMaximumWidth;

            int horizontalToolbarLayoutPlayFocusedStretchWidth;
            int horizontalToolbarLayoutStretchableCount;

            bool horizontalToolbarLayoutValid;

            object attachedHostView;

            RenderTexture snapshotTexture;
            bool previewVisible;

            public SnapshotController(EditorWindow gameView)
            {
                this.gameView = gameView;
                gameViewRoot = gameView.rootVisualElement;

                drawSnapshotDelegate = Delegate.CreateDelegate(hostViewOnGUIField.FieldType, this, drawSnapshotMethod);

                gameViewRoot.Q<VisualElement>(SnapshotPreviewContainerName)?.RemoveFromHierarchy();
                gameViewRoot.Q<VisualElement>(SnapshotToolbarAnchorName)?.RemoveFromHierarchy();

                snapshotToolbarAnchor = new VisualElement
                {
                    name = SnapshotToolbarAnchorName,
                    pickingMode = PickingMode.Ignore
                };

                snapshotToolbarAnchor.style.position = Position.Absolute;

                snapshotToolbarAnchor.style.left = 0.0f;
                snapshotToolbarAnchor.style.right = 0.0f;
                snapshotToolbarAnchor.style.top = 0.0f;

                snapshotToolbarAnchor.style.height = EditorGUIUtility.singleLineHeight + 2.0f;

                snapshotToolbarAnchor.style.flexDirection = FlexDirection.Row;
                snapshotToolbarAnchor.style.justifyContent = Justify.FlexStart;

                snapshotToolbarAnchor.style.alignItems = Align.FlexStart;

                snapshotToolbar = new Toolbar
                {
                    pickingMode = PickingMode.Position
                };

                snapshotToolbar.style.position = Position.Absolute;

                snapshotToolbar.style.left = 0.0f;
                snapshotToolbar.style.width = snapshotToolbarWidth;

                snapshotToolbar.style.visibility = Visibility.Hidden;

                ToolbarButton snapButton = CreateToolbarButton(

                    CaptureSnapshot,
                    "Capture snapshot of current Game View.",

                    snapshotButtonWidth,
                    "Camera Icon", "Snap"
                );

                previewButton = CreateToolbarButton(

                    () => { },
                    "Hold to preview captured snapshot.",

                    previewButtonWidth,
                    "animationvisibilitytoggleon", "Preview"
                );

                previewButton.SetEnabled(false);

                previewButton.RegisterCallback<PointerDownEvent>(ShowSnapshot, TrickleDown.TrickleDown);
                previewButton.RegisterCallback<PointerUpEvent>(HideSnapshot, TrickleDown.TrickleDown);
                previewButton.RegisterCallback<PointerCancelEvent>(_ => HideSnapshot(), TrickleDown.TrickleDown);
                previewButton.RegisterCallback<PointerCaptureOutEvent>(_ => HideSnapshot(), TrickleDown.TrickleDown);

                VisualElement previewOpacitySliderContainer = new();

                SetFixedWidth(previewOpacitySliderContainer, previewOpacitySliderContainerWidth);

                previewOpacitySliderContainer.style.alignItems = Align.Center;
                previewOpacitySliderContainer.style.justifyContent = Justify.Center;

                previewOpacitySlider = new Slider(0.0f, 1.0f)
                {
                    tooltip = "Adjust captured snapshot preview opacity."
                };

                previewOpacitySlider.style.width = previewOpacitySliderWidth;

                previewOpacitySlider.SetValueWithoutNotify(1.0f);
                previewOpacitySlider.SetEnabled(false);

                previewOpacitySlider.RegisterValueChangedCallback(_ => gameView.Repaint());

                previewOpacitySliderContainer.Add(previewOpacitySlider);

                snapshotToolbar.Add(snapButton);
                snapshotToolbar.Add(previewButton);
                snapshotToolbar.Add(previewOpacitySliderContainer);

                snapshotToolbarAnchor.Add(snapshotToolbar);

                gameViewRoot.Add(snapshotToolbarAnchor);
                gameViewRoot.RegisterCallback<GeometryChangedEvent>(UpdateGameViewGeometry);

                UpdateOnGUIAttachment();
            }

            // ...

            static int GetDelegateInvocationCount(Delegate sourceDelegate, Delegate invocationDelegate)
            {
                if (sourceDelegate == null)
                {
                    return 0;
                }

                int invocationCount = 0;

                foreach (Delegate currentInvocationDelegate in sourceDelegate.GetInvocationList())
                {
                    if (currentInvocationDelegate.Equals(invocationDelegate))
                    {
                        ++invocationCount;
                    }
                }

                return invocationCount;
            }

            static bool HasDelegateTarget(Delegate sourceDelegate, object target)
            {
                if (sourceDelegate == null)
                {
                    return false;
                }

                foreach (Delegate currentInvocationDelegate in sourceDelegate.GetInvocationList())
                {
                    if (ReferenceEquals(currentInvocationDelegate.Target, target))
                    {
                        return true;
                    }
                }

                return false;
            }

            static Delegate RemoveDelegateInvocations(Delegate sourceDelegate, Delegate invocationDelegate)
            {
                while (GetDelegateInvocationCount(sourceDelegate, invocationDelegate) > 0)
                {
                    sourceDelegate = Delegate.Remove(sourceDelegate, invocationDelegate);
                }

                return sourceDelegate;
            }

            public void UpdateOnGUIAttachment()
            {
                object currentHostView = editorWindowParentField.GetValue(gameView);

                Delegate currentOnGUIDelegate = (currentHostView == null)
                    ? null : (Delegate)hostViewOnGUIField.GetValue(currentHostView);

                int drawSnapshotInvocationCount = GetDelegateInvocationCount(currentOnGUIDelegate, drawSnapshotDelegate);

                if (ReferenceEquals(currentHostView, attachedHostView)
                    && HasDelegateTarget(currentOnGUIDelegate, gameView)
                    && (drawSnapshotInvocationCount == 1))
                {
                    return;
                }

                DetachFromHostView();

                if (currentHostView == null)
                {
                    return;
                }

                currentOnGUIDelegate = (Delegate)hostViewOnGUIField.GetValue(currentHostView);
                drawSnapshotInvocationCount = GetDelegateInvocationCount(currentOnGUIDelegate, drawSnapshotDelegate);

                bool containedSnapshotDelegate = drawSnapshotInvocationCount > 0;

                if (containedSnapshotDelegate)
                {
                    currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, drawSnapshotDelegate);
                }

                if (!HasDelegateTarget(currentOnGUIDelegate, gameView))
                {
                    if (containedSnapshotDelegate)
                    {
                        hostViewOnGUIField.SetValue(currentHostView, currentOnGUIDelegate);
                    }

                    return;
                }

                hostViewOnGUIField.SetValue(currentHostView, Delegate.Combine(currentOnGUIDelegate, drawSnapshotDelegate));
                attachedHostView = currentHostView;
            }

            // ...

            void DetachFromHostView()
            {
                if (attachedHostView != null)
                {
                    Delegate currentOnGUIDelegate = (Delegate)hostViewOnGUIField.GetValue(attachedHostView);

                    if (GetDelegateInvocationCount(currentOnGUIDelegate, drawSnapshotDelegate) > 0)
                    {
                        currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, drawSnapshotDelegate);
                        hostViewOnGUIField.SetValue(attachedHostView, currentOnGUIDelegate);
                    }
                }

                attachedHostView = null;
                previewVisible = false;

                horizontalToolbarLayoutValid = false;
                snapshotToolbar.style.visibility = Visibility.Hidden;
            }

            // ...

            void CaptureSnapshot()
            {
                RenderTexture gameViewRenderTexture = GetGameViewRenderTexture(gameView);

                if ((gameViewRenderTexture == null) || !gameViewRenderTexture.IsCreated())
                {
                    return;
                }

                RenderTexture capturedSnapshot = new(gameViewRenderTexture.descriptor)
                {
                    name = "Game View Snapshot",
                    hideFlags = HideFlags.HideAndDontSave,

                    filterMode = gameViewRenderTexture.filterMode,

                    wrapMode = gameViewRenderTexture.wrapMode,
                    anisoLevel = gameViewRenderTexture.anisoLevel
                };

                if (!capturedSnapshot.Create())
                {
                    Object.DestroyImmediate(capturedSnapshot);
                    return;
                }

                try
                {
                    Graphics.CopyTexture(gameViewRenderTexture, capturedSnapshot);
                }
                catch
                {
                    Object.DestroyImmediate(capturedSnapshot);
                    throw;
                }

                if (snapshotTexture != null)
                {
                    Object.DestroyImmediate(snapshotTexture);
                }

                snapshotTexture = capturedSnapshot;

                previewButton.SetEnabled(true);
                previewOpacitySlider.SetEnabled(true);
            }

            // ...

            void ShowSnapshot(PointerDownEvent eventData)
            {
                if (eventData.button != 0)
                {
                    return;
                }

                previewVisible = true;
                gameView.Repaint();
            }

            // ...

            void HideSnapshot(PointerUpEvent eventData)
            {
                if (eventData.button != 0)
                {
                    return;
                }

                HideSnapshot();
            }

            void HideSnapshot()
            {
                previewVisible = false;
                gameView.Repaint();
            }

            readonly struct HorizontalToolbarLayoutEntry
            {
                public readonly float minimumWidth;
                public readonly float maximumWidth;

                public readonly int stretchWidth;

                public readonly int marginLeft;
                public readonly int marginRight;

                public readonly bool consideredForMargin;

                public HorizontalToolbarLayoutEntry(object layoutEntry)
                {
                    minimumWidth = (float)guiLayoutEntryMinimumWidthField.GetValue(layoutEntry);
                    maximumWidth = (float)guiLayoutEntryMaximumWidthField.GetValue(layoutEntry);

                    stretchWidth = (int)guiLayoutEntryStretchWidthField.GetValue(layoutEntry);

                    marginLeft = (int)guiLayoutEntryMarginLeftProperty.GetValue(layoutEntry);
                    marginRight = (int)guiLayoutEntryMarginRightProperty.GetValue(layoutEntry);

                    consideredForMargin = (bool)guiLayoutEntryConsideredForMarginField.GetValue(layoutEntry);
                }
            }

            // ...

            void UpdateGameViewGeometry(GeometryChangedEvent eventData)
            {
                UpdateSnapshotToolbarPosition(eventData.newRect.width);
            }

            void UpdateHorizontalToolbarLayout(object toolbarLayoutGroup, int playFocusedLayoutEntryIndex)
            {
                if ((bool)guiLayoutGroupIsVerticalField.GetValue(toolbarLayoutGroup))
                {
                    throw new InvalidOperationException("Unity Game View toolbar layout group was unexpectedly vertical.");
                }

                IList layoutEntries = (IList)guiLayoutGroupEntriesField.GetValue(toolbarLayoutGroup);
                Rect toolbarLayoutGroupRectangle = (Rect)guiLayoutEntryRectangleField.GetValue(toolbarLayoutGroup);

                GUIStyle toolbarLayoutGroupStyle = (GUIStyle)guiLayoutEntryStyleProperty.GetValue(toolbarLayoutGroup);

                int groupLeftMargin = 0;
                int groupRightMargin = 0;

                if (toolbarLayoutGroupStyle != GUIStyle.none)
                {
                    HorizontalToolbarLayoutEntry firstLayoutEntry = new(layoutEntries[0]);
                    HorizontalToolbarLayoutEntry lastLayoutEntry = new(layoutEntries[layoutEntries.Count - 1]);

                    groupLeftMargin = Mathf.Max(toolbarLayoutGroupStyle.padding.left, firstLayoutEntry.marginLeft);
                    groupRightMargin = Mathf.Max(toolbarLayoutGroupStyle.padding.right, lastLayoutEntry.marginRight);
                }

                float gameViewRootWidth = gameViewRoot.resolvedStyle.width;
                bool resetCoordinates = (bool)guiLayoutGroupResetCoordinatesField.GetValue(toolbarLayoutGroup);

                horizontalToolbarLayoutContentX = (resetCoordinates ? 0.0f : toolbarLayoutGroupRectangle.x) + groupLeftMargin;
                horizontalToolbarLayoutContentWidthInset = toolbarLayoutGroupRectangle.x + Mathf.Max(0.0f, gameViewRootWidth - toolbarLayoutGroupRectangle.xMax);

                horizontalToolbarLayoutContentHorizontalMargins = groupLeftMargin + groupRightMargin;

                float toolbarLayoutGroupSpacing = (float)guiLayoutGroupSpacingField.GetValue(toolbarLayoutGroup);

                horizontalToolbarLayoutTotalSpacing = toolbarLayoutGroupSpacing * (layoutEntries.Count - 1);
                horizontalToolbarLayoutChildMinimumWidth = (float)guiLayoutGroupChildMinimumWidthField.GetValue(toolbarLayoutGroup);
                horizontalToolbarLayoutChildMaximumWidth = (float)guiLayoutGroupChildMaximumWidthField.GetValue(toolbarLayoutGroup);
                horizontalToolbarLayoutStretchableCount = (int)guiLayoutGroupStretchableCountXField.GetValue(toolbarLayoutGroup);

                horizontalToolbarLayoutPlayFocusedMinimumOffset = 0.0f;
                horizontalToolbarLayoutPlayFocusedWidthRange = 0.0f;
                horizontalToolbarLayoutPlayFocusedStretchWidth = 0;

                int previousRightMargin = 0;
                bool firstMargin = true;

                for (int i = 0; i <= playFocusedLayoutEntryIndex; ++i)
                {
                    HorizontalToolbarLayoutEntry layoutEntry = new(layoutEntries[i - 0]);

                    if (layoutEntry.consideredForMargin)
                    {
                        int currentLeftMargin = layoutEntry.marginLeft;

                        if (firstMargin)
                        {
                            currentLeftMargin = 0;
                            firstMargin = false;
                        }

                        horizontalToolbarLayoutPlayFocusedMinimumOffset += Mathf.Max(previousRightMargin, currentLeftMargin);
                        previousRightMargin = layoutEntry.marginRight;
                    }

                    if (i == playFocusedLayoutEntryIndex)
                    {
                        break;
                    }

                    horizontalToolbarLayoutPlayFocusedMinimumOffset += layoutEntry.minimumWidth + toolbarLayoutGroupSpacing;
                    horizontalToolbarLayoutPlayFocusedWidthRange += layoutEntry.maximumWidth - layoutEntry.minimumWidth;
                    horizontalToolbarLayoutPlayFocusedStretchWidth += layoutEntry.stretchWidth;
                }

                horizontalToolbarLayoutValid = true;
                UpdateSnapshotToolbarPosition(gameViewRootWidth);
            }

            void UpdateSnapshotToolbarPosition(float gameViewRootWidth)
            {
                if (!horizontalToolbarLayoutValid)
                {
                    return;
                }

                float contentWidth = Mathf.Max(0.0f, gameViewRootWidth - horizontalToolbarLayoutContentWidthInset);
                contentWidth -= horizontalToolbarLayoutContentHorizontalMargins;

                float widthToDistribute = contentWidth - horizontalToolbarLayoutTotalSpacing;

                float minimumMaximumScale = 0.0f;

                if (horizontalToolbarLayoutChildMinimumWidth != horizontalToolbarLayoutChildMaximumWidth)
                {
                    minimumMaximumScale = Mathf.Clamp01(

                        (widthToDistribute - horizontalToolbarLayoutChildMinimumWidth)
                        / (horizontalToolbarLayoutChildMaximumWidth - horizontalToolbarLayoutChildMinimumWidth)
                    );
                }

                float stretchWidthPerItem = 0.0f;

                if ((widthToDistribute > horizontalToolbarLayoutChildMaximumWidth) && (horizontalToolbarLayoutStretchableCount > 0))
                {
                    stretchWidthPerItem = (widthToDistribute - horizontalToolbarLayoutChildMaximumWidth) / horizontalToolbarLayoutStretchableCount;
                }

                float playFocusedX = horizontalToolbarLayoutContentX + horizontalToolbarLayoutPlayFocusedMinimumOffset;

                playFocusedX += horizontalToolbarLayoutPlayFocusedWidthRange * minimumMaximumScale;
                playFocusedX += horizontalToolbarLayoutPlayFocusedStretchWidth * stretchWidthPerItem;

                float snapshotToolbarX = Mathf.Round(playFocusedX) - snapshotToolbarWidth;
                snapshotToolbar.style.translate = new Translate(snapshotToolbarX, 0.0f, 0.0f);
            }

            // ...

            // Ignore compiler if it says it's not used.
            // IT IS ABSOLUTELY USED, just bound/ID'd with a string.

            // > drawSnapshotMethod.

            void DrawSnapshot()
            {
                if (Event.current.type == EventType.Layout)
                {
                    GetPlayFocusedLayoutEntries(

                        out object layoutToolbarSpaceLayoutEntry,

                        out _,
                        out _
                    );

                    guiLayoutEntryMinimumWidthField.SetValue(layoutToolbarSpaceLayoutEntry, snapshotToolbarWidth);
                    guiLayoutEntryMaximumWidthField.SetValue(layoutToolbarSpaceLayoutEntry, snapshotToolbarWidth);

                    return;
                }

                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                GetPlayFocusedLayoutEntries(

                    out _,

                    out object toolbarLayoutGroup,
                    out int playFocusedLayoutEntryIndex
                );

                UpdateHorizontalToolbarLayout(toolbarLayoutGroup, playFocusedLayoutEntryIndex);
                snapshotToolbar.style.visibility = Visibility.Visible;

                if (!previewVisible)
                {
                    return;
                }

                Rect gameViewDrawRect = GetGameViewDrawRect(gameView);
                Rect gameViewTargetRect = GetDeviceFlippedTargetInView(gameView);

                gameViewTargetRect.x = Mathf.Round(gameViewTargetRect.x);
                gameViewTargetRect.y = Mathf.Round(gameViewTargetRect.y);

                Color previousGUIColour = GUI.color;
                Color previewColour = new(1.0f, 1.0f, 1.0f, previewOpacitySlider.value);

                GUI.color = Color.white;
                GUI.BeginGroup(gameViewDrawRect);

                try
                {
                    drawTextureWithHdrSupport(

                        gameViewTargetRect, snapshotTexture,
                        new Rect(0.0f, 0.0f, 1.0f, 1.0f),

                        0, 0, 0, 0,

                        previewColour, (Material)guiBlitMaterialProperty.GetValue(null),

                        -1, true

                    );
                }
                finally
                {
                    GUI.EndGroup();
                    GUI.color = previousGUIColour;
                }
            }

            // ...

            public void Dispose()
            {
                previewVisible = false;

                DetachFromHostView();

                gameViewRoot.UnregisterCallback<GeometryChangedEvent>(UpdateGameViewGeometry);
                snapshotToolbarAnchor.RemoveFromHierarchy();

                if (snapshotTexture != null)
                {
                    Object.DestroyImmediate(snapshotTexture);
                }
            }
        }
    }
}

#endif