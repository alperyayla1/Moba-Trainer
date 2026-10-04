using System.Collections;
using UnityEngine;

namespace MobaTrainer.UI
{
    /// <summary>
    /// A screen or overlay. Shows/hides with a short CanvasGroup fade (0.15 s, unscaled time),
    /// the one addition over the reference game's instant SetActive swaps.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Seconds; 0 = instant.")]
        public float fadeDuration = 0.15f;

        public bool IsOpen { get; private set; }

        private CanvasGroup group;
        private Coroutine routine;

        private CanvasGroup Group
        {
            get
            {
                if (group == null) group = GetComponent<CanvasGroup>();
                return group;
            }
        }

        public virtual void Show(bool instant = false)
        {
            IsOpen = true;
            bool wasActive = gameObject.activeSelf;
            gameObject.SetActive(true);
            if (!wasActive && !instant) Group.alpha = 0f;
            transform.SetAsLastSibling();
            Group.interactable = true;
            Group.blocksRaycasts = true;
            StartFade(1f, instant, false);
            OnShown();
        }

        public virtual void Hide(bool instant = false)
        {
            if (!gameObject.activeSelf)
            {
                IsOpen = false;
                return;
            }
            IsOpen = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            StartFade(0f, instant, true);
            OnHidden();
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        private void StartFade(float to, bool instant, bool deactivateAtEnd)
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            if (instant || fadeDuration <= 0f || !isActiveAndEnabled)
            {
                Group.alpha = to;
                if (deactivateAtEnd) gameObject.SetActive(false);
                return;
            }
            routine = StartCoroutine(Fade(to, deactivateAtEnd));
        }

        private IEnumerator Fade(float to, bool deactivateAtEnd)
        {
            float from = Group.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                Group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
                yield return null;
            }
            Group.alpha = to;
            routine = null;
            if (deactivateAtEnd) gameObject.SetActive(false);
        }
    }
}
