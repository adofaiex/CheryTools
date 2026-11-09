using System.Collections.Generic;
using CheryTools.Sonnet.Shared;
using UnityEngine;

namespace CheryTools.Tools.Preview
{
    internal static class ToolsVisualRuntime
    {
        private static bool _restoringDefaults;
        private static bool _customColorsApplied;
        private static readonly Dictionary<PlanetRenderer, PlanetAlphaState> PlanetAlphaStates =
            new Dictionary<PlanetRenderer, PlanetAlphaState>();

        private sealed class PlanetAlphaState
        {
            internal float Body = 1f;
            internal float Ring = 1f;
            internal float Tail = 1f;
        }

        internal static bool ShouldHideHitText(HitMargin margin)
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            if (ToolsUiVisibilityRuntime.ShouldHideJudgment()) return true;
            if (!ToolsRuntime.IsActive || settings == null || !settings.HideHitTextEnabled) return false;
            switch (SonnetJudgements.Of(margin))
            {
                case SonnetJudgement.TooEarly: return settings.HideTooEarly;
                case SonnetJudgement.VeryEarly: return settings.HideVeryEarly;
                case SonnetJudgement.EarlyPerfect: return settings.HideEarlyPerfect;
                case SonnetJudgement.PerfectMinus:
                case SonnetJudgement.Perfect:
                case SonnetJudgement.PerfectPlus:
                    return settings.HidePerfect;
                case SonnetJudgement.LatePerfect: return settings.HideLatePerfect;
                case SonnetJudgement.VeryLate: return settings.HideVeryLate;
                case SonnetJudgement.TooLate: return settings.HideTooLate;
                case SonnetJudgement.Multipress: return settings.HideMultipress;
                case SonnetJudgement.FailMiss: return settings.HideFailMiss;
                case SonnetJudgement.FailOverload: return settings.HideFailOverload;
                case SonnetJudgement.OverPress: return settings.HideOverPress;
                default: return false;
            }
        }

        internal static void ApplyCustomColors()
        {
            if (_restoringDefaults) return;
            ToolsSettings settings = ToolsRuntime.Settings;
            if (!ToolsRuntime.IsActive || settings == null || !settings.EnableCustomPlanetColors) return;
            scrController controller = scrController.instance;
            if (controller == null) return;
            Apply(controller.planetRed?.planetRenderer, settings.RedPlanetColor, settings.RedRingColor, settings.RedTailColor,
                settings.RedPlanetOpacity, settings.RedRingOpacity, settings.RedTailOpacity);
            Apply(controller.planetBlue?.planetRenderer, settings.BluePlanetColor, settings.BlueRingColor, settings.BlueTailColor,
                settings.BluePlanetOpacity, settings.BlueRingOpacity, settings.BlueTailOpacity);
            Apply(controller.planetGreen?.planetRenderer, settings.GreenPlanetColor, settings.GreenRingColor, settings.GreenTailColor,
                settings.GreenPlanetOpacity, settings.GreenRingOpacity, settings.GreenTailOpacity);
        }

        internal static void Refresh()
        {
            if (_restoringDefaults) return;
            ToolsSettings settings = ToolsRuntime.Settings;
            if (!ToolsRuntime.IsActive || settings == null) return;
            if (settings.EnableCustomPlanetColors)
            {
                ApplyCustomColors();
                _customColorsApplied = true;
            }
            else if (_customColorsApplied)
            {
                // Restore the game's base colors before applying opacity. Otherwise
                // disabling custom colors while opacity remains enabled leaves the
                // previous custom RGB values on the planets.
                RestoreDefaultColors();
                _customColorsApplied = false;
            }
            if (settings.EnablePlanetOpacity)
            {
                scrController controller = scrController.instance;
                if (controller == null) return;
                ApplyPlanetOpacity(controller.planetRed?.planetRenderer);
                ApplyPlanetOpacity(controller.planetBlue?.planetRenderer);
                ApplyPlanetOpacity(controller.planetGreen?.planetRenderer);
            }
            else if (!settings.EnableCustomPlanetColors && PlanetAlphaStates.Count > 0)
            {
                // Turning both features off must immediately hand ownership back
                // to the game instead of leaving the last alpha values applied.
                RestoreDefaultColors();
            }
        }

        internal static void ApplyPlanetOpacity(PlanetRenderer renderer)
        {
            ToolsSettings settings = ToolsRuntime.Settings;
            if (!ToolsRuntime.IsActive || settings == null || !settings.EnablePlanetOpacity || renderer == null) return;
            PlanetAlphaState state;
            if (!PlanetAlphaStates.TryGetValue(renderer, out state))
            {
                state = new PlanetAlphaState();
                state.Body = renderer.sprite != null ? renderer.sprite.color.a : 1f;
                state.Ring = renderer.ringComp != null ? renderer.ringComp.color.a : 1f;
                state.Tail = renderer.tailParticles != null ? renderer.tailParticles.main.startColor.color.a : 1f;
                PlanetAlphaStates[renderer] = state;
            }
            bool red = IsRed(renderer);
            bool blue = IsBlue(renderer);
            float body = red ? settings.RedPlanetOpacity : blue ? settings.BluePlanetOpacity : settings.GreenPlanetOpacity;
            float ring = red ? settings.RedRingOpacity : blue ? settings.BlueRingOpacity : settings.GreenRingOpacity;
            float tail = red ? settings.RedTailOpacity : blue ? settings.BlueTailOpacity : settings.GreenTailOpacity;
            float bodyBase = settings.EnableCustomPlanetColors
                ? (red ? settings.RedPlanetColor[3] : blue ? settings.BluePlanetColor[3] : settings.GreenPlanetColor[3])
                : state.Body;
            float ringBase = settings.EnableCustomPlanetColors
                ? (red ? settings.RedRingColor[3] : blue ? settings.BlueRingColor[3] : settings.GreenRingColor[3])
                : state.Ring;
            float tailBase = settings.EnableCustomPlanetColors
                ? (red ? settings.RedTailColor[3] : blue ? settings.BlueTailColor[3] : settings.GreenTailColor[3])
                : state.Tail;
            float bodyAlpha = bodyBase * body / 100f;
            float ringAlpha = ringBase * ring / 100f;
            float tailAlpha = tailBase * tail / 100f;
            SetPlanetSpriteAlpha(renderer.sprite, bodyAlpha);
            SetSpriteAlpha(renderer.faceSprite, bodyAlpha);
            SetSpriteAlpha(renderer.glow, bodyAlpha * 0.5f);
            SetParticleAlpha(renderer.sparks, bodyAlpha);
            SetParticleAlpha(renderer.coreParticles, bodyAlpha);
            if (renderer.ringComp != null)
            {
                Color ringColor = renderer.ringComp.color;
                ringColor.a = Mathf.Clamp01(ringAlpha);
                renderer.ringComp.color = ringColor;
            }
            if (renderer.tailParticles != null)
            {
                ParticleSystem.MainModule main = renderer.tailParticles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(main.startColor.color, tailAlpha));
            }
            SetParticleAlpha(renderer.deathExplosion, tailAlpha);
        }

        internal static void RestoreDefaultColors()
        {
            scrController controller = scrController.instance;
            if (controller == null) return;
            _restoringDefaults = true;
            try
            {
                if (controller.planetRed?.planetRenderer != null) controller.planetRed.planetRenderer.LoadPlanetColor(true);
                if (controller.planetBlue?.planetRenderer != null) controller.planetBlue.planetRenderer.LoadPlanetColor(false);
                if (controller.planetGreen?.planetRenderer != null)
                    controller.planetGreen.planetRenderer.SetColor(new PlanetColor(new Color(0.3f, 0.7f, 0f, 1f)));
            }
            finally
            {
                _restoringDefaults = false;
                PlanetAlphaStates.Clear();
                _customColorsApplied = false;
            }
        }

        private static void Apply(PlanetRenderer renderer, float[] planet, float[] ring, float[] tail,
            float planetOpacity, float ringOpacity, float tailOpacity)
        {
            if (renderer == null) return;
            CaptureAlphaState(renderer);
            renderer.EnableCustomColor();
            renderer.SetPlanetColor(ToColor(planet));
            if (renderer.ringComp != null) renderer.ringComp.color = ToColor(ring);
            renderer.SetTailColor(ToColor(tail));
            if (ToolsRuntime.Settings.EnablePlanetOpacity) ApplyPlanetOpacity(renderer);
        }

        private static void CaptureAlphaState(PlanetRenderer renderer)
        {
            if (renderer == null || PlanetAlphaStates.ContainsKey(renderer)) return;
            PlanetAlphaState state = new PlanetAlphaState();
            state.Body = renderer.sprite != null ? renderer.sprite.color.a : 1f;
            state.Ring = renderer.ringComp != null ? renderer.ringComp.color.a : 1f;
            state.Tail = renderer.tailParticles != null ? renderer.tailParticles.main.startColor.color.a : 1f;
            PlanetAlphaStates[renderer] = state;
        }

        private static bool IsRed(PlanetRenderer renderer)
        {
            scrController controller = scrController.instance;
            return renderer != null && controller != null && controller.planetRed != null &&
                controller.planetRed.planetRenderer == renderer;
        }

        private static bool IsBlue(PlanetRenderer renderer)
        {
            scrController controller = scrController.instance;
            return renderer != null && controller != null && controller.planetBlue != null &&
                controller.planetBlue.planetRenderer == renderer;
        }

        private static void SetPlanetSpriteAlpha(PlanetSprite sprite, float alpha)
        {
            if (sprite == null) return;
            Color color = sprite.color;
            color.a = Mathf.Clamp01(alpha);
            sprite.color = color;
        }

        private static void SetSpriteAlpha(SpriteRenderer sprite, float alpha)
        {
            if (sprite == null) return;
            Color color = sprite.color;
            color.a = Mathf.Clamp01(alpha);
            sprite.color = color;
        }

        private static void SetParticleAlpha(ParticleSystem particles, float alpha)
        {
            if (particles == null) return;
            ParticleSystem.MainModule main = particles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(main.startColor.color, alpha));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static Color ToColor(float[] value)
        {
            return value != null && value.Length >= 4
                ? new Color(value[0], value[1], value[2], value[3])
                : Color.white;
        }
    }
}
