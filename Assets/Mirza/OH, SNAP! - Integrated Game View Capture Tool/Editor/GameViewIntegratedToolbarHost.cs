using System;

using System.Collections;
using System.Collections.Generic;

using System.Reflection;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

// ...

namespace Mirza.OHSNAP
{
    [InitializeOnLoad]
    public static class GameViewIntegratedToolbarHost
    {
        // ...

        const string integratedToolbarAnchorName = "game-view-integrated-toolbar-anchor";
        const string legacyScreenshotToolbarAnchorName = "game-view-screenshot-toolbar-anchor";

        const double gameViewScanInterval = 0.5;

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

        static readonly FieldInfo guiLayoutEntryMinimumWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "minWidth", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryMaximumWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "maxWidth", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryRectangleField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "rect", instancePublicBindingFlags);
        static readonly FieldInfo guiLayoutEntryStretchWidthField = GetRequiredMember<FieldInfo>(guiLayoutEntryType, "stretchWidth", instancePublicBindingFlags);

        static readonly PropertyInfo guiLayoutEntryStyleProperty = GetRequiredMember<PropertyInfo>(guiLayoutEntryType, "style", instancePublicBindingFlags);

        static readonly FieldInfo gameViewRenderTextureField = GetRequiredMember<FieldInfo>(gameViewType, "m_RenderTexture", instanceNonPublicBindingFlags);
        static readonly FieldInfo gameViewZoomAreaField = GetRequiredMember<FieldInfo>(gameViewType, "m_ZoomArea", instanceNonPublicBindingFlags);

        static readonly PropertyInfo zoomAreaDrawRectProperty = GetRequiredMember<PropertyInfo>(gameViewZoomAreaField.FieldType, "drawRect", instanceAnyVisibilityBindingFlags);
        static readonly PropertyInfo deviceFlippedTargetInViewProperty = GetRequiredMember<PropertyInfo>(gameViewType, "deviceFlippedTargetInView", instanceNonPublicBindingFlags);

        static readonly PropertyInfo guiBlitMaterialProperty = GetRequiredMember<PropertyInfo>(typeof(GUI), "blitMaterial", staticNonPublicBindingFlags);

        static readonly DrawTextureWithHdrSupport drawTextureWithHdrSupport = (DrawTextureWithHdrSupport)Delegate.CreateDelegate(

            typeof(DrawTextureWithHdrSupport),
            GetRequiredMember<MethodInfo>(typeof(EditorGUIUtility), "DrawTextureHdrSupport", staticNonPublicBindingFlags)

        );

        static readonly MethodInfo handleInputMethod = GetRequiredMember<MethodInfo>(typeof(GameViewController), "HandleInput", instanceNonPublicBindingFlags);
        static readonly MethodInfo drawToolbarMethod = GetRequiredMember<MethodInfo>(typeof(GameViewController), "DrawToolbar", instanceNonPublicBindingFlags);

        static readonly List<FeatureRegistration> featureRegistrations = new();

        static readonly Dictionary<EditorWindow, GameViewController> gameViewControllers = new();
        static readonly List<EditorWindow> closedGameViews = new();

        static double nextGameViewScanTime;

        // ...

        static GameViewIntegratedToolbarHost()
        {
            EditorApplication.update += UpdateGameViews;
            EditorApplication.delayCall += UpdateGameViews;

            AssemblyReloadEvents.beforeAssemblyReload += DisposeGameViewControllers;
            EditorApplication.quitting += DisposeGameViewControllers;
        }

        // ...

        public interface IToolbarFeature : IDisposable
        {
            float ToolbarWidth { get; }
            float TrailingSpacing { get; }

            VisualElement ToolbarContent { get; }

            void Update();
            void HandleGameViewEvent(Event gameViewEvent);
            void DrawGameViewOverlay();
        }

        public sealed class GameViewContext
        {
            readonly EditorWindow gameView;

            internal GameViewContext(EditorWindow gameView)
            {
                this.gameView = gameView;
            }

            public EditorWindow GameView => gameView;

            public RenderTexture GetRenderTexture()
            {
                return GetGameViewRenderTexture(gameView);
            }

            public Rect GetDrawRect()
            {
                return GetGameViewDrawRect(gameView);
            }

            public Rect GetDeviceFlippedTargetInView()
            {
                return GameViewIntegratedToolbarHost.GetDeviceFlippedTargetInView(gameView);
            }

            public void DrawTextureOverlay(Texture texture, Color colour)
            {
                GameViewIntegratedToolbarHost.DrawTextureOverlay(gameView, texture, colour);
            }

            public void Repaint()
            {
                gameView.Repaint();
            }
        }

        sealed class FeatureRegistration
        {
            public readonly string identifier;
            public readonly int order;

            public readonly Func<GameViewContext, IToolbarFeature> createFeature;

            public FeatureRegistration(string identifier, int order, Func<GameViewContext, IToolbarFeature> createFeature)
            {
                this.identifier = identifier;
                this.order = order;

                this.createFeature = createFeature;
            }
        }

        sealed class FeatureInstance
        {
            public readonly FeatureRegistration registration;
            public readonly IToolbarFeature feature;

            public readonly List<VisualElement> toolbarElements = new();

            public FeatureInstance(FeatureRegistration registration, IToolbarFeature feature)
            {
                this.registration = registration;
                this.feature = feature;

                VisualElement toolbarContent = feature.ToolbarContent;

                if (toolbarContent == null)
                {
                    throw new InvalidOperationException($"Game View toolbar feature '{registration.identifier}' returned null toolbar content.");
                }

                for (int i = 0; i < toolbarContent.childCount; ++i)
                {
                    toolbarElements.Add(toolbarContent[i - 0]);
                }
            }
        }

        // ...

        public static void RegisterFeature(string identifier, int order, Func<GameViewContext, IToolbarFeature> createFeature)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Game View toolbar feature identifier cannot be null or empty.", nameof(identifier));
            }

            if (createFeature == null)
            {
                throw new ArgumentNullException(nameof(createFeature));
            }

            FeatureRegistration registeredFeature = new(identifier, order, createFeature);
            int existingFeatureRegistrationIndex = featureRegistrations.FindIndex(featureRegistration =>
                string.Equals(featureRegistration.identifier, identifier, StringComparison.Ordinal));

            if (existingFeatureRegistrationIndex >= 0)
            {
                featureRegistrations[existingFeatureRegistrationIndex] = registeredFeature;
            }
            else
            {
                featureRegistrations.Add(registeredFeature);
            }

            foreach (GameViewController gameViewController in gameViewControllers.Values)
            {
                gameViewController.SetFeature(registeredFeature);
            }
        }

        static int CompareFeatureRegistrations(FeatureRegistration left, FeatureRegistration right)
        {
            int orderComparison = left.order.CompareTo(right.order);

            return orderComparison != 0
                ? orderComparison
                : string.CompareOrdinal(left.identifier, right.identifier);
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

        static void UpdateGameViews()
        {
            foreach (KeyValuePair<EditorWindow, GameViewController> gameViewController in gameViewControllers)
            {
                if (gameViewController.Key != null)
                {
                    gameViewController.Value.UpdateFeatures();
                }
            }

            if (EditorApplication.timeSinceStartup < nextGameViewScanTime)
            {
                return;
            }

            nextGameViewScanTime = EditorApplication.timeSinceStartup + gameViewScanInterval;
            closedGameViews.Clear();

            foreach (KeyValuePair<EditorWindow, GameViewController> gameViewController in gameViewControllers)
            {
                if (gameViewController.Key != null)
                {
                    gameViewController.Value.UpdateOnGUIAttachment();

                    continue;
                }

                gameViewController.Value.Dispose();
                closedGameViews.Add(gameViewController.Key);
            }

            foreach (EditorWindow closedGameView in closedGameViews)
            {
                gameViewControllers.Remove(closedGameView);
            }

            Object[] openGameViewObjects = Resources.FindObjectsOfTypeAll(gameViewType);

            foreach (Object openGameViewObject in openGameViewObjects)
            {
                EditorWindow openGameView = (EditorWindow)openGameViewObject;

                if (gameViewControllers.ContainsKey(openGameView))
                {
                    continue;
                }

                gameViewControllers.Add(openGameView, new GameViewController(openGameView));
            }
        }

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

        static void DrawTextureOverlay(EditorWindow gameView, Texture texture, Color colour)
        {
            Rect gameViewDrawRect = GetGameViewDrawRect(gameView);
            Rect gameViewTargetRect = GetDeviceFlippedTargetInView(gameView);

            gameViewTargetRect.x = Mathf.Round(gameViewTargetRect.x);
            gameViewTargetRect.y = Mathf.Round(gameViewTargetRect.y);

            Color previousGUIColour = GUI.color;

            GUI.color = Color.white;
            GUI.BeginGroup(gameViewDrawRect);

            try
            {
                drawTextureWithHdrSupport(

                    gameViewTargetRect, texture,
                    new Rect(0.0f, 0.0f, 1.0f, 1.0f),

                    0, 0, 0, 0,

                    colour, (Material)guiBlitMaterialProperty.GetValue(null),

                    -1, true

                );
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = previousGUIColour;
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

        public static void SetFixedWidth(VisualElement visualElement, float width)
        {
            visualElement.style.width = width;

            visualElement.style.minWidth = width;
            visualElement.style.maxWidth = width;
        }

        public static ToolbarButton CreateToolbarButton(

            Action clicked, string tooltip, float width,
            string iconName, string buttonText, float iconSize = 16.0f
        )
        {
            ToolbarButton toolbarButton = new(clicked)
            {
                focusable = false, // Don't leave annoying/ugly blue selection highlight on it after interaction/clicks.

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

            buttonIcon.style.width = iconSize;
            buttonIcon.style.height = iconSize;

            buttonIcon.style.flexShrink = 0.0f;

            buttonContent.Add(buttonIcon);

            if (!string.IsNullOrEmpty(buttonText))
            {
                Label buttonLabel = new(buttonText)
                {
                    pickingMode = PickingMode.Ignore
                };

                buttonLabel.style.marginLeft = 3.0f;
                buttonContent.Add(buttonLabel);
            }

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

        static bool TryGetPlayFocusedLayoutEntries(object layoutEntry, out object toolbarSpaceLayoutEntry, out object playFocusedLayoutEntry)
        {
            toolbarSpaceLayoutEntry = null;
            playFocusedLayoutEntry = null;

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

                if (guiLayoutGroupType.IsInstanceOfType(currentToolbarSpaceLayoutEntry)
                    || ((int)guiLayoutEntryStretchWidthField.GetValue(currentToolbarSpaceLayoutEntry) == 0))
                {
                    continue;
                }

                toolbarSpaceLayoutEntry = currentToolbarSpaceLayoutEntry;
                playFocusedLayoutEntry = currentLayoutEntry;

                return true;
            }

            foreach (object childLayoutEntry in childLayoutEntries)
            {
                if (TryGetPlayFocusedLayoutEntries(childLayoutEntry, out toolbarSpaceLayoutEntry, out playFocusedLayoutEntry))
                {
                    return true;
                }
            }

            return false;
        }

        static void GetPlayFocusedLayoutEntries(out object toolbarSpaceLayoutEntry, out object playFocusedLayoutEntry)
        {
            object topLevelLayoutEntry = guiLayoutTopLevelProperty.GetValue(null);

            if (!TryGetPlayFocusedLayoutEntries(topLevelLayoutEntry, out toolbarSpaceLayoutEntry, out playFocusedLayoutEntry))
            {
                throw new InvalidOperationException("Unity Game View Play Focused toolbar entry was not found.");
            }
        }

        // ...

        static void DisposeGameViewControllers()
        {
            EditorApplication.update -= UpdateGameViews;
            EditorApplication.delayCall -= UpdateGameViews;

            foreach (GameViewController gameViewController in gameViewControllers.Values)
            {
                gameViewController.Dispose();
            }

            gameViewControllers.Clear();
        }

        // ...

        sealed class GameViewController
        {
            readonly EditorWindow gameView;
            readonly GameViewContext gameViewContext;

            readonly Toolbar integratedToolbar;

            readonly List<FeatureInstance> featureInstances = new();

            readonly Delegate handleInputDelegate;
            readonly Delegate drawToolbarDelegate;

            object attachedHostView;

            float integratedToolbarWidth;
            float integratedToolbarTrailingSpacing;

            public GameViewController(EditorWindow gameView)
            {
                this.gameView = gameView;

                gameViewContext = new GameViewContext(gameView);

                handleInputDelegate = Delegate.CreateDelegate(hostViewOnGUIField.FieldType, this, handleInputMethod);
                drawToolbarDelegate = Delegate.CreateDelegate(hostViewOnGUIField.FieldType, this, drawToolbarMethod);

                VisualElement gameViewRoot = gameView.rootVisualElement;

                gameViewRoot.Q<VisualElement>(integratedToolbarAnchorName)?.RemoveFromHierarchy();
                gameViewRoot.Q<VisualElement>(legacyScreenshotToolbarAnchorName)?.RemoveFromHierarchy();

                integratedToolbar = new Toolbar
                {
                    name = integratedToolbarAnchorName,
                    pickingMode = PickingMode.Position
                };

                integratedToolbar.style.position = Position.Absolute;
                integratedToolbar.style.left = 0.0f;
                integratedToolbar.style.top = 0.0f;

                integratedToolbar.style.marginLeft = 0.0f;
                integratedToolbar.style.marginRight = 0.0f;

                integratedToolbar.style.paddingLeft = 0.0f;
                integratedToolbar.style.paddingRight = 0.0f;

                SetFixedWidth(integratedToolbar, 0.0f);
                integratedToolbar.style.visibility = Visibility.Hidden;

                gameViewRoot.Add(integratedToolbar);

                foreach (FeatureRegistration featureRegistration in featureRegistrations)
                {
                    featureInstances.Add(CreateFeatureInstance(featureRegistration));
                }

                featureInstances.Sort((left, right) =>
                    CompareFeatureRegistrations(left.registration, right.registration));

                RebuildToolbar();
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

            FeatureInstance CreateFeatureInstance(FeatureRegistration featureRegistration)
            {
                IToolbarFeature feature = featureRegistration.createFeature(gameViewContext);

                if (feature == null)
                {
                    throw new InvalidOperationException($"Game View toolbar feature '{featureRegistration.identifier}' returned null.");
                }

                return new FeatureInstance(featureRegistration, feature);
            }

            public void SetFeature(FeatureRegistration featureRegistration)
            {
                int existingFeatureInstanceIndex = featureInstances.FindIndex(featureInstance =>
                    string.Equals(featureInstance.registration.identifier, featureRegistration.identifier, StringComparison.Ordinal));

                if (existingFeatureInstanceIndex >= 0)
                {
                    featureInstances[existingFeatureInstanceIndex].feature.Dispose();
                    featureInstances[existingFeatureInstanceIndex] = CreateFeatureInstance(featureRegistration);
                }
                else
                {
                    featureInstances.Add(CreateFeatureInstance(featureRegistration));
                }

                featureInstances.Sort((left, right) =>
                    CompareFeatureRegistrations(left.registration, right.registration));

                RebuildToolbar();
            }

            void RebuildToolbar()
            {
                integratedToolbar.Clear();
                integratedToolbarWidth = 0.0f;

                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    foreach (VisualElement toolbarElement in featureInstance.toolbarElements)
                    {
                        toolbarElement.RemoveFromHierarchy();

                        toolbarElement.style.flexGrow = 0.0f;
                        toolbarElement.style.flexShrink = 0.0f;

                        integratedToolbar.Add(toolbarElement);
                    }

                    integratedToolbarWidth += featureInstance.feature.ToolbarWidth;
                }

                integratedToolbarTrailingSpacing = featureInstances.Count > 0
                    ? featureInstances[featureInstances.Count - 1].feature.TrailingSpacing
                    : 0.0f;

                SetFixedWidth(integratedToolbar, integratedToolbarWidth);

                if (integratedToolbarWidth <= 0.0f)
                {
                    integratedToolbar.style.visibility = Visibility.Hidden;
                }
            }

            void UpdateToolbarDimensions()
            {
                float updatedToolbarWidth = 0.0f;

                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    updatedToolbarWidth += featureInstance.feature.ToolbarWidth;
                }

                float updatedTrailingSpacing = featureInstances.Count > 0
                    ? featureInstances[featureInstances.Count - 1].feature.TrailingSpacing
                    : 0.0f;

                if (!Mathf.Approximately(integratedToolbarWidth, updatedToolbarWidth))
                {
                    integratedToolbarWidth = updatedToolbarWidth;
                    SetFixedWidth(integratedToolbar, integratedToolbarWidth);
                }

                integratedToolbarTrailingSpacing = updatedTrailingSpacing;
            }

            public void UpdateFeatures()
            {
                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    featureInstance.feature.Update();
                }

                UpdateToolbarDimensions();
            }

            public void UpdateOnGUIAttachment()
            {
                object currentHostView = editorWindowParentField.GetValue(gameView);

                Delegate currentOnGUIDelegate = (currentHostView == null)
                    ? null : (Delegate)hostViewOnGUIField.GetValue(currentHostView);

                int handleInputInvocationCount = GetDelegateInvocationCount(currentOnGUIDelegate, handleInputDelegate);
                int drawToolbarInvocationCount = GetDelegateInvocationCount(currentOnGUIDelegate, drawToolbarDelegate);

                if (ReferenceEquals(currentHostView, attachedHostView)
                    && HasDelegateTarget(currentOnGUIDelegate, gameView)
                    && (handleInputInvocationCount == 1)
                    && (drawToolbarInvocationCount == 1))
                {
                    return;
                }

                DetachFromHostView();

                if (currentHostView == null)
                {
                    return;
                }

                currentOnGUIDelegate = (Delegate)hostViewOnGUIField.GetValue(currentHostView);

                currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, handleInputDelegate);
                currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, drawToolbarDelegate);

                if (!HasDelegateTarget(currentOnGUIDelegate, gameView))
                {
                    hostViewOnGUIField.SetValue(currentHostView, currentOnGUIDelegate);
                    return;
                }

                currentOnGUIDelegate = Delegate.Combine(handleInputDelegate, currentOnGUIDelegate);
                currentOnGUIDelegate = Delegate.Combine(currentOnGUIDelegate, drawToolbarDelegate);

                hostViewOnGUIField.SetValue(currentHostView, currentOnGUIDelegate);
                attachedHostView = currentHostView;
            }

            // ...

            void DetachFromHostView()
            {
                if (attachedHostView != null)
                {
                    Delegate currentOnGUIDelegate = (Delegate)hostViewOnGUIField.GetValue(attachedHostView);

                    currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, handleInputDelegate);
                    currentOnGUIDelegate = RemoveDelegateInvocations(currentOnGUIDelegate, drawToolbarDelegate);

                    hostViewOnGUIField.SetValue(attachedHostView, currentOnGUIDelegate);
                }

                attachedHostView = null;
                integratedToolbar.style.visibility = Visibility.Hidden;
            }

            // Ignore compiler if it says it's not used.
            // IT IS ABSOLUTELY USED, just bound/ID'd with a string.

            // > handleInputMethod.

            void HandleInput()
            {
                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    featureInstance.feature.HandleGameViewEvent(Event.current);
                }
            }

            // Ignore compiler if it says it's not used.
            // IT IS ABSOLUTELY USED, just bound/ID'd with a string.

            // > drawToolbarMethod.

            void DrawToolbar()
            {
                if (Event.current.type == EventType.Layout)
                {
                    GetPlayFocusedLayoutEntries(out object toolbarSpaceLayoutEntry, out _);

                    float reservedToolbarWidth = integratedToolbarWidth > 0.0f
                        ? integratedToolbarWidth + integratedToolbarTrailingSpacing
                        : 0.0f;

                    guiLayoutEntryMinimumWidthField.SetValue(toolbarSpaceLayoutEntry, reservedToolbarWidth);
                    guiLayoutEntryMaximumWidthField.SetValue(toolbarSpaceLayoutEntry, reservedToolbarWidth);

                    return;
                }

                if (Event.current.type != EventType.Repaint)
                {
                    return;
                }

                if (integratedToolbarWidth <= 0.0f)
                {
                    integratedToolbar.style.visibility = Visibility.Hidden;
                    return;
                }

                GetPlayFocusedLayoutEntries(out _, out object playFocusedLayoutEntry);

                Rect playFocusedRectangle = (Rect)guiLayoutEntryRectangleField.GetValue(playFocusedLayoutEntry);

                integratedToolbar.style.left = Mathf.Round(playFocusedRectangle.xMin)
                    - integratedToolbarWidth - integratedToolbarTrailingSpacing;
                integratedToolbar.style.visibility = Visibility.Visible;

                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    featureInstance.feature.DrawGameViewOverlay();
                }
            }

            // ...

            public void Dispose()
            {
                DetachFromHostView();

                foreach (FeatureInstance featureInstance in featureInstances)
                {
                    featureInstance.feature.Dispose();
                }

                featureInstances.Clear();

                integratedToolbar.RemoveFromHierarchy();
            }
        }
    }
}
