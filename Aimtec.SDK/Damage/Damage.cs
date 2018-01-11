// ReSharper disable SuggestBaseTypeForParameter
namespace Aimtec.SDK.Damage
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Aimtec.SDK.Damage.JSON;
    using Aimtec.SDK.Extensions;

    /// <summary>
    ///     Class Damage.
    /// </summary>
    public static class Damage
    {
        #region Public Methods and Operators

        /// <summary>
        ///     Calculates the damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="damageType">Type of the damage.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        /// <exception cref="ArgumentOutOfRangeException">damageType - null</exception>
        public static double CalculateDamage(
            this Obj_AI_Base source,
            Obj_AI_Base target,
            DamageType damageType,
            double amount)
        {
            var damage = 0d;
            switch (damageType)
            {
                case DamageType.Magical:
                    damage = source.CalculateMagicDamage(target, amount);
                    break;
                case DamageType.Physical:
                    damage = source.CalculatePhysicalDamage(target, amount);
                    break;
                case DamageType.Mixed:
                    damage = source.CalculateMixedDamage(target, damage / 2d, damage / 2d);
                    break;
                case DamageType.True:
                    damage = Math.Max(Math.Floor(amount), 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(damageType), damageType, null);
            }

            return damage;
        }

        /// <summary>
        ///     Calculates the mixed damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="physicalAmount">The physical amount.</param>
        /// <param name="magicalAmount">The magical amount.</param>
        /// <returns>System.Double.</returns>
        public static double CalculateMixedDamage(
            this Obj_AI_Base source,
            Obj_AI_Base target,
            double physicalAmount,
            double magicalAmount)
        {
            return
                source.CalculatePhysicalDamage(target, physicalAmount) +
                source.CalculateMagicDamage(target, magicalAmount);
        }

        /// <summary>
        ///     Gets the automatic attack damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <returns>System.Double.</returns>
        public static double GetAutoAttackDamage(this Obj_AI_Base source, Obj_AI_Base target)
        {
            var dmgPhysical = (double)source.TotalAttackDamage;
            var dmgMagical = 0d;
            var dmgTrue = 0d;

            var dmgReduce = 1d;

            var hero = source as Obj_AI_Hero;
            var targetHero = target as Obj_AI_Hero;

            if (hero != null)
            {
                var passiveDamage = DamagePassives.ComputePassiveDamages(hero, target);
                dmgPhysical += passiveDamage.PhysicalDamage;
                dmgMagical += passiveDamage.MagicalDamage;
                dmgTrue += passiveDamage.TrueDamage;

                dmgPhysical *= passiveDamage.PhysicalDamagePercent;
                dmgMagical *= passiveDamage.MagicalDamagePercent;
                dmgTrue *= passiveDamage.TrueDamagePercent;

                if (target is Obj_AI_Minion)
                {
                    if (hero.HasItem(ItemId.DoransShield))
                    {
                        dmgPhysical += 5;
                    }

                    if (!hero.IsMelee &&
                        target.Team == GameObjectTeam.Neutral &&
                        Regex.IsMatch(target.Name, "SRU_RiftHerald"))
                    {
                        dmgReduce *= 0.65;
                    }
                }
            }

            if (targetHero != null)
            {
                if (!(source is Obj_AI_Turret) &&
                    targetHero.HasItem(ItemId.NinjaTabi))
                {
                    dmgReduce *= 0.9;
                }

                switch (targetHero.ChampionName)
                {
                    case "Fizz":
                        dmgPhysical -= 4 + 2 * Math.Floor((targetHero.Level - 1) / 3d);
                        break;
                }
            }

            var itemDamage = DamageItems.ComputeItemDamages(source, target);
            dmgPhysical += itemDamage.PhysicalDamage;
            dmgMagical += itemDamage.MagicalDamage;

            dmgPhysical = source.CalculatePhysicalDamage(target, dmgPhysical);
            dmgMagical = source.CalculateMagicDamage(target, dmgMagical);

            switch (targetHero?.ChampionName)
            {
                case "Amumu":
                    if (targetHero.HasBuff("Tantrum"))
                    {
                        dmgPhysical -= new[] { 2, 4, 6, 8, 10 }[targetHero.SpellBook.GetSpell(SpellSlot.E).Level - 1];
                    }
                    break;
            }

            return Math.Max(Math.Floor(dmgPhysical + dmgMagical) * dmgReduce + dmgTrue, 0);
        }

        /// <summary>
        ///     Gets the spell damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="spellSlot">The spell slot.</param>
        /// <param name="stage">The stage.</param>
        /// <returns>System.Double.</returns>
        public static double GetSpellDamage(
            this Obj_AI_Hero source,
            Obj_AI_Base target,
            SpellSlot spellSlot,
            DamageStage stage = DamageStage.Default)
        {
            if (source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            if (!DamageLibrary.Damages.TryGetValue(source.ChampionName, out ChampionDamage value))
            {
                return 0;
            }

            var spellData = value.GetSlot(spellSlot)?.FirstOrDefault(e => e.Stage == stage)?.SpellData;
            if (spellData == null)
            {
                return 0;
            }

            var scaleSlot = spellData.ScaleSlot != SpellSlot.Unknown ? spellData.ScaleSlot : spellSlot;
            var spellLevel = source.SpellBook.GetSpell(scaleSlot).Level;
            if (spellLevel == 0)
            {
                return 0;
            }

            var alreadyAdd1 = false;

            var targetHero = target as Obj_AI_Hero;
            var targetMinion = target as Obj_AI_Minion;

            var dmgBase = 0d;
            var dmgBonus = 0d;
            var dmgPassive = 0d;
            var dmgReduce = 1d;

            if (spellData.DamagesPerLvl?.Count > 0)
            {
                dmgBase = spellData.DamagesPerLvl[Math.Min(source.Level - 1, spellData.DamagesPerLvl.Count - 1)];
            }
            else if (spellData.Damages?.Count > 0)
            {
                dmgBase = spellData.Damages[Math.Min(spellLevel - 1, spellData.Damages.Count - 1)];

                if (!string.IsNullOrEmpty(spellData.ScalingBuff))
                {
                    var scalingTarget = spellData.ScalingBuffTarget == DamageScalingTarget.Source ? source : target;
                    var buffCount = scalingTarget.GetRealBuffCount(spellData.ScalingBuff);

                    dmgBase = buffCount > 0 ? dmgBase * (buffCount + spellData.ScalingBuffOffset) : 0;
                }
            }

            if (dmgBase > 0)
            {
                if (targetMinion != null && spellData.BonusDamageOnMinion?.Count > 0)
                {
                    dmgBase += spellData.BonusDamageOnMinion[Math.Min(
                        spellLevel - 1,
                        spellData.BonusDamageOnMinion.Count - 1)];
                }

                if (spellData.IsApplyOnHit ||
                    spellData.IsModifiedDamage ||
                    spellData.SpellEffectType == SpellEffectType.Single)
                {
                    alreadyAdd1 = true;
                }

                dmgBase = source.CalculateDamage(target, spellData.DamageType, dmgBase);
            }

            if (spellData.BonusDamages?.Count > 0)
            {
                foreach (var bonusDmg in spellData.BonusDamages)
                {
                    var dmg = source.GetBonusSpellDamage(target, bonusDmg, spellLevel - 1);
                    if (dmg <= 0)
                    {
                        continue;
                    }

                    if (!alreadyAdd1 &&
                        (spellData.IsModifiedDamage || spellData.SpellEffectType == SpellEffectType.Single))
                    {
                        alreadyAdd1 = true;
                    }

                    dmgBonus += source.CalculateDamage(target, bonusDmg.DamageType, dmg);
                }
            }

            var totalDamage = dmgBase + dmgBonus;
            if (totalDamage > 0)
            {
                if (spellData.ScalePerCritPercent > 0)
                {
                    totalDamage *= source.Crit * 100 * spellData.ScalePerCritPercent;
                }

                if (spellData.ScalePerTargetMissHealth > 0)
                {
                    totalDamage *= (target.MaxHealth - target.Health) / target.MaxHealth * spellData.ScalePerTargetMissHealth + 1;
                }

                if (target is Obj_AI_Minion)
                {
                    if (spellData.MaxDamageOnMinion?.Count > 0)
                    {
                        totalDamage = Math.Min(
                            totalDamage,
                            spellData.MaxDamageOnMinion[Math.Min(spellLevel - 1, spellData.MaxDamageOnMinion.Count - 1)]);
                    }

                    if (target.Team == GameObjectTeam.Neutral &&
                        spellData.MaxDamageOnMonster?.Count > 0)
                    {
                        totalDamage = Math.Min(
                            totalDamage,
                            spellData.MaxDamageOnMonster[Math.Min(spellLevel - 1, spellData.MaxDamageOnMonster.Count - 1)]);
                    }
                }

                if (spellData.IsModifiedDamage)
                {
                    if (targetHero != null &&
                        targetHero.HasItem(ItemId.NinjaTabi))
                    {
                        dmgReduce *= 0.9;
                    }
                }
            }

            if (spellData.IsApplyOnHit ||
                spellData.IsModifiedDamage)
            {
                var itemDamage = DamageItems.ComputeItemDamages(source, target);
                totalDamage += source.CalculateDamage(target, DamageType.Physical, itemDamage.PhysicalDamage);
                totalDamage += source.CalculateDamage(target, DamageType.Magical, itemDamage.MagicalDamage);
            }

            if (source.ChampionName == "Sejuani" &&
                target.HasBuff("sejuanistun"))
            {
                switch (target.Type)
                {
                    case GameObjectType.obj_AI_Hero:
                        if (source.Level < 7)
                        {
                            dmgPassive += 0.1 * target.MaxHealth;
                        }
                        else if (source.Level < 14)
                        {
                            dmgPassive += 0.15 * target.MaxHealth;
                        }
                        else
                        {
                            dmgPassive += 0.2 * target.MaxHealth;
                        }
                        break;

                    case GameObjectType.obj_AI_Minion:
                        dmgPassive += 400;
                        break;
                }

                dmgPassive = source.CalculateDamage(target, DamageType.Magical, dmgPassive);
            }

            return Math.Max(Math.Floor(totalDamage * dmgReduce + dmgPassive), 0);
        }

        #endregion

        #region Methods

        /// <summary>
        ///     Calculates the magic damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        private static double CalculateMagicDamage(this Obj_AI_Base source, Obj_AI_Base target, double amount)
        {
            if (amount < 0 || source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            double value;

            if (target.SpellBlock < 0)
            {
                value = 2 - 100 / (100 - target.SpellBlock);
            }
            else if (target.SpellBlock * source.PercentMagicPenetration - source.FlatMagicPenetration < 0)
            {
                value = 1;
            }
            else
            {
                value = 100 / (100 + target.SpellBlock * source.PercentMagicPenetration - source.FlatMagicPenetration);
            }

            if (target.HasBuff("cursedtouch"))
            {
                amount *= 1.1;
            }

            return Math.Max(Math.Floor(source.GetPassivePercentMod(target, value, DamageType.Magical) * amount), 0);
        }

        /// <summary>
        ///     Calculates the physical damage.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <returns>System.Double.</returns>
        private static double CalculatePhysicalDamage(this Obj_AI_Base source, Obj_AI_Base target, double amount)
        {
            if (amount < 0 || source == null || !source.IsValid || target == null || !target.IsValid)
            {
                return 0;
            }

            double armorPenetrationPercent = source.PercentArmorPenetration;
            double bonusArmorPenetrationMod = source.PercentBonusArmorPenetration;
            var armorPenetrationFlat = source.PhysicalLethality * (0.6 + 0.4 * source.Level / 18);
            if (double.IsNaN(armorPenetrationFlat))
            {
                armorPenetrationFlat = 0;
            }

            switch (source.Type)
            {
                // Minions return wrong percent values.
                case GameObjectType.obj_AI_Minion:
                    armorPenetrationFlat = 0;
                    armorPenetrationPercent = 1;
                    bonusArmorPenetrationMod = 1;
                    break;

                // Turrets' Passive damage.
                case GameObjectType.obj_AI_Turret:
                    armorPenetrationFlat = 0;
                    bonusArmorPenetrationMod = 1;

                    //TODO:
                    break;
            }

            var armor = target.Armor;
            var bonusArmor = target.BonusArmor;

            double value;
            if (armor < 0)
            {
                value = 2 - 100 / (100 - armor);
            }
            else if (armor * armorPenetrationPercent - bonusArmor * (1 - bonusArmorPenetrationMod) - armorPenetrationFlat < 0)
            {
                value = 1;
            }
            else
            {
                value = 100 / (100 + armor * armorPenetrationPercent - bonusArmor * (1 - bonusArmorPenetrationMod) - armorPenetrationFlat);
            }

            return Math.Max(Math.Floor(source.GetPassivePercentMod(target, value, DamageType.Physical) * amount), 0);
        }

        /// <summary>
        ///     Gets the passive percent mod.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="target">The target.</param>
        /// <param name="amount">The amount.</param>
        /// <param name="damageType">The damageType.</param>
        /// <returns>System.Double.</returns>
        private static double GetPassivePercentMod(
            this Obj_AI_Base source,
            AttackableUnit target,
            double amount,
            // ReSharper disable once UnusedParameter.Local
            DamageType damageType)
        {
            var hero = source as Obj_AI_Hero;
            var turret = source as Obj_AI_Turret;
            var minion = source as Obj_AI_Minion;

            var minionTarget = target as Obj_AI_Minion;
            var targetHero = target as Obj_AI_Hero;

            if (turret != null)
            {
                if (minionTarget != null &&
                    (minionTarget.UnitSkinName.Contains("MinionSiege") || minionTarget.UnitSkinName.Contains("MinionSuper")))
                {
                    amount *= 0.7;
                }
            }

            if (minion != null)
            {
                if (minionTarget != null &&
                    Game.MapId == GameMapId.SummonersRift)
                {
                    amount *= 1f + minion.PercentDamageToBarracksMinionMod;
                }
            }

            if (minionTarget != null)
            {
                if (minionTarget.UnitSkinName.Contains("MinionMelee") &&
                    minionTarget.HasBuff("exaltedwithbaronnashorminion"))
                {
                    amount *= 0.25;
                }
            }

            if (hero != null)
            {
                if (minionTarget != null)
                {
                    if (source.HasBuff("barontarget") &&
                        minionTarget.UnitSkinName.Contains("SRU_Baron"))
                    {
                        amount *= 0.5;
                    }

                    if (source.HasBuff("dragonbuff_tooltipmanager") &&
                         minionTarget.HasBuff("s5_dragonvengeance") &&
                         minionTarget.UnitSkinName.Contains("SRU_Dragon"))
                    {
                        amount *= 1 - 7 * source.ValidActiveBuffs().Count(b => b.Name.Contains("dragonbuff") && b.Name.Contains("_manager")) / 100;
                    }
                }

                if (targetHero != null)
                {
                    if (damageType == DamageType.Physical &&
                        hero.MaxHealth < targetHero.MaxHealth)
                    {
                        var healthDiff = Math.Min(targetHero.MaxHealth - hero.MaxHealth, 2000);
                        if (hero.HasItem(ItemId.LordDominiksRegards))
                        {
                            amount *= 1 + healthDiff / 10000;
                        }
                        else if (hero.HasItem(ItemId.GiantSlayer))
                        {
                            amount *= 1 + healthDiff / 20000;
                        }
                    }

                    var damageReductions = DamageReductions.ComputeReductions(hero, targetHero, damageType);
                    amount *= damageReductions.PercentDamageReduction;
                    amount -= damageReductions.FlatDamageReduction;
                }
            }

            return amount;
        }

        #endregion
    }
}
