using System;
using MobaTrainer.Persistence;
using UnityEngine;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// The playable characters on the player object. Each hero has its own model (child of Visual) and
    /// Q / W / E abilities; Blink is shared. The choice comes from Settings (Combat > Champion) and is
    /// applied as soon as it changes, unless a cast or charge is in progress.
    /// </summary>
    [RequireComponent(typeof(PlayerAbilities))]
    public class HeroRoster : MonoBehaviour
    {
        [Serializable]
        public class Hero
        {
            public string displayName = "Hero";
            public GameObject model;
            public Renderer[] flashRenderers;
            public AbilityDefinition q, w, e;
        }

        public Hero[] heroes = new Hero[0];

        /// <summary>Index of the hero in use (-1 before the first apply).</summary>
        public int Current { get; private set; } = -1;
        public Hero CurrentHero => Current >= 0 && Current < heroes.Length ? heroes[Current] : null;
        /// <summary>(newIndex) after a hero switch.</summary>
        public event Action<int> HeroChanged;

        private PlayerAbilities abilities;
        private Unit unit;

        private void Awake()
        {
            abilities = GetComponent<PlayerAbilities>();
            unit = GetComponent<Unit>();
            Apply(Wanted());
        }

        private int Wanted() => heroes.Length == 0 ? 0 : Mathf.Clamp(SettingsManager.Current.hero, 0, heroes.Length - 1);

        private void Update()
        {
            int want = Wanted();
            if (want != Current && !abilities.IsCasting && !abilities.IsCharging && !abilities.IsLocked) Apply(want);
        }

        public void Apply(int index)
        {
            if (heroes.Length == 0) return;
            index = Mathf.Clamp(index, 0, heroes.Length - 1);
            // Two entries may share a model (fallback), so hide all first, then show the chosen one.
            for (int i = 0; i < heroes.Length; i++)
                if (heroes[i].model != null) heroes[i].model.SetActive(false);
            if (heroes[index].model != null) heroes[index].model.SetActive(true);

            Hero h = heroes[index];
            if (abilities.slots.Length > 0 && h.q != null) abilities.slots[0].ability = h.q;
            if (abilities.slots.Length > 1 && h.w != null) abilities.slots[1].ability = h.w;
            if (abilities.slots.Length > 2 && h.e != null) abilities.slots[2].ability = h.e;
            abilities.CancelAiming();
            if (unit != null && h.flashRenderers != null && h.flashRenderers.Length > 0) unit.SetFlashRenderers(h.flashRenderers);

            bool changed = Current != index;
            Current = index;
            if (changed) HeroChanged?.Invoke(index);
        }
    }
}
