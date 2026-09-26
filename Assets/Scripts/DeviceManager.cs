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

// ── GPU model lists ───────────────────────────────────────────────────
        // Matched as case-insensitive substrings against SystemInfo.graphicsDeviceName.
        //
        // Full model strings are used deliberately. Short prefixes are tempting but dangerous:
        // "Adreno 50" would also match Adreno 530 (Snapdragon 820), which is still capable, and
        // "Mali-G6" would catch mid-range chips like Mali-G68. More entries, no false positives.
        //
        // These lists are a starting point based on Android GPU tier research. Tune them from real
        // device testing — log the GPU name on each new device and add anything that struggles.

        /// <summary>
        /// GPUs that cannot render high-resolution UI at all. These get UltraLow (720p, 0.65 scale).
        /// Legacy/entry/bottom-tier parts — mostly Mali-400 era through Mali-G57.
        /// </summary>
        private static readonly string[] VeryWeakGpuModels =
        {
            // Legacy / obsolete
            "PowerVR SGX530", "PowerVR SGX543", "PowerVR SGX544",
            "VideoCore IV", "Tegra 4",
            "Adreno 200", "Adreno 203", "Adreno 205",
            "Adreno 302", "Adreno 304", "Adreno 306", "Adreno 320", "Adreno 405",
            "Mali-400", "Mali-T720", "Mali-T760", "Mali-T820", "Mali-T830", "Mali-T860",

            // Bottom tier / entry
            "PowerVR G6200",
            "PowerVR GE8100", "PowerVR GE8300", "PowerVR GE8320", "PowerVR GE8322",
            "IMG8322",

            // Budget low-end
            "Adreno 504", "Adreno 505", "Adreno 506",
            "Adreno 610", "Adreno 611", "Adreno 612",

            // Recent low-entry
            "Mali-G31", "Mali-G51", "Mali-G52", "Mali-G57",
        };

        /// <summary>
        /// Budget GPUs that need reduced load but can manage 1080p. These get Low (1080p, 0.85 scale).
        /// </summary>
        private static readonly string[] WeakGpuModels =
        {
            "Adreno 509", "Adreno 512",
            "Adreno 613", "Adreno 615", "Adreno 616", "Adreno 618", "Adreno 619",
            "Mali-G68", "Mali-G610",
            "PowerVR GM9446",
        };

        /// <summary>
        /// Pixel count above which a device is considered to have a heavy display load.
        /// Logged for diagnostics only — deliberate: testing showed a Mali-G57 stalled even at
        /// 1.18M px, so the GPU model is a far better predictor of fill-rate trouble than the
        /// pixel count. Wire this into DetectTier only if evidence later supports it.
        /// </summary>
        private const long PixelLoadThreshold = 2_500_000;

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
        // iOS devices (iPhones & iPads) have high per-core performance, powerful Apple GPUs, and Metal API optimization,
        // so low-end tier downgrades are unnecessary and only apply to Android hardware.
#if UNITY_IOS
        Debug.Log("[DeviceTierManager] Running on iOS device. Defaulting to Normal tier.");
        return DeviceTier.Normal;
#elif !UNITY_ANDROID && !UNITY_EDITOR
        return DeviceTier.Normal;
#endif

        int ram = SystemInfo.systemMemorySize;
        int cores = SystemInfo.processorCount;
        int vram = SystemInfo.graphicsMemorySize;
        string gpuName = SystemInfo.graphicsDeviceName ?? string.Empty;

        // Pixel load: the metric that actually predicts a fill-rate stall. Logged but NOT yet used
        // for tier decisions — we're gathering per-device numbers first so the threshold is set from
        // real hardware rather than guessed. See PixelLoadThreshold below.
        long screenPixels = (long)Screen.currentResolution.width * Screen.currentResolution.height;
        bool heavyPixelLoad = screenPixels >= PixelLoadThreshold;

        bool gpuVeryWeak = MatchesAny(gpuName, VeryWeakGpuModels);
        bool gpuWeak = gpuVeryWeak || MatchesAny(gpuName, WeakGpuModels);

        // Score using the Low thresholds (looser)
        float lowRatio = ComputeWeaknessRatio(
            ram, cores, vram, gpuWeak,
            lowRamThresholdMB, lowCoreCountThreshold, lowGraphicsMemThresholdMB);

        // Score using the UltraLow thresholds (stricter)
        float ultraLowRatio = ComputeWeaknessRatio(
            ram, cores, vram, gpuVeryWeak,
            ultraLowRamThresholdMB, ultraLowCoreCountThreshold, ultraLowGraphicsMemThresholdMB);

        DeviceTier result;
        string reason;

        if (gpuVeryWeak)
        {
            // A GPU on the very-weak list is the most reliable predictor of a fill-rate stall, which
            // is what low-end Android actually fails at. The ratio score dilutes the GPU signal to
            // one vote among four, so a Mali-G57 with 4GB RAM and 8 cores previously scored 0.25 and
            // landed on Low (0.85 scale) — still far too heavy for that GPU.
            result = DeviceTier.UltraLow;
            reason = "GPU on very-weak list";
        }
        else if (ultraLowRatio >= ultraLowRatioCutoff)
        {
            result = DeviceTier.UltraLow;
            reason = $"ultraLowRatio {ultraLowRatio:0.00} >= {ultraLowRatioCutoff:0.00}";
        }
        else if (gpuWeak || lowRatio >= lowRatioCutoff)
        {
            result = DeviceTier.Low;
            reason = gpuWeak
                ? "GPU on weak list"
                : $"lowRatio {lowRatio:0.00} >= {lowRatioCutoff:0.00}";
        }
        else
        {
            result = DeviceTier.Normal;
            reason = "no weak signals";
        }

        Debug.Log($"[DeviceTierManager] RAM={ram}MB, Cores={cores}, VRAM={vram}MB, GPU=\"{gpuName}\", " +
                  $"gpuVeryWeak={gpuVeryWeak}, gpuWeak={gpuWeak}, " +
                  $"lowRatio={lowRatio:0.00}, ultraLowRatio={ultraLowRatio:0.00} " +
                  $"=> Tier={result} ({reason})");

        // Diagnostic only — pixel load is not yet a tier input. Use this on real devices to decide
        // a sensible PixelLoadThreshold, then wire it in.
        Debug.Log($"[DeviceTierManager] Display={Screen.currentResolution.width}x{Screen.currentResolution.height} " +
                  $"({screenPixels / 1_000_000f:0.00}M px), dpi={Screen.dpi}, " +
                  $"heavyPixelLoad={heavyPixelLoad} (threshold {PixelLoadThreshold / 1_000_000f:0.0}M)");

        return result;
    }

    /// <summary>True if the GPU name contains any of the supplied model strings (case-insensitive).</summary>
    private static bool MatchesAny(string gpuName, string[] models)
    {
        if (string.IsNullOrEmpty(gpuName) || models == null) return false;

        foreach (var model in models)
        {
            if (!string.IsNullOrEmpty(model) &&
                gpuName.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }
        return false;
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

        var urpAsset = (GraphicsSettings.currentRenderPipeline ?? QualitySettings.renderPipeline) as UniversalRenderPipelineAsset;

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