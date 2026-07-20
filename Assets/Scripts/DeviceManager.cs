using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DeviceManager : MonoBehaviour
{
    public enum DeviceTier
    {
        Normal,
        Low,
        UltraLow
    }

    [Header("Debug Override")]
    [Tooltip("If enabled, skips detection and forces the tier below. Useful for testing each tier in-Editor without needing the actual hardware.")]
    public bool forceTierForTesting = false;
    public DeviceTier forcedTier = DeviceTier.Normal;

    [Header("Thresholds — RAM (MB)")]
    [Tooltip("Devices at or below this RAM count as a 'weak' RAM signal for Low tier.")]
    public int lowRamThresholdMB = 4000;

    [Tooltip("Devices at or below this RAM count as a 'weak' RAM signal for UltraLow tier. Should be lower than the Low threshold above.")]
    public int ultraLowRamThresholdMB = 3000;

    [Header("Thresholds — CPU cores")]
    [Tooltip("Devices at or below this core count count as a 'weak' CPU signal for Low tier.")]
    public int lowCoreCountThreshold = 8;

    [Tooltip("Devices at or below this core count count as a 'weak' CPU signal for UltraLow tier. Should be lower than or equal to the Low threshold above.")]
    public int ultraLowCoreCountThreshold = 4;

    [Header("Thresholds — Graphics memory (MB)")]
    [Tooltip("Devices at or below this VRAM count as a 'weak' signal for Low tier. Skipped if the driver reports 0 (unreliable on some devices).")]
    public int lowGraphicsMemThresholdMB = 1024;

    [Tooltip("Devices at or below this VRAM count as a 'weak' signal for UltraLow tier.")]
    public int ultraLowGraphicsMemThresholdMB = 512;

    [Header("Weakness score cutoffs")]
    [Tooltip("If the fraction of 'weak' signals is at or above this, device is at least Low tier.")]
    [Range(0f, 1f)] public float lowRatioCutoff = 0.5f;

    [Tooltip("If the fraction of 'weak' signals (using the stricter UltraLow thresholds) is at or above this, device is UltraLow tier instead of just Low.")]
    [Range(0f, 1f)] public float ultraLowRatioCutoff = 0.75f;

    [Header("Tier Settings — Normal (your tested baseline)")]
    public float normalRenderScale = 1.0f;
    public int normalTargetFrameRate = 60;
    public int normalQualityLevelIndex = 2; // Medium — what you've actually built/tested against

    [Header("Tier Settings — Low")]
    public float lowRenderScale = 0.85f;
    public int lowTargetFrameRate = 30;
    public int lowQualityLevelIndex = 1; // Low

    [Header("Tier Settings — UltraLow")]
    public float ultraLowRenderScale = 0.65f;
    public int ultraLowTargetFrameRate = 30;
    public int ultraLowQualityLevelIndex = 0; // Very Low

    private static readonly string[] KnownWeakGpuFragments =
    {
        "Mali-G57",
        "Mali-G52",
        "PowerVR GE8320",
        "PowerVR GE8322",
        "Adreno 610",
        "Adreno 611",
    };

    public static DeviceManager Instance { get; private set; }
    public static DeviceTier CurrentTier { get; private set; } = DeviceTier.Normal;

    public static event Action<DeviceTier> OnTierApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        DeviceTier tier;
        if (forceTierForTesting)
        {
            tier = forcedTier;
            Debug.Log($"[DeviceTierManager] FORCED TESTING ACTIVE: Forcing Tier={tier}");
        }
        else
        {
            tier = DetectTier();
        }

        ApplyTier(tier);
    }

    private DeviceTier DetectTier()
    {
        int ram = SystemInfo.systemMemorySize;
        int cores = SystemInfo.processorCount;
        int vram = SystemInfo.graphicsMemorySize;
        string gpuName = SystemInfo.graphicsDeviceName ?? string.Empty;

        bool gpuKnownWeak = false;
        foreach (var fragment in KnownWeakGpuFragments)
        {
            if (gpuName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                gpuKnownWeak = true;
                break;
            }
        }

        // Score using the Low thresholds (looser)
        float lowRatio = ComputeWeaknessRatio(
            ram, cores, vram, gpuKnownWeak,
            lowRamThresholdMB, lowCoreCountThreshold, lowGraphicsMemThresholdMB);

        // Score using the UltraLow thresholds (stricter)
        float ultraLowRatio = ComputeWeaknessRatio(
            ram, cores, vram, gpuKnownWeak,
            ultraLowRamThresholdMB, ultraLowCoreCountThreshold, ultraLowGraphicsMemThresholdMB);

        DeviceTier result;
        if (ultraLowRatio >= ultraLowRatioCutoff)
        {
            result = DeviceTier.UltraLow;
        }
        else if (lowRatio >= lowRatioCutoff)
        {
            result = DeviceTier.Low;
        }
        else
        {
            result = DeviceTier.Normal;
        }

        Debug.Log($"[DeviceTierManager] RAM={ram}MB, Cores={cores}, VRAM={vram}MB, GPU=\"{gpuName}\", " +
                  $"lowRatio={lowRatio:0.00}, ultraLowRatio={ultraLowRatio:0.00} => Tier={result}");

        return result;
    }

    private float ComputeWeaknessRatio(int ram, int cores, int vram, bool gpuKnownWeak,
        int ramThreshold, int coreThreshold, int vramThreshold)
    {
        int weakSignals = 0;
        int totalSignals = 0;

        totalSignals++;
        if (ram > 0 && ram <= ramThreshold) weakSignals++;

        totalSignals++;
        if (cores > 0 && cores <= coreThreshold) weakSignals++;

        // Skip VRAM signal entirely if driver reports 0 — unreliable on some devices
        if (vram > 0)
        {
            totalSignals++;
            if (vram <= vramThreshold) weakSignals++;
        }

        totalSignals++;
        if (gpuKnownWeak) weakSignals++;

        return totalSignals > 0 ? (float)weakSignals / totalSignals : 0f;
    }

    private void ApplyTier(DeviceTier tier)
    {
        CurrentTier = tier;

        QualitySettings.vSyncCount = 0; // let targetFrameRate control pacing, not vSync

        var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        switch (tier)
        {
            case DeviceTier.Normal:
                Application.targetFrameRate = normalTargetFrameRate;
                if (urpAsset != null) urpAsset.renderScale = normalRenderScale;
                QualitySettings.SetQualityLevel(normalQualityLevelIndex, true);
                break;

            case DeviceTier.Low:
                Application.targetFrameRate = lowTargetFrameRate;
                if (urpAsset != null) urpAsset.renderScale = lowRenderScale;
                QualitySettings.SetQualityLevel(lowQualityLevelIndex, true);
                break;

            case DeviceTier.UltraLow:
                Application.targetFrameRate = ultraLowTargetFrameRate;
                if (urpAsset != null) urpAsset.renderScale = ultraLowRenderScale;
                QualitySettings.SetQualityLevel(ultraLowQualityLevelIndex, true);
                break;
        }

        if (urpAsset == null)
        {
            Debug.LogWarning("[DeviceTierManager] Could not find an active UniversalRenderPipelineAsset — render scale was not applied. Frame rate and quality level were still set.");
        }

        OnTierApplied?.Invoke(tier);
    }

    /// <summary>
    /// Convenience helpers for other scripts, e.g.:
    /// if (DeviceTierManager.IsAtLeastLow) { skip confetti spawn; }
    /// if (DeviceTierManager.IsUltraLow) { use flat-color background instead of photo; }
    /// </summary>
    public static bool IsAtLeastLow => CurrentTier == DeviceTier.Low || CurrentTier == DeviceTier.UltraLow;
    public static bool IsUltraLow => CurrentTier == DeviceTier.UltraLow;
}