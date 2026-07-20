using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOHeadStyleDefinition
    {
        [SerializeField] private string headStyleId = "head_1";
        [SerializeField] private string displayName = "Head Style 1";
        [SerializeField] private GameObject modelPrefab;

        public string HeadStyleId => headStyleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? headStyleId : displayName;
        public GameObject ModelPrefab => modelPrefab;

        public void Configure(string newHeadStyleId, string newDisplayName, GameObject newModelPrefab)
        {
            headStyleId = string.IsNullOrWhiteSpace(newHeadStyleId) ? "head_1" : newHeadStyleId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? headStyleId : newDisplayName.Trim();
            modelPrefab = newModelPrefab;
        }
    }

    [Serializable]
    public sealed class MMOFaceDefinition
    {
        [SerializeField] private string faceId = "face_1";
        [SerializeField] private string displayName = "Face 1";
        [SerializeField] private Texture2D albedoTexture;

        public string FaceId => faceId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? faceId : displayName;
        public Texture2D AlbedoTexture => albedoTexture;

        public void Configure(string newFaceId, string newDisplayName, Texture2D newAlbedoTexture)
        {
            faceId = string.IsNullOrWhiteSpace(newFaceId) ? "face_1" : newFaceId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? faceId : newDisplayName.Trim();
            albedoTexture = newAlbedoTexture;
        }
    }

    [Serializable]
    public sealed class MMOHairstyleDefinition
    {
        [SerializeField] private string hairstyleId = "hair_1";
        [SerializeField] private string displayName = "Hairstyle 1";
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Texture2D colorMask;

        public string HairstyleId => hairstyleId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? hairstyleId : displayName;
        public GameObject ModelPrefab => modelPrefab;
        public Texture2D ColorMask => colorMask;

        public void Configure(string newHairstyleId, string newDisplayName, GameObject newModelPrefab)
        {
            Configure(newHairstyleId, newDisplayName, newModelPrefab, null);
        }

        public void Configure(
            string newHairstyleId,
            string newDisplayName,
            GameObject newModelPrefab,
            Texture2D newColorMask)
        {
            hairstyleId = string.IsNullOrWhiteSpace(newHairstyleId) ? "hair_1" : newHairstyleId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? hairstyleId : newDisplayName.Trim();
            modelPrefab = newModelPrefab;
            colorMask = newColorMask;
        }
    }

    [Serializable]
    public sealed class MMOHairColorDefinition
    {
        [SerializeField] private string hairColorId = "hair_black";
        [SerializeField] private string displayName = "Black";
        [SerializeField] private Color color = Color.white;

        public string HairColorId => hairColorId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? hairColorId : displayName;
        public Color Color => color;

        public void Configure(string newHairColorId, string newDisplayName, Color newColor)
        {
            hairColorId = string.IsNullOrWhiteSpace(newHairColorId) ? "hair_black" : newHairColorId.Trim();
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? hairColorId : newDisplayName.Trim();
            color = newColor;
        }
    }

    [CreateAssetMenu(menuName = "RPG Clone/Characters/Appearance Catalog", fileName = "CharacterAppearanceCatalog")]
    public sealed class MMOCharacterAppearanceCatalog : ScriptableObject
    {
        [SerializeField] private List<MMOHeadStyleDefinition> headStyles = new();
        [SerializeField] private List<MMOFaceDefinition> faces = new();
        [SerializeField] private List<MMOHairstyleDefinition> hairstyles = new();
        [SerializeField] private List<MMOHairColorDefinition> hairColors = new();

        public IReadOnlyList<MMOHeadStyleDefinition> HeadStyles => headStyles;
        public IReadOnlyList<MMOFaceDefinition> Faces => faces;
        public IReadOnlyList<MMOHairstyleDefinition> Hairstyles => hairstyles;
        public IReadOnlyList<MMOHairColorDefinition> HairColors => hairColors;
        public string DefaultHeadStyleId => headStyles.Count > 0 && headStyles[0] != null
            ? headStyles[0].HeadStyleId
            : "head_1";
        public string DefaultFaceId => faces.Count > 0 && faces[0] != null
            ? faces[0].FaceId
            : "face_1";
        public string DefaultHairstyleId => hairstyles.Count > 0 && hairstyles[0] != null
            ? hairstyles[0].HairstyleId
            : "hair_1";
        public string DefaultHairColorId => hairColors.Count > 0 && hairColors[0] != null
            ? hairColors[0].HairColorId
            : "hair_black";

        public MMOHeadStyleDefinition FindHeadStyle(string headStyleId)
        {
            if (string.IsNullOrWhiteSpace(headStyleId))
            {
                return null;
            }

            foreach (MMOHeadStyleDefinition headStyle in headStyles)
            {
                if (headStyle != null && string.Equals(headStyle.HeadStyleId, headStyleId, StringComparison.Ordinal))
                {
                    return headStyle;
                }
            }

            return null;
        }

        public string NormalizeHeadStyleId(string headStyleId)
        {
            MMOHeadStyleDefinition headStyle = FindHeadStyle(headStyleId);
            return headStyle != null ? headStyle.HeadStyleId : DefaultHeadStyleId;
        }

        public MMOFaceDefinition FindFace(string faceId)
        {
            if (string.IsNullOrWhiteSpace(faceId))
            {
                return null;
            }

            foreach (MMOFaceDefinition face in faces)
            {
                if (face != null && string.Equals(face.FaceId, faceId, StringComparison.Ordinal))
                {
                    return face;
                }
            }

            return null;
        }

        public string NormalizeFaceId(string faceId)
        {
            MMOFaceDefinition face = FindFace(faceId);
            return face != null ? face.FaceId : DefaultFaceId;
        }

        public MMOHairstyleDefinition FindHairstyle(string hairstyleId)
        {
            if (string.IsNullOrWhiteSpace(hairstyleId))
            {
                return null;
            }

            foreach (MMOHairstyleDefinition hairstyle in hairstyles)
            {
                if (hairstyle != null && string.Equals(hairstyle.HairstyleId, hairstyleId, StringComparison.Ordinal))
                {
                    return hairstyle;
                }
            }

            return null;
        }

        public string NormalizeHairstyleId(string hairstyleId)
        {
            MMOHairstyleDefinition hairstyle = FindHairstyle(hairstyleId);
            return hairstyle != null ? hairstyle.HairstyleId : DefaultHairstyleId;
        }

        public int IndexOfHairstyle(string hairstyleId)
        {
            for (int i = 0; i < hairstyles.Count; i++)
            {
                if (hairstyles[i] != null && string.Equals(hairstyles[i].HairstyleId, hairstyleId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return hairstyles.Count > 0 ? 0 : -1;
        }

        public MMOHairColorDefinition FindHairColor(string hairColorId)
        {
            if (string.IsNullOrWhiteSpace(hairColorId))
            {
                return null;
            }

            foreach (MMOHairColorDefinition hairColor in hairColors)
            {
                if (hairColor != null
                    && string.Equals(hairColor.HairColorId, hairColorId, StringComparison.Ordinal))
                {
                    return hairColor;
                }
            }

            return null;
        }

        public string NormalizeHairColorId(string hairColorId)
        {
            MMOHairColorDefinition hairColor = FindHairColor(hairColorId);
            return hairColor != null ? hairColor.HairColorId : DefaultHairColorId;
        }

        public int IndexOfHairColor(string hairColorId)
        {
            for (int i = 0; i < hairColors.Count; i++)
            {
                if (hairColors[i] != null
                    && string.Equals(hairColors[i].HairColorId, hairColorId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return hairColors.Count > 0 ? 0 : -1;
        }

        public int IndexOfFace(string faceId)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i] != null && string.Equals(faces[i].FaceId, faceId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return faces.Count > 0 ? 0 : -1;
        }

        public void Configure(
            IEnumerable<MMOHeadStyleDefinition> newHeadStyles,
            IEnumerable<MMOFaceDefinition> newFaces,
            IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            Configure(newHeadStyles, newFaces, newHairstyles, hairColors);
        }

        public void Configure(
            IEnumerable<MMOHeadStyleDefinition> newHeadStyles,
            IEnumerable<MMOFaceDefinition> newFaces,
            IEnumerable<MMOHairstyleDefinition> newHairstyles,
            IEnumerable<MMOHairColorDefinition> newHairColors)
        {
            headStyles = newHeadStyles != null
                ? new List<MMOHeadStyleDefinition>(newHeadStyles)
                : new List<MMOHeadStyleDefinition>();
            faces = newFaces != null
                ? new List<MMOFaceDefinition>(newFaces)
                : new List<MMOFaceDefinition>();
            hairstyles = newHairstyles != null
                ? new List<MMOHairstyleDefinition>(newHairstyles)
                : new List<MMOHairstyleDefinition>();
            hairColors = newHairColors != null
                ? new List<MMOHairColorDefinition>(newHairColors)
                : new List<MMOHairColorDefinition>();
        }

        public void Configure(
            IEnumerable<MMOHeadStyleDefinition> newHeadStyles,
            IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            Configure(newHeadStyles, faces, newHairstyles);
        }

        public void Configure(IEnumerable<MMOHairstyleDefinition> newHairstyles)
        {
            Configure(headStyles, faces, newHairstyles);
        }
    }
}
