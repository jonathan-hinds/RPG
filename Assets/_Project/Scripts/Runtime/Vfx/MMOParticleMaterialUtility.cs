using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Vfx
{
    public static class MMOParticleMaterialUtility
    {
        private static readonly Dictionary<int, Material> ParticleMaterials = new();
        private static readonly Dictionary<int, Material> LineMaterials = new();
        private static Texture2D softParticleTexture;

        public static Material GetParticleMaterial(Color color)
        {
            return GetMaterial(color, ParticleMaterials, true);
        }

        public static Material GetLineMaterial(Color color)
        {
            return GetMaterial(color, LineMaterials, false);
        }

        public static void ApplyParticleMaterial(ParticleSystem particleSystem, Color color)
        {
            if (particleSystem == null)
            {
                return;
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetParticleMaterial(color);
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.minParticleSize = 0.015f;
                renderer.maxParticleSize = 0.18f;
                renderer.sortingFudge = 8f;
            }
        }

        private static Material GetMaterial(Color color, Dictionary<int, Material> cache, bool additive)
        {
            int key = ColorUtility.ToHtmlStringRGBA(color).GetHashCode() ^ (additive ? 17 : 31);
            if (cache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Shader shader = Shader.Find(additive ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            Material material = new(shader)
            {
                name = additive ? $"Runtime_Particle_{key}" : $"Runtime_Line_{key}",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent
            };

            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            if (additive)
            {
                Texture2D texture = GetSoftParticleTexture();
                SetTextureIfPresent(material, "_BaseMap", texture);
                SetTextureIfPresent(material, "_MainTex", texture);
            }

            ConfigureTransparentMaterial(material, additive);
            cache[key] = material;
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material, bool additive)
        {
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", additive ? 1f : 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (material != null && value != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private static Texture2D GetSoftParticleTexture()
        {
            if (softParticleTexture != null)
            {
                return softParticleTexture;
            }

            const int size = 128;
            softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime_Soft_Cloud",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new((x + 0.5f) / size, (y + 0.5f) / size);
                    float alpha = EvaluateCloudAlpha(uv);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            softParticleTexture.SetPixels(pixels);
            softParticleTexture.Apply(false, true);
            return softParticleTexture;
        }

        private static float EvaluateCloudAlpha(Vector2 uv)
        {
            float alpha = 0f;
            alpha += SoftBlob(uv, new Vector2(0.5f, 0.5f), 0.52f, 0.95f);
            alpha += SoftBlob(uv, new Vector2(0.36f, 0.55f), 0.32f, 0.45f);
            alpha += SoftBlob(uv, new Vector2(0.64f, 0.46f), 0.34f, 0.42f);
            alpha += SoftBlob(uv, new Vector2(0.5f, 0.34f), 0.28f, 0.28f);
            float edgeFade = Mathf.SmoothStep(0f, 0.18f, uv.x)
                * Mathf.SmoothStep(0f, 0.18f, uv.y)
                * Mathf.SmoothStep(0f, 0.18f, 1f - uv.x)
                * Mathf.SmoothStep(0f, 0.18f, 1f - uv.y);
            float noise = 0.78f
                + Hash01(Mathf.FloorToInt(uv.x * 18f), Mathf.FloorToInt(uv.y * 18f)) * 0.16f
                + Hash01(Mathf.FloorToInt(uv.x * 37f) + 11, Mathf.FloorToInt(uv.y * 37f) + 7) * 0.06f;
            return Mathf.Clamp01(alpha * edgeFade * noise);
        }

        private static float SoftBlob(Vector2 uv, Vector2 center, float radius, float strength)
        {
            float distance = Vector2.Distance(uv, center) / Mathf.Max(0.001f, radius);
            float value = Mathf.Clamp01(1f - distance);
            value = Mathf.SmoothStep(0f, 1f, value);
            return value * value * strength;
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int hash = x * 73856093 ^ y * 19349663;
                hash = (hash << 13) ^ hash;
                return 1f - ((hash * (hash * hash * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
            }
        }
    }
}
