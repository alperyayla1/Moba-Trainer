using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Gameplay;
using UnityEngine;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>An item the player holds (with the Random card's stat bonus, if any).</summary>
    public class OwnedItem
    {
        public SurvivorItem item;
        public float statMult = 1f;
        public bool fromRandom;
    }

    /// <summary>
    /// Extreme Survivor stats + items. Owns the player's AD / AP / HP / move speed / attack speed / CDR
    /// while the mode runs and puts the original values back when it ends (<see cref="Restore"/>), so
    /// nothing leaks into other modes. Skill damage = skill base (unchanged, dealt by the normal skill
    /// system) + AD and AP ratios (dealt here as extra damage on the hit). Item passives run on four
    /// hooks: skill cast, skill hit (and attack hit), taking damage, tick.
    /// </summary>
    public class SurvivorItemSystem
    {
        private readonly SurvivorConfig cfg;
        private readonly PlayerController player;
        private readonly Unit self;

        public readonly List<OwnedItem> owned = new List<OwnedItem>();
        /// <summary>What sits in each slot (null = empty). The player can rearrange these by dragging.</summary>
        public readonly OwnedItem[] slots;

        // ---- actives
        private readonly Dictionary<SurvivorItem, float> activeCooldown = new Dictionary<SurvivorItem, float>();
        private float stasisTimer;
        private float stasisPulse;
        public bool InStasis => stasisTimer > 0f;

        // ---- stats
        public float AD { get; private set; }
        public float BonusAD => AD - cfg.baseAD;
        public float AP { get; private set; }
        public float MaxHealth { get; private set; }
        public float MoveSpeed { get; private set; }
        public float AttackSpeed { get; private set; }
        public float CDR { get; private set; }

        // Original player values (restored when the mode ends).
        private readonly float origMove, origAttackSpeed, origAttackDamage, origCooldownScale;

        // ---- passive state
        private readonly Dictionary<ItemGroup, float> groupCooldown = new Dictionary<ItemGroup, float>();
        private readonly Dictionary<SurvivorItem, float> itemCooldown = new Dictionary<SurvivorItem, float>();
        private bool spellbladeArmed;
        private float spellbladeWindow;
        private bool spellShieldUp;
        private int momentumStacks;
        private int castCount;
        private float pendingRefund;
        private float momentumTimer;
        private float speedBoost;          // fraction, temporary
        private float speedBoostTimer;
        private bool wasLow;

        private class Slow { public float baseSpeed; public float until; public float pct; }
        private readonly Dictionary<EnemyBrain, Slow> slows = new Dictionary<EnemyBrain, Slow>();
        private class Burn { public float dps; public float until; }
        private readonly Dictionary<Unit, Burn> burns = new Dictionary<Unit, Burn>();
        private readonly List<Unit> scratch = new List<Unit>();
        private readonly List<EnemyBrain> brainScratch = new List<EnemyBrain>();
        private struct Zone { public Vector3 pos; public float radius, until, slow; }
        private readonly List<Zone> zones = new List<Zone>();
        private float time;

        public SurvivorItemSystem(SurvivorConfig cfg, PlayerController player)
        {
            this.cfg = cfg;
            this.player = player;
            self = player.Unit;
            origMove = player.Motor.moveSpeed;
            origAttackSpeed = player.Attack.attacksPerSecond;
            origAttackDamage = player.Attack.damage;
            origCooldownScale = player.Abilities.cooldownScale;
            slots = new OwnedItem[Mathf.Max(1, cfg.itemSlots)];
            Recalculate(true);
        }

        /// <summary>Puts the player's original stats back (called when the run ends).</summary>
        public void Restore()
        {
            player.Motor.moveSpeed = origMove;
            player.Attack.attacksPerSecond = origAttackSpeed;
            player.Attack.damage = origAttackDamage;
            player.Abilities.cooldownScale = origCooldownScale;
            self.invulnerable = false;
            stasisTimer = 0f;
            self.SetTint(Color.white, 0f);
            if (veilBubble != null) Object.Destroy(veilBubble);
            if (goldBubble != null) Object.Destroy(goldBubble);
            foreach (var kv in slows)
            {
                if (kv.Key == null) continue;
                kv.Key.moveSpeed = kv.Value.baseSpeed;
                TintSlow(kv.Key, false);
            }
            slows.Clear();
            burns.Clear();
            zones.Clear();
        }

        // ------------------------------------------------------------------ items

        public bool Owns(SurvivorItem it)
        {
            foreach (OwnedItem o in owned) if (o.item == it) return true;
            return false;
        }

        public bool GroupTaken(ItemGroup g)
        {
            if (g == ItemGroup.None) return false;
            foreach (OwnedItem o in owned) if (o.item.group == g) return true;
            return false;
        }

        public bool CanTake(SurvivorItem it) => it != null && !Owns(it) && !GroupTaken(it.group) && owned.Count < cfg.itemSlots;

        public void Add(SurvivorItem it, float statMult, bool fromRandom)
        {
            if (!CanTake(it)) return;
            var o = new OwnedItem { item = it, statMult = statMult, fromRandom = fromRandom };
            owned.Add(o);
            for (int i = 0; i < slots.Length; i++) if (slots[i] == null) { slots[i] = o; break; }
            if (it.passive == ItemPassive.SpellShield) spellShieldUp = true;
            Recalculate(false);
        }

        /// <summary>Drag & drop: swap two slots (either may be empty).</summary>
        public void Swap(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= slots.Length || b >= slots.Length) return;
            (slots[a], slots[b]) = (slots[b], slots[a]);
        }

        public float ActiveCooldownLeft(SurvivorItem it) => activeCooldown.TryGetValue(it, out float t) ? Mathf.Max(0f, t - time) : 0f;

        /// <summary>The cooldown shown on the slot: the active's if it has one, otherwise the passive's.</summary>
        public float SlotCooldownLeft(SurvivorItem it, out float total)
        {
            if (it.HasActive) { total = Mathf.Max(0.01f, it.activeCooldown); return ActiveCooldownLeft(it); }
            total = Mathf.Max(0.01f, it.cooldown);
            return CooldownLeft(it);
        }

        /// <summary>Slot key pressed: use that item's active (if any, and ready). Returns true if something happened.</summary>
        public bool Activate(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot] == null || InStasis) return false;
            SurvivorItem it = slots[slot].item;
            if (!it.HasActive || ActiveCooldownLeft(it) > 0f) return false;
            switch (it.active)
            {
                case ItemActive.Stasis:
                    stasisTimer = Mathf.Max(0.1f, it.activeDuration);
                    MobaTrainer.Audio.GameSfx.Play("stasis", 0.8f, 0f);
                    stasisPulse = 0f;
                    player.Motor.Stop();
                    player.Attack.ClearOrders();
                    player.Abilities.CancelAiming();
                    FxSystem.Instance?.Burst(self.Position, 1.6f, it.color.WithAlpha(0.8f), 0.4f);
                    break;
            }
            activeCooldown[it] = time + it.activeCooldown;
            return true;
        }

        /// <summary>Cooldown left for an owned item's passive (group items share one).</summary>
        public float CooldownLeft(SurvivorItem it)
        {
            if (it.group != ItemGroup.None) return groupCooldown.TryGetValue(it.group, out float g) ? Mathf.Max(0f, g - time) : 0f;
            return itemCooldown.TryGetValue(it, out float c) ? Mathf.Max(0f, c - time) : 0f;
        }

        public bool IsReady(SurvivorItem it) => CooldownLeft(it) <= 0f;

        private void StartCooldown(SurvivorItem it)
        {
            float until = time + it.cooldown;
            if (it.group != ItemGroup.None) groupCooldown[it.group] = until;
            else itemCooldown[it] = until;
        }

        /// <summary>Extra info for the HUD slot (stacks, "READY", ...).</summary>
        public string SlotNote(SurvivorItem it)
        {
            switch (it.passive)
            {
                case ItemPassive.Momentum: return momentumStacks > 0 ? momentumStacks.ToString() : "";
                case ItemPassive.SpellShield: return spellShieldUp ? "ON" : "";
                default: return "";
            }
        }

        // ------------------------------------------------------------------ stats

        public void Recalculate(bool first)
        {
            float ad = cfg.baseAD, ap = cfg.baseAP, hp = cfg.playerHealth, ms = 0f, aspd = 0f, cdr = 0f, apMult = 1f;
            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                float m = o.statMult;
                ad += it.ad * m; ap += it.ap * m; hp += it.health * m;
                ms += it.moveSpeed * m; aspd += it.attackSpeed * m; cdr += it.cdr * m;
                if (it.passive == ItemPassive.ApAmplifier) apMult *= Mathf.Max(1f, it.p1);
            }
            AD = ad;
            AP = ap * apMult;
            CDR = Mathf.Clamp(cdr, 0f, cfg.cdrCap);
            AttackSpeed = origAttackSpeed * (1f + aspd);
            MoveSpeed = origMove + ms * 0.01f;

            // Max HP: keep the same missing-health amount when it grows.
            float oldMax = self.maxHealth;
            MaxHealth = hp;
            if (!first && self.IsAlive && hp > oldMax)
            {
                self.maxHealth = hp;
                self.Heal(hp - oldMax);
            }
            else if (!first)
            {
                self.maxHealth = hp;
            }

            player.Attack.damage = AD * cfg.autoAttackFactor;   // skills carry the damage in this mode
            player.Attack.attacksPerSecond = AttackSpeed;
            player.Abilities.cooldownScale = origCooldownScale * (1f - CDR);
            ApplyMoveSpeed();
        }

        private void ApplyMoveSpeed() => player.Motor.moveSpeed = MoveSpeed * (1f + speedBoost);

        private void SpeedBoost(float pct, float seconds)
        {
            speedBoost = Mathf.Max(speedBoost, pct);
            speedBoostTimer = Mathf.Max(speedBoostTimer, seconds);
            ApplyMoveSpeed();
        }

        // ------------------------------------------------------------------ damage ratios

        /// <summary>The AD/AP part of a skill's damage (the skill's own base is dealt by the skill system).</summary>
        public float RatioDamage(int slot)
        {
            switch (slot)
            {
                case 0: return AD * cfg.qAD + AP * cfg.qAP;
                case 1: return BonusAD * cfg.wBonusAD + AP * cfg.wAP;
                case 2: return BonusAD * cfg.eBonusAD + AP * cfg.eAP;
                default: return 0f;
            }
        }

        private float SkillMultiplier(Unit target)
        {
            float mult = 1f;
            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                if (it.passive == ItemPassive.Momentum) mult += momentumStacks * it.p2;
                if (it.passive == ItemPassive.TankBreaker && target != null && target.maxHealth >= it.p2) mult += it.p1;
            }
            return mult;
        }

        // ------------------------------------------------------------------ hooks

        /// <summary>onSkillCast</summary>
        public void OnSkillCast(int slot)
        {
            if (slot < 0 || slot > 2) return;    // Blink doesn't count
            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                switch (it.passive)
                {
                    case ItemPassive.SpellbladeStrike:
                    case ItemPassive.SpellbladeFrost:
                    case ItemPassive.SpellbladeHex:
                        if (IsReady(it)) { spellbladeArmed = true; spellbladeWindow = 10f; }
                        break;
                    case ItemPassive.Momentum:
                        momentumStacks = Mathf.Min(Mathf.RoundToInt(it.p1), momentumStacks + 1);
                        momentumTimer = it.p3;
                        break;
                    case ItemPassive.CastRefund:
                        castCount++;
                        if (castCount >= Mathf.Max(1, Mathf.RoundToInt(it.p1)))
                        {
                            castCount = 0;
                            pendingRefund += it.p2;      // applied next tick, after this cast's own cooldown started
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// onSkillHit: a skill connected. <paramref name="skillBase"/> is the damage the skill system
        /// already dealt; this adds the AD/AP ratio part and runs the item passives.
        /// </summary>
        public void OnSkillHit(int slot, Unit target, float skillBase)
        {
            if (target == null) return;
            float mult = SkillMultiplier(target);
            float extra = RatioDamage(slot) * mult + skillBase * (mult - 1f);
            float dealt = skillBase;
            if (extra > 0f && target.IsAlive) dealt += target.TakeDamage(extra, self);

            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                switch (it.passive)
                {
                    case ItemPassive.ChillingSkills: ApplySlow(target, it.p1, it.p2); break;
                    case ItemPassive.Burn: ApplyBurn(target, target.maxHealth * it.p1, it.p2); break;
                    case ItemPassive.SkillLifesteal: self.Heal(dealt * it.p1); break;
                    case ItemPassive.Bloodthirst: self.Heal(dealt * it.p2); break;
                    case ItemPassive.CooldownOnHit: player.Abilities.ReduceCooldowns(it.p1); break;
                }
            }
            ChargedStrike(target);
        }

        /// <summary>The W mark was detonated (by Q or an attack): add W's AD/AP ratio part.</summary>
        public void OnMarkBurst(Unit target, float burst)
        {
            if (target == null || !target.IsAlive) return;
            float mult = SkillMultiplier(target);
            float extra = RatioDamage(1) * mult + burst * (mult - 1f);
            if (extra > 0f) target.TakeDamage(extra, self);
        }

        /// <summary>An enemy died (kill hooks).</summary>
        public void OnKill(Unit unit)
        {
            foreach (OwnedItem o in owned)
                if (o.item.passive == ItemPassive.KillRefund) player.Abilities.ReduceCooldowns(o.item.p1 * o.statMult);
        }

        /// <summary>An auto-attack connected (Spellblade procs and charged strikes ride on it).</summary>
        public void OnAttackHit(Unit target, float dealt = 0f)
        {
            if (target == null) return;
            foreach (OwnedItem o in owned)
                if (o.item.passive == ItemPassive.Bloodthirst && dealt > 0f) self.Heal(dealt * o.item.p1 * o.statMult);
            if (spellbladeArmed)
            {
                foreach (OwnedItem o in owned)
                {
                    SurvivorItem it = o.item;
                    if (it.group != ItemGroup.Spellblade || !IsReady(it)) continue;
                    float bonus = 0f;
                    switch (it.passive)
                    {
                        case ItemPassive.SpellbladeStrike:
                            bonus = cfg.baseAD * it.p1;
                            break;
                        case ItemPassive.SpellbladeFrost:
                            bonus = cfg.baseAD * it.p1;
                            zones.Add(new Zone { pos = target.Position, radius = it.p2, slow = it.p3, until = time + it.p4 });
                            FxSystem.Instance?.Burst(target.Position, it.p2, new Color(0.55f, 0.85f, 1f, 0.6f), 0.4f);
                            break;
                        case ItemPassive.SpellbladeHex:
                            bonus = cfg.baseAD * it.p1 + AP * it.p2;
                            SpeedBoost(it.p3, it.p4);
                            break;
                    }
                    if (bonus > 0f && target.IsAlive)
                    {
                        target.TakeDamage(bonus * o.statMult, self);
                        FxSystem.Instance?.Flash(target.Position, target.radius + 0.6f, it.color, 0.3f);
                    }
                    StartCooldown(it);
                    spellbladeArmed = false;
                }
            }
            ChargedStrike(target);
        }

        private void ChargedStrike(Unit target)
        {
            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                if (it.passive != ItemPassive.ChargedStrike || !IsReady(it)) continue;
                if (target.IsAlive) target.TakeDamage(it.p1 + AD * it.p2, self);
                FxSystem.Instance?.Flash(target.Position, target.radius + 0.5f, it.color, 0.25f);
                SpeedBoost(it.p3, it.p4);
                StartCooldown(it);
            }
        }

        /// <summary>
        /// Called before an enemy hit lands: true = the spell shield is up and the hit should be
        /// blocked (the scenario makes the player invulnerable while the shield is up).
        /// </summary>
        public bool SpellShieldUp => spellShieldUp;

        /// <summary>onTakeDamage (amount 0 = the hit was blocked by the spell shield).</summary>
        public void OnTakeDamage(float amount, Unit source)
        {
            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                switch (it.passive)
                {
                    case ItemPassive.SpellShield:
                        if (spellShieldUp && amount <= 0f && !InStasis)
                        {
                            spellShieldUp = false;
                            MobaTrainer.Audio.GameSfx.Play("shield_block", 0.8f);
                            StartCooldown(it);
                            FxSystem.Instance?.Burst(self.Position, 1.6f, it.color.WithAlpha(0.7f), 0.35f);
                        }
                        else if (!spellShieldUp && amount > 0f && it.p1 >= 1f)
                        {
                            StartCooldown(it);    // taking damage restarts the recharge
                        }
                        break;
                    case ItemPassive.Thorns:
                        if (amount > 0f && IsReady(it))
                        {
                            // Reflect onto whoever hit you (no area shockwave).
                            if (source == null || !source.IsAlive || !UnitRegistry.IsHostile(self.team, source.team)) break;
                            float dmg = (it.p2 + self.maxHealth * it.p3) * o.statMult;
                            source.TakeDamage(dmg, self);
                            FxSystem.Instance?.Burst(source.Position, 0.8f, it.color.WithAlpha(0.6f), 0.25f);
                            StartCooldown(it);
                        }
                        break;
                }
            }
        }

        /// <summary>onTick</summary>
        public void OnTick(float dt)
        {
            time += dt;
            if (pendingRefund > 0f)
            {
                player.Abilities.ReduceCooldowns(pendingRefund);
                pendingRefund = 0f;
            }
            UpdateVisuals();

            if (spellbladeArmed)
            {
                spellbladeWindow -= dt;
                if (spellbladeWindow <= 0f) spellbladeArmed = false;
            }
            if (momentumStacks > 0)
            {
                momentumTimer -= dt;
                if (momentumTimer <= 0f) { momentumStacks--; momentumTimer = 0.5f; }
            }
            if (speedBoostTimer > 0f)
            {
                speedBoostTimer -= dt;
                if (speedBoostTimer <= 0f) { speedBoost = 0f; ApplyMoveSpeed(); }
            }

            foreach (OwnedItem o in owned)
            {
                SurvivorItem it = o.item;
                if (it.passive == ItemPassive.SpellShield && !spellShieldUp && IsReady(it)) spellShieldUp = true;
                if (it.passive == ItemPassive.LowHealthShield)
                {
                    bool low = self.IsAlive && self.Health01 < it.p1;
                    if (low && !wasLow && IsReady(it))
                    {
                        self.AddShield((it.p2 + self.maxHealth * it.p3) * o.statMult, it.p4);
                        FxSystem.Instance?.Burst(self.Position, 1.4f, it.color.WithAlpha(0.6f), 0.35f);
                        StartCooldown(it);
                    }
                    wasLow = low;
                }
            }
            if (stasisTimer > 0f)
            {
                stasisTimer -= dt;
                stasisPulse -= dt;
                if (stasisPulse <= 0f)
                {
                    stasisPulse = 0.35f;
                    FxSystem.Instance?.UnitPulse(self.transform, self.radius + 0.5f, new Color(1f, 0.82f, 0.35f, 0.9f));
                }
                if (stasisTimer <= 0f) FxSystem.Instance?.Burst(self.Position, 1.4f, new Color(1f, 0.82f, 0.35f, 0.6f), 0.3f);
            }
            // Untouchable while in stasis; a ready spell shield also turns the next hit into 0 damage.
            self.invulnerable = spellShieldUp || stasisTimer > 0f;

            TickZones();
            TickSlows();
            TickBurns(dt);
        }

        // ------------------------------------------------------------------ visuals

        private GameObject veilBubble;     // pink ring while the spell shield is ready
        private GameObject goldBubble;     // gold ring during stasis
        private static readonly Color VeilPink = new Color(1f, 0.45f, 0.85f, 1f);
        private static readonly Color StasisGold = new Color(1f, 0.8f, 0.25f, 1f);
        private static readonly Color SlowBlue = new Color(0.45f, 0.75f, 1f, 1f);

        private GameObject MakeBubble(string name, Color color, float scale)
        {
            GameObject src = player.Abilities.shieldBubble;
            if (src == null) return null;
            GameObject b = Object.Instantiate(src, src.transform.parent);
            b.name = name;
            b.transform.localScale = src.transform.localScale * scale;
            foreach (ProceduralFlatMesh m in b.GetComponentsInChildren<ProceduralFlatMesh>(true))
            {
                m.color = new Color(color.r, color.g, color.b, m.color.a);
                m.Apply();
            }
            b.SetActive(false);
            return b;
        }

        private void UpdateVisuals()
        {
            if (veilBubble == null) veilBubble = MakeBubble("VeilBubble", VeilPink, 1.15f);
            if (goldBubble == null) goldBubble = MakeBubble("StasisBubble", StasisGold, 1.25f);
            bool veil = spellShieldUp && self.IsAlive;
            bool gold = stasisTimer > 0f;
            if (veilBubble != null && veilBubble.activeSelf != veil) veilBubble.SetActive(veil);
            if (goldBubble != null && goldBubble.activeSelf != gold) goldBubble.SetActive(gold);
            self.SetTint(StasisGold, gold ? 0.85f : 0f);
        }

        // ------------------------------------------------------------------ effects

        private void DamageAround(Vector3 center, float radius, float damage)
        {
            scratch.Clear();
            var all = UnitRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i];
                if (!u.IsAlive || !UnitRegistry.IsHostile(self.team, u.team)) continue;
                Vector3 d = u.Position - center;
                d.y = 0f;
                float r = radius + u.radius;
                if (d.sqrMagnitude <= r * r) scratch.Add(u);
            }
            foreach (Unit u in scratch) u.TakeDamage(damage, self);
        }

        public void ApplySlow(Unit target, float pct, float seconds)
        {
            if (target == null || !target.IsAlive) return;
            EnemyBrain b = target.GetComponent<EnemyBrain>();
            if (b == null) return;
            if (!slows.TryGetValue(b, out Slow s))
            {
                s = new Slow { baseSpeed = b.moveSpeed };
                slows[b] = s;
            }
            s.pct = Mathf.Max(s.until > time ? s.pct : 0f, Mathf.Clamp01(pct));
            s.until = Mathf.Max(s.until, time + seconds);
            b.moveSpeed = s.baseSpeed * (1f - s.pct);
            TintSlow(b, true);
        }

        /// <summary>Forget an enemy's slow without touching its speed (it is being re-configured).</summary>
        public void ForgetSlow(EnemyBrain b)
        {
            if (b == null) return;
            if (slows.Remove(b)) TintSlow(b, false);
        }

        /// <summary>Change an enemy's base speed while keeping any active slow.</summary>
        public void SetBaseSpeed(EnemyBrain b, float speed)
        {
            if (b == null) return;
            if (slows.TryGetValue(b, out Slow s)) { s.baseSpeed = speed; b.moveSpeed = speed * (1f - s.pct); }
            else b.moveSpeed = speed;
        }

        /// <summary>Slowed enemies look icy blue.</summary>
        private static void TintSlow(EnemyBrain b, bool on)
        {
            Unit u = b != null ? b.GetComponent<Unit>() : null;
            if (u != null) u.SetTint(SlowBlue, on ? 0.45f : 0f);
        }

        private void TickSlows()
        {
            brainScratch.Clear();
            foreach (var kv in slows)
            {
                if (kv.Key == null) { brainScratch.Add(kv.Key); continue; }
                if (time < kv.Value.until) continue;
                kv.Key.moveSpeed = kv.Value.baseSpeed;
                TintSlow(kv.Key, false);
                brainScratch.Add(kv.Key);
            }
            foreach (EnemyBrain b in brainScratch) slows.Remove(b);
        }

        private void ApplyBurn(Unit target, float dps, float seconds)
        {
            if (!burns.TryGetValue(target, out Burn b)) { b = new Burn(); burns[target] = b; }
            b.dps = Mathf.Max(b.until > time ? b.dps : 0f, dps);
            b.until = time + seconds;
        }

        private void TickBurns(float dt)
        {
            scratch.Clear();
            foreach (var kv in burns)
            {
                Unit u = kv.Key;
                if (u == null || !u.IsAlive || time >= kv.Value.until) { scratch.Add(u); continue; }
                u.TakeDamage(kv.Value.dps * dt, self);
            }
            foreach (Unit u in scratch) burns.Remove(u);
        }

        private void TickZones()
        {
            for (int z = zones.Count - 1; z >= 0; z--)
            {
                Zone zone = zones[z];
                if (time >= zone.until) { zones.RemoveAt(z); continue; }
                var all = UnitRegistry.All;
                for (int i = 0; i < all.Count; i++)
                {
                    Unit u = all[i];
                    if (!u.IsAlive || !UnitRegistry.IsHostile(self.team, u.team)) continue;
                    Vector3 d = u.Position - zone.pos;
                    d.y = 0f;
                    if (d.sqrMagnitude <= zone.radius * zone.radius) ApplySlow(u, zone.slow, 0.25f);
                }
            }
        }
    }
}
