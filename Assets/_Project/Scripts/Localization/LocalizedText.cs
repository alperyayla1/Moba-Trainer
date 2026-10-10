using TMPro;
using UnityEngine;

namespace MobaTrainer.Localization
{
    /// <summary>
    /// Translates whatever English text sits in its TMP label. Code can keep writing English
    /// (e.g. "GO", "Quick"): a change is picked up in LateUpdate and translated. Text that is not in
    /// the table (numbers, key names, already-translated strings) is left alone.
    /// Added automatically by <see cref="Localizer"/>; never needs to be placed by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        private TMP_Text label;
        private string source;
        private string applied;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Loc.Changed += Reapply;
            Sync(true);
        }

        private void OnDisable()
        {
            Loc.Changed -= Reapply;
        }

        private void LateUpdate()
        {
            Sync(false);
        }

        private void Reapply() => Sync(true);

        private void Sync(bool force)
        {
            if (label == null) return;
            string now = label.text;
            if (!ReferenceEquals(now, applied) && now != applied)
            {
                source = now;         // code wrote new text
                force = true;
            }
            if (!force) return;
            string tr = Loc.T(source);
            if (tr != now) label.text = tr;
            applied = label.text;
        }
    }
}
