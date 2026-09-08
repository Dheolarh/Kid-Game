using System;

using System.IO;

using UnityEditor;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

// ...

namespace Mirza.OHSNAP
{
    [InitializeOnLoad]
    static class GameViewIntegratedScreenshotTool
    {
        // ...

        const float snapshotButtonWidth = 60.0f;
        const float saveButtonWidth = 28.0f;
        const float previewButtonWidth = 76.0f;

        const float previewOpacitySliderContainerWidth = 96.0f;
        const float previewOpacitySliderWidth = 80.0f;

        const float screenshotToolbarWidth = snapshotButtonWidth + saveButtonWidth + previewButtonWidth
            + previewOpacitySliderContainerWidth;

        // ...

        static GameViewIntegratedScreenshotTool()
        {
            GameViewIntegratedToolbarHost.RegisterFeature(

                "screenshot",
                0,

                gameViewContext => new ScreenshotFeature(gameViewContext)

            );
        }

        // ...

        sealed class ScreenshotFeature : GameViewIntegratedToolbarHost.IToolbarFeature
        {
            readonly GameViewIntegratedToolbarHost.GameViewContext gameViewContext;

            readonly VisualElement screenshotToolbarContent;

            readonly ToolbarButton snapButton;
            readonly ToolbarButton saveButton;
            readonly ToolbarButton previewButton;

            readonly Slider previewOpacitySlider;

            RenderTexture snapshotTexture;

            bool previewVisible;
            bool snapshotShortcutHeld;
            bool previewShortcutHeld;

            public float ToolbarWidth => screenshotToolbarWidth;
            public float TrailingSpacing => 1.0f;

            public VisualElement ToolbarContent => screenshotToolbarContent;

            public ScreenshotFeature(GameViewIntegratedToolbarHost.GameViewContext gameViewContext)
            {
                this.gameViewContext = gameViewContext;
                screenshotToolbarContent = new VisualElement
                {
                    pickingMode = PickingMode.Position
                };

                screenshotToolbarContent.style.flexDirection = FlexDirection.Row;
                screenshotToolbarContent.style.alignItems = Align.Stretch;

                snapButton = GameViewIntegratedToolbarHost.CreateToolbarButton(

                    CaptureSnapshot,
                    "Capture snapshot of current Game View.",

                    snapshotButtonWidth,
                    "Camera Icon", "Snap"
                );

                saveButton = GameViewIntegratedToolbarHost.CreateToolbarButton(

                    SaveSnapshot,
                    "Save captured snapshot as PNG.",

                    saveButtonWidth, "SaveAs", null
                );

                saveButton.SetEnabled(false);

                previewButton = GameViewIntegratedToolbarHost.CreateToolbarButton(

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

                previewOpacitySlider = new Slider(0.0f, 1.0f)
                {
                    tooltip = "Adjust captured snapshot preview opacity."
                };

                GameViewIntegratedToolbarHost.SetFixedWidth(previewOpacitySlider, previewOpacitySliderWidth);

                float previewOpacitySliderMargin = (previewOpacitySliderContainerWidth - previewOpacitySliderWidth) / 2.0f;

                previewOpacitySlider.style.marginLeft = previewOpacitySliderMargin;
                previewOpacitySlider.style.marginRight = previewOpacitySliderMargin;
                previewOpacitySlider.style.alignSelf = Align.Center;

                previewOpacitySlider.SetValueWithoutNotify(1.0f);
                previewOpacitySlider.SetEnabled(false);

                previewOpacitySlider.RegisterValueChangedCallback(_ => gameViewContext.Repaint());
                previewOpacitySlider.RegisterCallback<PointerUpEvent>(_ => previewOpacitySlider.Blur());

                screenshotToolbarContent.Add(saveButton);
                screenshotToolbarContent.Add(snapButton);
                screenshotToolbarContent.Add(previewButton);
                screenshotToolbarContent.Add(previewOpacitySlider);
            }

            // ...

            public void Update()
            {

            }

            public void HandleGameViewEvent(Event gameViewEvent)
            {
                if ((gameViewEvent == null) || (gameViewEvent.keyCode != KeyCode.F5))
                {
                    return;
                }

                if (gameViewEvent.type == EventType.KeyDown)
                {
                    if (snapshotShortcutHeld || previewShortcutHeld)
                    {
                        gameViewEvent.Use();
                        return;
                    }

                    bool hasOtherModifier = gameViewEvent.control || gameViewEvent.alt || gameViewEvent.command;

                    if (gameViewEvent.shift && !hasOtherModifier)
                    {
                        snapshotShortcutHeld = true;

                        CaptureSnapshot();
                        gameViewEvent.Use();

                        return;
                    }

                    if (!gameViewEvent.shift && !hasOtherModifier)
                    {
                        previewShortcutHeld = true;

                        ShowSnapshot();
                        gameViewEvent.Use();
                    }

                    return;
                }

                if (gameViewEvent.type != EventType.KeyUp)
                {
                    return;
                }

                bool shortcutWasHeld = snapshotShortcutHeld || previewShortcutHeld;

                snapshotShortcutHeld = false;

                if (previewShortcutHeld)
                {
                    previewShortcutHeld = false;
                    HideSnapshot();
                }

                if (shortcutWasHeld)
                {
                    gameViewEvent.Use();
                }
            }

            // ...

            public void CaptureSnapshot()
            {
                RenderTexture gameViewRenderTexture = gameViewContext.GetRenderTexture();

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

                string lastSnapshotInformation = $"Last snap: {capturedSnapshot.width} × {capturedSnapshot.height}\n"

                    + $"Captured: {DateTime.Now:yyyy-MM-dd @ HH:mm:ss}\n\n"
                    + $"Format: {capturedSnapshot.graphicsFormat}";

                // ...

                saveButton.tooltip = "Save captured snapshot as PNG.\n\n" + lastSnapshotInformation;

                snapButton.tooltip = "Capture snapshot of current Game View.\n\n" + lastSnapshotInformation;
                previewButton.tooltip = "Hold to preview captured snapshot.\n\n" + lastSnapshotInformation;

                // ...

                saveButton.SetEnabled(true);

                previewButton.SetEnabled(true);
                previewOpacitySlider.SetEnabled(true);
            }

            static void FlipTextureVertically(Texture2D texture)
            {
                Color32[] texturePixels = texture.GetPixels32();

                int textureWidth = texture.width;
                int textureHeight = texture.height;

                for (int y = 0; y < (textureHeight / 2); ++y)
                {
                    int oppositeY = textureHeight - 1 - y;

                    int rowStartIndex = y * textureWidth;
                    int oppositeRowStartIndex = oppositeY * textureWidth;

                    for (int x = 0; x < textureWidth; ++x)
                    {
                        int pixelIndex = rowStartIndex + x;
                        int oppositePixelIndex = oppositeRowStartIndex + x;

                        // Tuple-swap. Kek.

                        (texturePixels[oppositePixelIndex], texturePixels[pixelIndex]) = (texturePixels[pixelIndex], texturePixels[oppositePixelIndex]);
                    }
                }

                texture.SetPixels32(texturePixels);
            }

            void SaveSnapshot()
            {
                if ((snapshotTexture == null) || !snapshotTexture.IsCreated())
                {
                    return;
                }

                string filePath = EditorUtility.SaveFilePanel(

                    "Save Game View Snapshot", string.Empty,
                    $"OH, SNAP - {DateTime.Now:yyyy-MM-dd @ HH-mm-ss}.png", "png");

                if (string.IsNullOrEmpty(filePath))
                {
                    return;
                }

                RenderTexture previousActiveRenderTexture = RenderTexture.active;
                Texture2D snapshotImage = null;

                try
                {
                    RenderTexture.active = snapshotTexture;

                    snapshotImage = new Texture2D(snapshotTexture.width, snapshotTexture.height, TextureFormat.RGBA32, false, false)
                    {
                        name = "Game View Snapshot Export",
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    snapshotImage.ReadPixels(

                        new Rect(0.0f, 0.0f, snapshotTexture.width, snapshotTexture.height),
                        0, 0, false

                    );

                    FlipTextureVertically(snapshotImage);
                    snapshotImage.Apply(false, false);

                    byte[] pngData = snapshotImage.EncodeToPNG();

                    if (pngData == null)
                    {
                        return;
                    }

                    File.WriteAllBytes(filePath, pngData);

                    string projectRelativePath = FileUtil.GetProjectRelativePath(filePath);

                    if (!string.IsNullOrEmpty(projectRelativePath))
                    {
                        AssetDatabase.ImportAsset(projectRelativePath);
                    }
                }
                finally
                {
                    RenderTexture.active = previousActiveRenderTexture;

                    if (snapshotImage != null)
                    {
                        Object.DestroyImmediate(snapshotImage);
                    }
                }
            }

            // ...

            void ShowSnapshot(PointerDownEvent eventData)
            {
                if (eventData.button != 0)
                {
                    return;
                }

                ShowSnapshot();
            }

            public void ShowSnapshot()
            {
                if ((snapshotTexture == null) || !snapshotTexture.IsCreated())
                {
                    return;
                }

                previewVisible = true;
                gameViewContext.Repaint();
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

            public void HideSnapshot()
            {
                previewVisible = false;
                gameViewContext.Repaint();
            }

            public void DrawGameViewOverlay()
            {
                if (!previewVisible || (snapshotTexture == null))
                {
                    return;
                }

                Color previewColour = new(1.0f, 1.0f, 1.0f, previewOpacitySlider.value);

                gameViewContext.DrawTextureOverlay(snapshotTexture, previewColour);
            }

            // ...

            public void Dispose()
            {
                previewVisible = false;
                snapshotShortcutHeld = false;
                previewShortcutHeld = false;

                if (snapshotTexture != null)
                {
                    Object.DestroyImmediate(snapshotTexture);
                    snapshotTexture = null;
                }
            }
        }
    }
}
