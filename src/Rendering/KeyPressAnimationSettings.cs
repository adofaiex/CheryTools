using UnityEngine;

namespace CheryTools
{
    internal struct KeyPressAnimationSettings
    {
        public bool Enabled;
        public float Duration;
        public string Easing;
        public bool AffectColors;
        public float Scale;
        public float OffsetX;
        public float OffsetY;

        public static KeyPressAnimationSettings Resolve(KVConfiguration config, KVNode node)
        {
            bool useNode = node != null && node.UseCustomKeyPressAnimation;
            KeyPressAnimationSettings settings = new KeyPressAnimationSettings
            {
                Enabled = useNode ? node.KeyPressAnimationEnabled : (config != null && config.KeyPressAnimationEnabled),
                Duration = useNode ? node.KeyPressAnimationDuration : (config != null ? config.KeyPressAnimationDuration : 0.12f),
                Easing = useNode ? node.KeyPressAnimationEasing : (config != null ? config.KeyPressAnimationEasing : "ease-out-quad"),
                AffectColors = useNode ? node.KeyPressAnimationAffectColors : (config == null || config.KeyPressAnimationAffectColors),
                Scale = useNode ? node.KeyPressAnimationScale : (config != null ? config.KeyPressAnimationScale : 1f),
                OffsetX = useNode ? node.KeyPressAnimationOffsetX : (config != null ? config.KeyPressAnimationOffsetX : 0f),
                OffsetY = useNode ? node.KeyPressAnimationOffsetY : (config != null ? config.KeyPressAnimationOffsetY : 0f)
            };

            settings.Duration = Mathf.Clamp(float.IsNaN(settings.Duration) || float.IsInfinity(settings.Duration) ? 0.12f : settings.Duration, 0.01f, 2f);
            settings.Scale = Mathf.Clamp(float.IsNaN(settings.Scale) || float.IsInfinity(settings.Scale) ? 1f : settings.Scale, 0.2f, 3f);
            settings.OffsetX = Mathf.Clamp(float.IsNaN(settings.OffsetX) || float.IsInfinity(settings.OffsetX) ? 0f : settings.OffsetX, -200f, 200f);
            settings.OffsetY = Mathf.Clamp(float.IsNaN(settings.OffsetY) || float.IsInfinity(settings.OffsetY) ? 0f : settings.OffsetY, -200f, 200f);
            if (string.IsNullOrEmpty(settings.Easing))
            {
                settings.Easing = "ease-out-quad";
            }
            return settings;
        }
    }
}
