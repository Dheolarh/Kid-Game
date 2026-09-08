using System;

using System.IO;

using UnityEditor;
using UnityEditor.Media;
using UnityEditor.UIElements;

using UnityEngine;
using UnityEngine.UIElements;

using Object = UnityEngine.Object;

// ...

namespace Mirza.OHSNAP
{
    [InitializeOnLoad]
    static class GameViewIntegratedRecordingTool
    {
        // ...

        const float recordingButtonWidth = 72.0f;
        const float recordingSecondaryButtonWidth = 72.0f;

        const float recordingPauseButtonWidth = 68.0f;
        const float recordingStopButtonWidth = 56.0f;
        const float recordingCancelButtonWidth = 72.0f;

        const float idleRecordingToolbarWidth = recordingButtonWidth + recordingSecondaryButtonWidth;
        const float activeRecordingToolbarWidth = recordingPauseButtonWidth + recordingStopButtonWidth + recordingCancelButtonWidth;

        const uint recordingBitRate5Mbps = 5000000u;
        const uint recordingBitRate10Mbps = 10000000u;
        const uint recordingBitRate20Mbps = 20000000u;
        const uint recordingBitRate40Mbps = 40000000u;

        const string recordingFPSPreferenceKey = "Mirza.OHSNAP.Recording.FPS";
        const string recordingQualityPreferenceKey = "Mirza.OHSNAP.Recording.Quality";

        // ...

        enum RecordingQuality
        {
            Low,
            Medium,
            High,
            Ultra
        }

        // ...

        static GameViewIntegratedRecordingTool()
        {
            GameViewIntegratedToolbarHost.RegisterFeature(

                "recording",
                100,

                gameViewContext => new RecordingFeature(gameViewContext)

            );
        }

        // ...

        sealed class RecordingFeature : GameViewIntegratedToolbarHost.IToolbarFeature
        {
            readonly GameViewIntegratedToolbarHost.GameViewContext gameViewContext;

            readonly VisualElement recordingToolbarContent;

            readonly ToolbarButton recordingButton;
            readonly ToolbarButton recordingSettingsButton;
            readonly ToolbarButton pauseRecordingButton;
            readonly ToolbarButton cancelRecordingButton;

            readonly Label pauseRecordingIconLabel;
            readonly Label recordingDurationLabel;

            MediaEncoder mediaEncoder;

            RenderTexture recordingReadbackTexture;
            Texture2D recordingFrameTexture;

            string temporaryRecordingFilePath;

            int recordingFPS;
            RecordingQuality recordingQuality;

            int recordingWidth;
            int recordingHeight;

            double accumulatedRecordingActiveDuration;
            double recordingActiveSegmentStartTime;

            long recordedFrameCount;

            bool recording;
            bool recordingPaused;

            public float ToolbarWidth => recording ? activeRecordingToolbarWidth : idleRecordingToolbarWidth;
            public float TrailingSpacing => 0.0f;

            public VisualElement ToolbarContent => recordingToolbarContent;

            public RecordingFeature(GameViewIntegratedToolbarHost.GameViewContext gameViewContext)
            {
                this.gameViewContext = gameViewContext;

                recordingFPS = GetSavedRecordingFPS();
                recordingQuality = GetSavedRecordingQuality();

                recordingToolbarContent = new VisualElement
                {
                    pickingMode = PickingMode.Position
                };

                recordingToolbarContent.style.flexDirection = FlexDirection.Row;
                recordingToolbarContent.style.alignItems = Align.Stretch;

                recordingButton = new ToolbarButton(StartRecording)
                {
                    focusable = false,
                    pickingMode = PickingMode.Position,

                    enableRichText = true,
                    text = GetRecordingButtonText(false)
                };

                GameViewIntegratedToolbarHost.SetFixedWidth(recordingButton, recordingButtonWidth);

                recordingButton.style.unityTextAlign = TextAnchor.MiddleCenter;
                recordingButton.style.unityFontStyleAndWeight = FontStyle.Bold;

                recordingSettingsButton = new ToolbarButton(ShowRecordingSettingsMenu)
                {
                    focusable = false,
                    pickingMode = PickingMode.Position
                };

                GameViewIntegratedToolbarHost.SetFixedWidth(recordingSettingsButton, recordingSecondaryButtonWidth);

                recordingSettingsButton.style.alignItems = Align.Center;
                recordingSettingsButton.style.justifyContent = Justify.Center;

                VisualElement recordingSettingsContent = new()
                {
                    pickingMode = PickingMode.Ignore
                };

                recordingSettingsContent.style.flexGrow = 1.0f;
                recordingSettingsContent.style.flexDirection = FlexDirection.Row;
                recordingSettingsContent.style.alignItems = Align.Center;
                recordingSettingsContent.style.justifyContent = Justify.Center;

                VisualElement recordingSettingsIndicator = new()
                {
                    pickingMode = PickingMode.Ignore
                };

                recordingSettingsIndicator.style.width = 8.0f;
                recordingSettingsIndicator.style.height = 8.0f;

                recordingSettingsIndicator.style.flexShrink = 0.0f;
                recordingSettingsIndicator.style.backgroundColor = new StyleColor(new Color32(229, 72, 77, 255));

                recordingSettingsIndicator.style.borderTopLeftRadius = 4.0f;
                recordingSettingsIndicator.style.borderTopRightRadius = 4.0f;
                recordingSettingsIndicator.style.borderBottomLeftRadius = 4.0f;
                recordingSettingsIndicator.style.borderBottomRightRadius = 4.0f;

                recordingSettingsContent.Add(recordingSettingsIndicator);

                Texture recordingSettingsIcon = GetRecordingSettingsIcon();

                if (recordingSettingsIcon != null)
                {
                    Image recordingSettingsImage = new()
                    {
                        image = recordingSettingsIcon,

                        pickingMode = PickingMode.Ignore,
                        scaleMode = ScaleMode.ScaleToFit
                    };

                    recordingSettingsImage.style.width = 16.0f;
                    recordingSettingsImage.style.height = 16.0f;

                    recordingSettingsImage.style.marginLeft = 4.0f;
                    recordingSettingsImage.style.flexShrink = 0.0f;

                    recordingSettingsContent.Add(recordingSettingsImage);
                }
                else
                {
                    Label recordingSettingsFallbackIcon = new("⚙")
                    {
                        pickingMode = PickingMode.Ignore
                    };

                    recordingSettingsFallbackIcon.style.marginLeft = 4.0f;
                    recordingSettingsContent.Add(recordingSettingsFallbackIcon);
                }

                Label recordingSettingsDropdownArrow = new("▾")
                {
                    pickingMode = PickingMode.Ignore
                };

                recordingSettingsDropdownArrow.style.marginLeft = 4.0f;
                recordingSettingsDropdownArrow.style.fontSize = 10.0f;

                recordingSettingsContent.Add(recordingSettingsDropdownArrow);
                recordingSettingsButton.Add(recordingSettingsContent);

                pauseRecordingButton = new ToolbarButton(ToggleRecordingPause)
                {
                    focusable = false,
                    pickingMode = PickingMode.Position,
                    tooltip = "Pause recording."
                };

                GameViewIntegratedToolbarHost.SetFixedWidth(pauseRecordingButton, recordingPauseButtonWidth);

                pauseRecordingButton.style.alignItems = Align.Center;
                pauseRecordingButton.style.justifyContent = Justify.Center;
                pauseRecordingButton.style.display = DisplayStyle.None;

                VisualElement pauseRecordingContent = new()
                {
                    pickingMode = PickingMode.Ignore
                };

                pauseRecordingContent.style.flexDirection = FlexDirection.Row;
                pauseRecordingContent.style.alignItems = Align.Center;
                pauseRecordingContent.style.justifyContent = Justify.Center;

                pauseRecordingIconLabel = new Label("⏸")
                {
                    pickingMode = PickingMode.Ignore
                };

                pauseRecordingIconLabel.style.width = 14.0f;
                pauseRecordingIconLabel.style.flexShrink = 0.0f;
                pauseRecordingIconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                recordingDurationLabel = new Label("00:00")
                {
                    pickingMode = PickingMode.Ignore
                };

                recordingDurationLabel.style.marginLeft = 4.0f;
                recordingDurationLabel.style.width = 32.0f;
                recordingDurationLabel.style.flexShrink = 0.0f;
                recordingDurationLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                pauseRecordingContent.Add(pauseRecordingIconLabel);
                pauseRecordingContent.Add(recordingDurationLabel);

                pauseRecordingButton.Add(pauseRecordingContent);

                cancelRecordingButton = new ToolbarButton(CancelRecording)
                {
                    focusable = false,
                    pickingMode = PickingMode.Position,
                    text = "✕ Cancel",
                    tooltip = "Cancel recording and discard captured video."
                };

                GameViewIntegratedToolbarHost.SetFixedWidth(cancelRecordingButton, recordingCancelButtonWidth);

                cancelRecordingButton.style.unityTextAlign = TextAnchor.MiddleCenter;
                cancelRecordingButton.style.display = DisplayStyle.None;

                recordingToolbarContent.Add(pauseRecordingButton);
                recordingToolbarContent.Add(recordingButton);
                recordingToolbarContent.Add(recordingSettingsButton);
                recordingToolbarContent.Add(cancelRecordingButton);

                UpdateRecordingSettingsTooltip();
            }

            // ...

            static string GetRecordingButtonText(bool active)
            {
                return active ? "■  Stop" : "<color=#E5484D>●</color>  Record";
            }

            static Texture GetRecordingSettingsIcon()
            {
                string[] recordingSettingsIconNames = EditorGUIUtility.isProSkin
                    ? new[] { "d_SettingsIcon", "SettingsIcon", "Settings" }
                    : new[] { "SettingsIcon", "Settings", "d_SettingsIcon" };

                foreach (string recordingSettingsIconName in recordingSettingsIconNames)
                {
                    Texture recordingSettingsIcon = EditorGUIUtility.IconContent(recordingSettingsIconName).image;

                    if (recordingSettingsIcon != null)
                    {
                        return recordingSettingsIcon;
                    }
                }

                return null;
            }

            string GetRecordingDurationText()
            {
                double recordingDurationSeconds = recordedFrameCount / (double)recordingFPS;

                TimeSpan recordingDuration = TimeSpan.FromSeconds(recordingDurationSeconds);

                return recordingDuration.TotalHours >= 1.0
                    ? $"{(int)recordingDuration.TotalHours:00}:{recordingDuration.Minutes:00}:{recordingDuration.Seconds:00}"
                    : $"{recordingDuration.Minutes:00}:{recordingDuration.Seconds:00}";
            }

            double GetRecordingActiveDuration(double currentTime)
            {
                return recordingPaused
                    ? accumulatedRecordingActiveDuration
                    : accumulatedRecordingActiveDuration + (currentTime - recordingActiveSegmentStartTime);
            }

            void UpdateRecordingPauseButton()
            {
                pauseRecordingIconLabel.text = recordingPaused ? "▶" : "⏸";
                recordingDurationLabel.text = GetRecordingDurationText();

                pauseRecordingButton.tooltip = recordingPaused
                    ? "Resume recording."
                    : "Pause recording.";
            }

            void ShowRecordingSettingsMenu()
            {
                if (recording)
                {
                    return;
                }

                GenericMenu recordingSettingsMenu = new();

                AppendRecordingFPSMenuItem(recordingSettingsMenu, 24);
                AppendRecordingFPSMenuItem(recordingSettingsMenu, 30);
                AppendRecordingFPSMenuItem(recordingSettingsMenu, 60);

                AppendRecordingQualityMenuItem(recordingSettingsMenu, RecordingQuality.Low);
                AppendRecordingQualityMenuItem(recordingSettingsMenu, RecordingQuality.Medium);
                AppendRecordingQualityMenuItem(recordingSettingsMenu, RecordingQuality.High);
                AppendRecordingQualityMenuItem(recordingSettingsMenu, RecordingQuality.Ultra);

                recordingSettingsMenu.ShowAsContext();
            }

            void AppendRecordingFPSMenuItem(GenericMenu recordingSettingsMenu, int FPS)
            {
                recordingSettingsMenu.AddItem(

                    new GUIContent($"FPS/{FPS}"),
                    recordingFPS == FPS,

                    () => SetRecordingFPS(FPS)

                );
            }

            void AppendRecordingQualityMenuItem(GenericMenu recordingSettingsMenu, RecordingQuality quality)
            {
                uint targetBitRate = GetRecordingTargetBitRate(quality);

                recordingSettingsMenu.AddItem(

                    new GUIContent($"Quality/{quality} ({FormatBitRate(targetBitRate)})"),
                    recordingQuality == quality,

                    () => SetRecordingQuality(quality)

                );
            }

            static uint GetRecordingTargetBitRate(RecordingQuality quality)
            {
                return quality switch
                {
                    RecordingQuality.Low => recordingBitRate5Mbps,
                    RecordingQuality.Medium => recordingBitRate10Mbps,
                    RecordingQuality.High => recordingBitRate20Mbps,
                    RecordingQuality.Ultra => recordingBitRate40Mbps,

                    _ => recordingBitRate10Mbps
                };
            }

            static string FormatBitRate(uint bitRate)
            {
                double megabitsPerSecond = bitRate / 1000000.0;
                return $"{megabitsPerSecond:0.0} Mbps";
            }

            static int GetSavedRecordingFPS()
            {
                int savedRecordingFPS = EditorPrefs.GetInt(recordingFPSPreferenceKey, 30);

                return savedRecordingFPS switch
                {
                    24 => 24,
                    30 => 30,
                    60 => 60,

                    _ => 30
                };
            }

            static RecordingQuality GetSavedRecordingQuality()
            {
                int savedRecordingQuality = EditorPrefs.GetInt(recordingQualityPreferenceKey, (int)RecordingQuality.Medium);

                return savedRecordingQuality switch
                {
                    (int)RecordingQuality.Low => RecordingQuality.Low,
                    (int)RecordingQuality.Medium => RecordingQuality.Medium,
                    (int)RecordingQuality.High => RecordingQuality.High,
                    (int)RecordingQuality.Ultra => RecordingQuality.Ultra,

                    _ => RecordingQuality.Medium
                };
            }

            void SetRecordingFPS(int FPS)
            {
                if (recording)
                {
                    return;
                }

                recordingFPS = FPS;
                EditorPrefs.SetInt(recordingFPSPreferenceKey, recordingFPS);

                UpdateRecordingSettingsTooltip();
            }

            void SetRecordingQuality(RecordingQuality quality)
            {
                if (recording)
                {
                    return;
                }

                recordingQuality = quality;
                EditorPrefs.SetInt(recordingQualityPreferenceKey, (int)recordingQuality);

                UpdateRecordingSettingsTooltip();
            }

            void UpdateRecordingSettingsTooltip()
            {
                RenderTexture gameViewRenderTexture = gameViewContext.GetRenderTexture();

                string recordingResolution = "Unavailable";

                if ((gameViewRenderTexture != null) && gameViewRenderTexture.IsCreated())
                {
                    recordingResolution = $"{gameViewRenderTexture.width} × {gameViewRenderTexture.height}";
                }

                string recordingBitRate = FormatBitRate(GetRecordingTargetBitRate(recordingQuality));

                string recordingSettingsInformation = $"Resolution: {recordingResolution}\n"
                    + $"Rate: {recordingFPS} FPS @ {recordingBitRate}";

                recordingButton.tooltip = "Record current Game View to MP4.\n\n" + recordingSettingsInformation;
                recordingSettingsButton.tooltip = "Recording settings.\n\n" + recordingSettingsInformation;
            }

            void SetRecordingControlsActive(bool active)
            {
                recordingButton.text = GetRecordingButtonText(active);

                recordingButton.clicked -= StartRecording;
                recordingButton.clicked -= StopRecording;

                if (active)
                {
                    recordingButton.clicked += StopRecording;
                }
                else
                {
                    recordingButton.clicked += StartRecording;
                }

                GameViewIntegratedToolbarHost.SetFixedWidth(

                    recordingButton,
                    active ? recordingStopButtonWidth : recordingButtonWidth

                );

                recordingSettingsButton.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
                pauseRecordingButton.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
                cancelRecordingButton.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

                if (active)
                {
                    UpdateRecordingPauseButton();
                    recordingButton.tooltip = "Stop recording and choose where to save video.";
                }
                else
                {
                    UpdateRecordingSettingsTooltip();
                }
            }

            void StartRecording()
            {
                if (recording)
                {
                    return;
                }

                RenderTexture gameViewRenderTexture = gameViewContext.GetRenderTexture();

                if ((gameViewRenderTexture == null) || !gameViewRenderTexture.IsCreated())
                {
                    return;
                }

                recordingWidth = gameViewRenderTexture.width;
                recordingHeight = gameViewRenderTexture.height;

                temporaryRecordingFilePath = Path.Combine(

                    Path.GetTempPath(),
                    $"OH, SNAP Recording - {Guid.NewGuid():N}.mp4"

                );

                H264EncoderAttributes h264EncoderAttributes = new()
                {
                    gopSize = (uint)(recordingFPS * 2),
                    numConsecutiveBFrames = 2,
                    profile = VideoEncodingProfile.H264High
                };

                VideoTrackEncoderAttributes videoTrackEncoderAttributes = new(h264EncoderAttributes)
                {
                    frameRate = new MediaRational(recordingFPS),

                    width = (uint)recordingWidth,
                    height = (uint)recordingHeight,

                    includeAlpha = false,

                    targetBitRate = GetRecordingTargetBitRate(recordingQuality)
                };

                try
                {
                    mediaEncoder = new MediaEncoder(temporaryRecordingFilePath, videoTrackEncoderAttributes);

                    recordingReadbackTexture = new RenderTexture(

                        recordingWidth, recordingHeight, 0,
                        RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default

                    )
                    {
                        name = "Game View Recording Readback",
                        hideFlags = HideFlags.HideAndDontSave,

                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp
                    };

                    if (!recordingReadbackTexture.Create())
                    {
                        throw new InvalidOperationException("Game View recording readback texture could not be created.");
                    }

                    recordingFrameTexture = new Texture2D(

                        recordingWidth, recordingHeight,
                        TextureFormat.RGBA32, false, false

                    )
                    {
                        name = "Game View Recording Frame",
                        hideFlags = HideFlags.HideAndDontSave
                    };

                    recordedFrameCount = 0;

                    accumulatedRecordingActiveDuration = 0.0;
                    recordingActiveSegmentStartTime = EditorApplication.timeSinceStartup;

                    recordingPaused = false;
                    recording = true;

                    SetRecordingControlsActive(true);

                    CaptureRecordingFrame();
                }
                catch
                {
                    DiscardRecording(true);
                    throw;
                }
            }

            public void Update()
            {
                if (!recording)
                {
                    UpdateRecordingSettingsTooltip();
                    return;
                }

                UpdateRecordingPauseButton();

                if (recordingPaused)
                {
                    return;
                }

                try
                {
                    double recordingActiveDuration = GetRecordingActiveDuration(EditorApplication.timeSinceStartup);
                    SynchronizeRecordingFrameCount(recordingActiveDuration);
                }
                catch (Exception exception)
                {
                    DiscardRecording(true);
                    Debug.LogException(exception);
                }
            }

            void SynchronizeRecordingFrameCount(double recordingActiveDuration)
            {
                long requiredRecordingFrameCount = Math.Max(

                    1L,
                    (long)Math.Round(recordingActiveDuration * recordingFPS, MidpointRounding.AwayFromZero)

                );

                long recordingFramesToAppend = requiredRecordingFrameCount - recordedFrameCount;

                if (recordingFramesToAppend <= 0)
                {
                    return;
                }

                for (long repeatedRecordingFrameIndex = 1; repeatedRecordingFrameIndex < recordingFramesToAppend; ++repeatedRecordingFrameIndex)
                {
                    AppendRecordingFrame();
                }

                CaptureRecordingFrame();
                UpdateRecordingPauseButton();
            }

            void CaptureRecordingFrame()
            {
                RenderTexture gameViewRenderTexture = gameViewContext.GetRenderTexture();

                if ((gameViewRenderTexture == null) || !gameViewRenderTexture.IsCreated())
                {
                    throw new InvalidOperationException("Unity Game View render texture is unavailable.");
                }

                RenderTexture previousActiveRenderTexture = RenderTexture.active;

                try
                {
                    Graphics.Blit(

                        gameViewRenderTexture,
                        recordingReadbackTexture,

                        new Vector2(1.0f, -1.0f),
                        new Vector2(0.0f, 1.0f)

                    );

                    RenderTexture.active = recordingReadbackTexture;

                    recordingFrameTexture.ReadPixels(

                        new Rect(0.0f, 0.0f, recordingWidth, recordingHeight),
                        0, 0, false

                    );

                    recordingFrameTexture.Apply(false, false);

                    AppendRecordingFrame();
                }
                finally
                {
                    RenderTexture.active = previousActiveRenderTexture;
                }
            }

            void AppendRecordingFrame()
            {
                if (!mediaEncoder.AddFrame(recordingFrameTexture))
                {
                    throw new InvalidOperationException("Unity MediaEncoder rejected Game View recording frame.");
                }

                ++recordedFrameCount;
            }

            void ToggleRecordingPause()
            {
                if (!recording)
                {
                    return;
                }

                double currentTime = EditorApplication.timeSinceStartup;

                if (recordingPaused)
                {
                    recordingActiveSegmentStartTime = currentTime;
                    recordingPaused = false;
                }
                else
                {
                    accumulatedRecordingActiveDuration = GetRecordingActiveDuration(currentTime);
                    recordingPaused = true;

                    try
                    {
                        SynchronizeRecordingFrameCount(accumulatedRecordingActiveDuration);
                    }
                    catch
                    {
                        DiscardRecording(true);
                        throw;
                    }
                }

                UpdateRecordingPauseButton();
            }

            void StopRecording()
            {
                if (!recording)
                {
                    return;
                }

                string completedTemporaryRecordingFilePath = temporaryRecordingFilePath;

                try
                {
                    double currentTime = EditorApplication.timeSinceStartup;

                    if (!recordingPaused)
                    {
                        accumulatedRecordingActiveDuration = GetRecordingActiveDuration(currentTime);
                        recordingPaused = true;
                    }

                    SynchronizeRecordingFrameCount(accumulatedRecordingActiveDuration);
                    FinishRecording();

                    string filePath = EditorUtility.SaveFilePanel(

                        "Save Game View Recording", string.Empty,
                        $"OH, SNAP - {DateTime.Now:yyyy-MM-dd @ HH-mm-ss}.mp4", "mp4"

                    );

                    if (string.IsNullOrEmpty(filePath))
                    {
                        DeleteFileIfPresent(completedTemporaryRecordingFilePath);
                        return;
                    }

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    File.Move(completedTemporaryRecordingFilePath, filePath);

                    string projectRelativePath = FileUtil.GetProjectRelativePath(filePath);

                    if (!string.IsNullOrEmpty(projectRelativePath))
                    {
                        AssetDatabase.ImportAsset(projectRelativePath);
                    }
                }
                catch
                {
                    if (recording)
                    {
                        DiscardRecording(true);
                    }
                    else
                    {
                        DeleteFileIfPresent(completedTemporaryRecordingFilePath);
                    }

                    throw;
                }
            }

            void CancelRecording()
            {
                if (!recording)
                {
                    return;
                }

                DiscardRecording(true);
            }

            void FinishRecording()
            {
                ReleaseRecordingResources(deleteTemporaryRecording: false, updateControls: true);
            }

            void DiscardRecording(bool updateControls)
            {
                ReleaseRecordingResources(deleteTemporaryRecording: true, updateControls: updateControls);
            }

            void ReleaseRecordingResources(bool deleteTemporaryRecording, bool updateControls)
            {
                string temporaryRecordingFilePathToRelease = temporaryRecordingFilePath;

                recording = false;
                recordingPaused = false;

                try
                {
                    mediaEncoder?.Dispose();
                }
                finally
                {
                    mediaEncoder = null;

                    DestroyRecordingTextures();

                    temporaryRecordingFilePath = null;

                    if (deleteTemporaryRecording)
                    {
                        DeleteFileIfPresent(temporaryRecordingFilePathToRelease);
                    }

                    if (updateControls)
                    {
                        SetRecordingControlsActive(false);
                    }
                }
            }

            void DestroyRecordingTextures()
            {
                if (recordingReadbackTexture != null)
                {
                    Object.DestroyImmediate(recordingReadbackTexture);
                    recordingReadbackTexture = null;
                }

                if (recordingFrameTexture != null)
                {
                    Object.DestroyImmediate(recordingFrameTexture);
                    recordingFrameTexture = null;
                }
            }

            static void DeleteFileIfPresent(string filePath)
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            public void HandleGameViewEvent(Event gameViewEvent)
            {

            }

            public void DrawGameViewOverlay()
            {

            }

            // ...

            public void Dispose()
            {
                if (recording || (mediaEncoder != null) || !string.IsNullOrEmpty(temporaryRecordingFilePath))
                {
                    DiscardRecording(false);
                }

                DestroyRecordingTextures();
            }
        }
    }
}
