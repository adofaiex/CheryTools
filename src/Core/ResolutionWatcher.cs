using UnityEngine;

namespace CheryTools
{
    // Watches the game screen resolution and, when it changes, rescales the live
    // KV/OV layouts so they keep their relative placement. The layout is scaled
    // from Settings.LastKnownScreenWidth/Height (the resolution the layout was last
    // adapted to) to the current resolution. Runs regardless of whether KV or OV
    // is enabled, and ignores changes while the mod itself is disabled.
    internal class ResolutionWatcher : MonoBehaviour
    {
        // Resolution changes (especially fullscreen/windowed toggles) report several
        // intermediate sizes over a few frames. Wait until the size has been stable
        // for this long before applying the rescale, so we scale once, not per step.
        private const float SettleSeconds = 0.5f;

        // Sampling Screen.width/height every frame is cheap but pointless; the size
        // cannot change faster than the OS delivers it, so poll at a modest rate.
        private const float PollIntervalSeconds = 0.1f;

        private int _stableWidth;
        private int _stableHeight;
        private int _pendingWidth;
        private int _pendingHeight;
        private float _pendingSince = -1f;
        private float _nextPollTime;

        private void Start()
        {
            CheryToolsAssets.GetCurrentScreenSizeInternal(out _stableWidth, out _stableHeight);
            _pendingWidth = _stableWidth;
            _pendingHeight = _stableHeight;

            // If the game was restarted at a different resolution than the layout was
            // last adapted to, adapt once at startup so the layout matches this screen.
            // When no baseline exists yet (fresh/legacy settings) we just adopt the
            // current resolution as the baseline without scaling, since the original
            // design resolution is unknown.
            if (Main.IsEnabled && Main.Settings != null)
            {
                int knownW = Main.Settings.LastKnownScreenWidth;
                int knownH = Main.Settings.LastKnownScreenHeight;
                bool hasBaseline = knownW > 0 && knownH > 0;

                if (hasBaseline && Main.Settings.ResolutionAutoAdaptEnabled
                    && (knownW != _stableWidth || knownH != _stableHeight))
                {
                    CheryToolsAssets.TryAdaptSettingsToCurrentResolution(Main.Settings, knownW, knownH, true);
                    Main.Settings.LastKnownScreenWidth = _stableWidth;
                    Main.Settings.LastKnownScreenHeight = _stableHeight;
                    Main.Logger?.Log(string.Format(
                        "[CheryTools] Startup resolution {0}x{1} differs from layout baseline {2}x{3}; rescaled KV/OV layout.",
                        _stableWidth, _stableHeight, knownW, knownH));
                    if (KeyViewerManager.Instance != null)
                    {
                        KeyViewerManager.Instance.RefreshKeys();
                    }
                    Main.RequestSave();
                }
                else
                {
                    EnsureBaselineInitialized();
                }
            }
        }

        private void Update()
        {
            if (!Main.IsEnabled || Main.Settings == null) return;

            // Both enabled and disabled paths only need occasional resolution
            // samples. Previously the disabled path queried the live screen size
            // every frame even though it only maintains a baseline.
            float now = Time.unscaledTime;
            if (now < _nextPollTime) return;
            _nextPollTime = now + PollIntervalSeconds;

            if (!Main.Settings.ResolutionAutoAdaptEnabled)
            {
                // Keep the baseline tracking the live size even while disabled, so
                // re-enabling does not trigger a spurious rescale.
                SyncBaselineToCurrent();
                return;
            }

            int width;
            int height;
            CheryToolsAssets.GetCurrentScreenSizeInternal(out width, out height);
            if (width <= 0 || height <= 0) return;

            if (width == _stableWidth && height == _stableHeight)
            {
                // Back to the settled size: cancel any pending intermediate change.
                _pendingSince = -1f;
                return;
            }

            if (width != _pendingWidth || height != _pendingHeight)
            {
                _pendingWidth = width;
                _pendingHeight = height;
                _pendingSince = now;
                return;
            }

            if (_pendingSince < 0f || now - _pendingSince < SettleSeconds)
            {
                return;
            }

            ApplyResolutionChange(width, height);
        }

        private void EnsureBaselineInitialized()
        {
            if (!Main.IsEnabled || Main.Settings == null) return;
            if (Main.Settings.LastKnownScreenWidth > 0 && Main.Settings.LastKnownScreenHeight > 0) return;

            int width;
            int height;
            CheryToolsAssets.GetCurrentScreenSizeInternal(out width, out height);
            if (width <= 0 || height <= 0) return;

            Main.Settings.LastKnownScreenWidth = width;
            Main.Settings.LastKnownScreenHeight = height;
        }

        private void SyncBaselineToCurrent()
        {
            int width;
            int height;
            CheryToolsAssets.GetCurrentScreenSizeInternal(out width, out height);
            if (width <= 0 || height <= 0) return;
            _stableWidth = width;
            _stableHeight = height;
            _pendingSince = -1f;

            if (Main.Settings == null) return;
            if (Main.Settings.LastKnownScreenWidth > 0 && Main.Settings.LastKnownScreenHeight > 0) return;
            Main.Settings.LastKnownScreenWidth = width;
            Main.Settings.LastKnownScreenHeight = height;
        }

        private void ApplyResolutionChange(int newWidth, int newHeight)
        {
            int sourceWidth = Main.Settings.LastKnownScreenWidth;
            int sourceHeight = Main.Settings.LastKnownScreenHeight;

            _stableWidth = newWidth;
            _stableHeight = newHeight;
            _pendingSince = -1f;

            if (sourceWidth > 0 && sourceHeight > 0
                && (sourceWidth != newWidth || sourceHeight != newHeight))
            {
                bool adapted = CheryToolsAssets.TryAdaptSettingsToCurrentResolution(Main.Settings, sourceWidth, sourceHeight, true);
                if (adapted)
                {
                    Main.Logger?.Log(string.Format(
                        "[CheryTools] Resolution changed {0}x{1} -> {2}x{3}; rescaled KV/OV layout.",
                        sourceWidth, sourceHeight, newWidth, newHeight));
                }
            }

            Main.Settings.LastKnownScreenWidth = newWidth;
            Main.Settings.LastKnownScreenHeight = newHeight;

            if (KeyViewerManager.Instance != null)
            {
                KeyViewerManager.Instance.RefreshKeys();
            }
            // RequestSave also bumps the overlay render revision, so the next frame
            // redraws with the rescaled layout.
            Main.RequestSave();
        }
    }
}
