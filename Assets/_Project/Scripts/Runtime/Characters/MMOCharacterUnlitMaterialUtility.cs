using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Characters
{
    public static class MMOCharacterUnlitMaterialUtility
    {
        public const string UnlitShaderName = "RPG Clone/Characters/Unlit Shadow Caster";

        private const string TemplateResourcePath = "RPGClone/Materials/Character_Unlit_Template";

        private static readonly string[] FloatPropertyNames =
        {
            "_Surface",
            "_Blend",
            "_Cull",
            "_AlphaClip",
            "_Cutoff",
            "_BlendOp",
            "_SrcBlend",
            "_DstBlend",
            "_SrcBlendAlpha",
            "_DstBlendAlpha",
            "_ZWrite",
            "_AlphaToMask",
            "_QueueOffset"
        };

        private static readonly Dictionary<Material, Material> SharedVariants = new();
        private static Material unlitTemplate;
        private static bool missingTemplateReported;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            SharedVariants.Clear();
            unlitTemplate = null;
            missingTemplateReported = false;
        }

        public static bool IsCharacterUnlit(Material material)
        {
            return material != null
                && material.shader != null
                && material.shader.name == UnlitShaderName;
        }

        public static Material GetOrCreateSharedVariant(Material source)
        {
            if (source == null || IsCharacterUnlit(source))
            {
                return source;
            }

            if (SharedVariants.TryGetValue(source, out Material cached) && cached != null)
            {
                return cached;
            }

            Material variant = new(source)
            {
                name = $"{source.name} (Character Unlit)",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!ConvertToUnlit(variant))
            {
                Object.Destroy(variant);
                return source;
            }

            SharedVariants[source] = variant;
            return variant;
        }

        public static bool ConvertToUnlit(Material material)
        {
            if (material == null || IsCharacterUnlit(material))
            {
                return material != null;
            }

            Material template = ResolveTemplate();
            if (template == null || template.shader == null)
            {
                return false;
            }

            Dictionary<string, float> floatValues = CaptureFloatProperties(material);
            Texture baseTexture = GetFirstTexture(material, "_BaseMap", "_MainTex");
            Vector2 textureScale = GetFirstTextureScale(material, "_BaseMap", "_MainTex");
            Vector2 textureOffset = GetFirstTextureOffset(material, "_BaseMap", "_MainTex");
            Color baseColor = GetFirstColor(material, "_BaseColor", "_Color", Color.white);
            int renderQueue = material.renderQueue;
            string renderType = material.GetTag("RenderType", false, string.Empty);
            bool enableInstancing = material.enableInstancing;
            bool doubleSidedGi = material.doubleSidedGI;
            MaterialGlobalIlluminationFlags globalIlluminationFlags = material.globalIlluminationFlags;

            material.shader = template.shader;
            material.CopyPropertiesFromMaterial(template);

            SetTexture(material, "_BaseMap", baseTexture, textureScale, textureOffset);
            SetTexture(material, "_MainTex", baseTexture, textureScale, textureOffset);
            SetColor(material, "_BaseColor", baseColor);
            SetColor(material, "_Color", baseColor);
            foreach (KeyValuePair<string, float> pair in floatValues)
            {
                if (material.HasProperty(pair.Key))
                {
                    material.SetFloat(pair.Key, pair.Value);
                }
            }

            material.renderQueue = renderQueue;
            material.enableInstancing = enableInstancing;
            material.doubleSidedGI = doubleSidedGi;
            material.globalIlluminationFlags = globalIlluminationFlags;
            ConfigureSurfaceKeywords(material, renderType);
            return true;
        }

        private static Material ResolveTemplate()
        {
            if (unlitTemplate != null)
            {
                return unlitTemplate;
            }

            unlitTemplate = Resources.Load<Material>(TemplateResourcePath);
            if (unlitTemplate == null || unlitTemplate.shader == null)
            {
                Shader shader = Shader.Find(UnlitShaderName);
                if (shader != null)
                {
                    unlitTemplate = new Material(shader)
                    {
                        name = "Runtime Character Unlit Template",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }
            }

            if (unlitTemplate == null && !missingTemplateReported)
            {
                missingTemplateReported = true;
                Debug.LogError(
                    $"Character body materials require '{TemplateResourcePath}' with shader '{UnlitShaderName}'.");
            }

            return unlitTemplate;
        }

        private static Dictionary<string, float> CaptureFloatProperties(Material material)
        {
            Dictionary<string, float> values = new();
            foreach (string propertyName in FloatPropertyNames)
            {
                if (material.HasProperty(propertyName))
                {
                    values[propertyName] = material.GetFloat(propertyName);
                }
            }

            return values;
        }

        private static Texture GetFirstTexture(Material material, string primary, string fallback)
        {
            if (material.HasProperty(primary) && material.GetTexture(primary) != null)
            {
                return material.GetTexture(primary);
            }

            return material.HasProperty(fallback) ? material.GetTexture(fallback) : null;
        }

        private static Vector2 GetFirstTextureScale(Material material, string primary, string fallback)
        {
            if (material.HasProperty(primary) && material.GetTexture(primary) != null)
            {
                return material.GetTextureScale(primary);
            }

            return material.HasProperty(fallback) ? material.GetTextureScale(fallback) : Vector2.one;
        }

        private static Vector2 GetFirstTextureOffset(Material material, string primary, string fallback)
        {
            if (material.HasProperty(primary) && material.GetTexture(primary) != null)
            {
                return material.GetTextureOffset(primary);
            }

            return material.HasProperty(fallback) ? material.GetTextureOffset(fallback) : Vector2.zero;
        }

        private static Color GetFirstColor(Material material, string primary, string fallback, Color defaultValue)
        {
            if (material.HasProperty(primary))
            {
                return material.GetColor(primary);
            }

            return material.HasProperty(fallback) ? material.GetColor(fallback) : defaultValue;
        }

        private static void SetTexture(
            Material material,
            string propertyName,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }

            material.SetTexture(propertyName, texture);
            material.SetTextureScale(propertyName, scale);
            material.SetTextureOffset(propertyName, offset);
        }

        private static void SetColor(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, color);
            }
        }

        private static void ConfigureSurfaceKeywords(Material material, string sourceRenderType)
        {
            bool alphaClip = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f;
            bool transparent = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
            float blendMode = material.HasProperty("_Blend") ? material.GetFloat("_Blend") : 0f;

            SetKeyword(material, "_ALPHATEST_ON", alphaClip);
            SetKeyword(material, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(material, "_ALPHAPREMULTIPLY_ON", transparent && Mathf.Approximately(blendMode, 1f));
            SetKeyword(material, "_ALPHAMODULATE_ON", transparent && Mathf.Approximately(blendMode, 3f));

            string renderType = !string.IsNullOrWhiteSpace(sourceRenderType)
                ? sourceRenderType
                : transparent
                    ? "Transparent"
                    : alphaClip
                        ? "TransparentCutout"
                        : "Opaque";
            material.SetOverrideTag("RenderType", renderType);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
