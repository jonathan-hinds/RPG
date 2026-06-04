using RPGClone.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace RPGClone.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MMOExperienceBarPresenter : MonoBehaviour
    {
        [SerializeField] private bool autoBuild = true;
        [SerializeField] private MMOExperienceComponent experience;

        private Image fill;
        private Text label;

        private void Awake()
        {
            ResolveReferences();
            if (autoBuild)
            {
                BuildIfNeeded();
            }

            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Configure(MMOExperienceComponent newExperience)
        {
            Unsubscribe();
            experience = newExperience;
            BuildIfNeeded();
            Refresh();
            Subscribe();
        }

        private void ResolveReferences()
        {
            if (experience != null)
            {
                return;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                experience = player.GetComponent<MMOExperienceComponent>();
            }
        }

        private void Subscribe()
        {
            if (experience != null)
            {
                experience.Changed -= OnExperienceChanged;
                experience.Changed += OnExperienceChanged;
            }
        }

        private void Unsubscribe()
        {
            if (experience != null)
            {
                experience.Changed -= OnExperienceChanged;
            }
        }

        private void OnExperienceChanged(MMOExperienceComponent changedExperience)
        {
            Refresh();
        }

        private void BuildIfNeeded()
        {
            if (fill != null)
            {
                return;
            }

            MMOUiFactory.DestroyChildren(transform);

            RectTransform root = (RectTransform)transform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.anchoredPosition = new Vector2(0f, 118f);
            root.sizeDelta = new Vector2(620f, 16f);

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            background.color = new Color(0.025f, 0.02f, 0.016f, 0.94f);

            fill = MMOUiFactory.CreateImage("Fill", transform, new Color(0.52f, 0.25f, 0.86f, 1f), false);
            MMOUiFactory.Stretch(fill.rectTransform);

            label = MMOUiFactory.CreateText("Label", transform, 10, FontStyle.Bold, TextAnchor.MiddleCenter);
            label.color = Color.white;
            MMOUiFactory.Stretch(label.rectTransform);
        }

        private void Refresh()
        {
            BuildIfNeeded();
            if (experience == null)
            {
                SetFill(0f);
                label.text = string.Empty;
                return;
            }

            int requiredExperience = experience.ExperienceToNextLevel;
            if (requiredExperience <= 0)
            {
                SetFill(1f);
                label.text = "Max Level";
                return;
            }

            SetFill(experience.CurrentExperience / (float)requiredExperience);
            label.text = $"{experience.CurrentExperience}/{requiredExperience} XP";
        }

        private void SetFill(float normalized)
        {
            float clampedValue = Mathf.Clamp01(normalized);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(clampedValue, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
