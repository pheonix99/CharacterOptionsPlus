﻿using BlueprintCore.Actions.Builder;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Buffs;
using BlueprintCore.Blueprints.ModReferences;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Assets;
using BlueprintCore.Utils.Types;
using CharacterOptionsPlus.UnitParts;
using CharacterOptionsPlus.Util;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.JsonSystem;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.ResourceLinks;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Visual.Particles;
using Kingmaker.Visual.Particles.FxSpawnSystem;
using System;
using System.Collections.Generic;
using TabletopTweaks.Core.NewActions;
using UnityEngine;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;
using static Kingmaker.Visual.Animation.Kingmaker.Actions.UnitAnimationActionCastSpell;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace CharacterOptionsPlus.Spells
{
  internal class HedgingWeapons
  {
    private const string FeatureName = "HedgingWeapons";

    internal const string DisplayName = "HedgingWeapons.Name";
    private const string Description = "HedgingWeapons.Description";

    private const string BuffName = "HedgingWeapons.Buff";
    private const string ThrowName = "HedgingWeapons.Throw";

    private const string IconPrefix = "assets/icons/";
    private const string IconName = IconPrefix + "gloriousheat.png";

    private static readonly ModLogger Logger = Logging.GetLogger(FeatureName);

    internal static void Configure()
    {
      try
      {
        if (Settings.IsEnabled(Guids.HedgingWeaponsSpell))
          ConfigureEnabled();
        else
          ConfigureDisabled();
      }
      catch (Exception e)
      {
        Logger.LogException("HedgingWeapons.Configure", e);
      }
    }

    private static void ConfigureDisabled()
    {
      Logger.Log($"Configuring {FeatureName} (disabled)");

      AbilityConfigurator.New(ThrowName, Guids.HedgingWeaponsThrow).Configure();
      BuffConfigurator.New(BuffName, Guids.HedgingWeaponsBuff).Configure();
      AbilityConfigurator.New(FeatureName, Guids.HedgingWeaponsSpell).Configure();
    }

    private static void ConfigureEnabled()
    {
      Logger.Log($"Configuring {FeatureName}");

      var buff = BuffConfigurator.New(BuffName, Guids.HedgingWeaponsBuff)
        .SetRanks(5)
        .SetStacking(StackingType.Rank)
        .AddComponent<HedgingWeaponsController>()
        .AddComponent<ControlledProjectilesHolder>()
        .AddFacts(new() { Guids.HedgingWeaponsThrow })
        .Configure();

      AbilityConfigurator.New(ThrowName, Guids.HedgingWeaponsThrow)
        .SetDisplayName(DisplayName)
        .SetDescription(Description)
        .SetIcon(IconName)
        .SetRange(AbilityRange.Close)
        .AllowTargeting(enemies: true)
        .SetAnimation(CastAnimationStyle.Thrown)
        .SetActionType(CommandType.Standard)
        .AddComponent(new AbilityDeliverControlledProjectile(buff.ToReference<BlueprintBuffReference>()))
        .Configure();

      AbilityConfigurator.NewSpell(
          FeatureName, Guids.HedgingWeaponsSpell, SpellSchool.Abjuration, canSpecialize: true, SpellDescriptor.Force)
        .SetDisplayName(DisplayName)
        .SetDescription(Description)
        .SetIcon(IconName)
        .SetRange(AbilityRange.Personal)
        .AllowTargeting(self: true)
        .SetAnimation(CastAnimationStyle.EnchantWeapon)
        .SetActionType(CommandType.Standard)
        .SetAvailableMetamagic(
          Metamagic.CompletelyNormal,
          Metamagic.Extend,
          Metamagic.Heighten,
          Metamagic.Quicken)
        .AddToSpellLists(
          level: 1, SpellList.Cleric, SpellList.Inquisitor, SpellList.LichInquisitorMinor, SpellList.Paladin)
        .AddToSpellList(level: 1, ModSpellListRefs.AntipaladinSpelllist.ToString())
        .AddContextRankConfig(ContextRankConfigs.CasterLevel())
        .AddContextRankConfig(
          ContextRankConfigs.CasterLevel(max: 5, type: AbilityRankType.ProjectilesCount)
            .WithStartPlusDivStepProgression(4, start: 2))
        .AddAbilityEffectRunAction(
          actions: ActionsBuilder.New()
            .Add<ContextActionApplyBuffRanks>(
              a =>
                {
                  a.m_Buff = buff.ToReference<BlueprintBuffReference>();
                  a.Rank = ContextValues.Rank(AbilityRankType.ProjectilesCount);
                  a.DurationValue = ContextDuration.Variable(ContextValues.Rank(), DurationRate.Minutes);
                }))
        .Configure();
    }

    [TypeId("45444d3f-4e06-4294-a6d5-7905a77f54b4")]
    private class HedgingWeaponsController : ProjectileControllerComponent
    {
      private static readonly BlueprintBuffReference HedgingWeaponsBuff =
        BlueprintTool.GetRef<BlueprintBuffReference>(Guids.HedgingWeaponsBuff);

      public override RuleAttackRoll RollAttack(AbilityExecutionContext context)
      {
        try
        {
          var caster = context.MaybeCaster;
          if (caster is null)
          {
            Logger.Warning("No caster");
            return null;
          }

          var weapon = GetWeapon(caster).CreateEntity<ItemEntityWeapon>();
          using (ContextData<AttackBonusStatReplacement>.Request().Setup(StatType.Dexterity))
          {
            return context.TriggerRule<RuleAttackRoll>(
              new(caster, context.MainTarget.Unit, weapon, attackBonusPenalty: 0));
          }
        }
        catch (Exception e)
        {
          Logger.LogException("HedgingWeaponsSpawner.RollAttack", e);
        }
        return null;
      }

      #region Asset Stuff
      private static readonly Vector3[] Offsets =
        new Vector3[]
        {
        new(0.5f, 1f, 0.5f),
        new(0.5f, 1f, -0.5f),
        new(-0.5f, 1f, 0.5f),
        new(-0.5f, 1f, -0.5f),
        new(-8.6f, 1f, 0f),
        };

      public override void OnActivate()
      {
        try
        {
          var caster = Context.MaybeCaster;
          if (caster is null)
          {
            Logger.Warning("No caster");
            return;
          }

          var enchant = Enchant.Load();
          var weapon = GetWeaponPrefab(caster).Load();
          weapon.SetActive(false);

          var projectiles = new List<ControlledProjectile>();
          var numWeapons = Buff.GetRank();
          for (int i = 0; i < numWeapons; i++)
            projectiles.Add(new(SpawnWeapon(weapon, enchant, caster, Offsets[i])));

          caster.Ensure<UnitPartControlledProjectiles>().Register(HedgingWeaponsBuff, projectiles);
        }
        catch (Exception e)
        {
          Logger.LogException("HedgingWeaponsSpawner.OnActivate", e);
        }
      }

      public override void SpawnProjectiles(UnitEntityData caster)
      {
        try
        {
          //var caster = Context.MaybeCaster;
          if (caster is null)
          {
            Logger.Warning("No caster");
            return;
          }

          var enchant = Enchant.Load();
          var weapon = GetWeaponPrefab(caster).Load();
          weapon.SetActive(false);

          var projectiles = caster.Ensure<UnitPartControlledProjectiles>().Get(HedgingWeaponsBuff);
          int i = 0;
          foreach (var projectile in projectiles)
          {
            projectile.Handle = SpawnWeapon(weapon, enchant, caster, Offsets[i]);
            i++;
          }
        }
        catch(Exception e)
        {
          Logger.LogException("HedgingWeaponsSpawner.SpawnProjectiles", e);
        }
      }

      private IFxHandle SpawnWeapon(GameObject prefab, GameObject enchant, UnitEntityData unit, Vector3 offset)
      {
        var handle = FxHelper.SpawnFxOnUnit(prefab, unit.View, partyRelated: true);
        handle.RunAfterSpawn(
          weapon =>
          {
            weapon.AnchorToUnit(unit, offset, Quaternion.AngleAxis(90, Vector3.up));
            weapon.SetActive(true);
            FxHelper.SpawnFxOnGameObject(enchant, weapon, partySource: true);
          });
        return handle;
      }

      public override void OnDeactivate()
      {
        try
        {
          var caster = Context.MaybeCaster;
          if (caster is null)
          {
            Logger.Warning("No caster");
            return;
          }
          caster.Get<UnitPartControlledProjectiles>()?.ConsumeAll(HedgingWeaponsBuff);
        }
        catch (Exception e)
        {
          Logger.LogException("HedgingWeaponsSpawner.OnDeactivate", e);
        }
      }
      #endregion

      #region God Stuff
      private static readonly PrefabLink Enchant = new() { AssetId = "fdc7f8f37d3f8da42be2a1d35a617001" };
      private static readonly PrefabLink AbadarPrefab = new() { AssetId = "f4ef679dee9518b40806cea527b62958" };
      private static readonly PrefabLink AsmodeusPrefab = new() { AssetId = "949c76c2c0141264e9885d939a3e09e4" };
      private static readonly PrefabLink DesnaPrefab = new() { AssetId = "0c8388fec791b844587490913639146b" };
      private static readonly PrefabLink ErastilPrefab = new() { AssetId = "8082c39628c82e34abf1dbc920624036" };
      private static readonly PrefabLink GorumPrefab = new() { AssetId = "5f14f60fffc49c843affe14fcb55fa04" };
      private static readonly PrefabLink IomedaePrefab = new() { AssetId = "7545e752a43edac47a2af12fd8cb79f5" };
      private static readonly PrefabLink LamashtuPrefab = new() { AssetId = "5464bdca2bddbab439369d03f6383f41" };
      private static readonly PrefabLink NorgorberPrefab = new() { AssetId = "8a0684581898ee642aa771427d77f9ab" };
      private static readonly PrefabLink SarenraePrefab = new() { AssetId = "ad5acf45026b1ac46911fbef2ab79fb5" };
      private static readonly PrefabLink ToragPrefab = new() { AssetId = "ef203bf6ef89c19409394731d72955d3" };
      private static readonly PrefabLink UrgathoaPrefab = new() { AssetId = "88fd281bc9977bf448246469c85477b6" };

      private static PrefabLink GetWeaponPrefab(UnitEntityData unit)
      {
        if (unit.HasFact(FeatureRefs.AbadarFeature.Reference.Get()))
          return AbadarPrefab;
        if (unit.HasFact(FeatureRefs.AsmodeusFeature.Reference.Get()))
          return AsmodeusPrefab;
        if (unit.HasFact(FeatureRefs.DesnaFeature.Reference.Get()))
          return DesnaPrefab;
        if (unit.HasFact(FeatureRefs.ErastilFeature.Reference.Get()))
          return ErastilPrefab;
        if (unit.HasFact(FeatureRefs.GorumFeature.Reference.Get()))
          return GorumPrefab;
        if (unit.HasFact(FeatureRefs.IomedaeFeature.Reference.Get()))
          return IomedaePrefab;
        if (unit.HasFact(FeatureRefs.LamashtuFeature.Reference.Get()))
          return LamashtuPrefab;
        if (unit.HasFact(FeatureRefs.NorgorberFeature.Reference.Get()))
          return NorgorberPrefab;
        if (unit.HasFact(FeatureRefs.SarenraeFeature.Reference.Get()))
          return SarenraePrefab;
        if (unit.HasFact(FeatureRefs.ToragFeature.Reference.Get()))
          return ToragPrefab;
        if (unit.HasFact(FeatureRefs.UrgathoaFeature.Reference.Get()))
          return UrgathoaPrefab;
        return IomedaePrefab;
      }

      private static readonly BlueprintItemWeaponReference AbadarWeapon =
        ItemWeaponRefs.StandardHeavyCrossbow.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference AsmodeusWeapon =
        ItemWeaponRefs.StandardHeavyMace.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference DesnaWeapon =
        ItemWeaponRefs.StandardStarknife.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference ErastilWeapon =
        ItemWeaponRefs.StandardLongbow.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference GorumWeapon =
        ItemWeaponRefs.StandardGreatsword.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference IomedaeWeapon =
        ItemWeaponRefs.StandardLongsword.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference LamashtuWeapon =
        ItemWeaponRefs.StandardKukri.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference NorgorberWeapon =
        ItemWeaponRefs.StandardDagger.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference SarenraeWeapon =
        ItemWeaponRefs.StandardScimitar.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference ToragWeapon =
        ItemWeaponRefs.StandardWarhammer.Cast<BlueprintItemWeaponReference>().Reference;
      private static readonly BlueprintItemWeaponReference UrgathoaWeapon =
        ItemWeaponRefs.StandardScythe.Cast<BlueprintItemWeaponReference>().Reference;

      private static BlueprintItemWeapon GetWeapon(UnitEntityData unit)
      {
        if (unit.HasFact(FeatureRefs.AbadarFeature.Reference.Get()))
          return AbadarWeapon;
        if (unit.HasFact(FeatureRefs.AsmodeusFeature.Reference.Get()))
          return AsmodeusWeapon;
        if (unit.HasFact(FeatureRefs.DesnaFeature.Reference.Get()))
          return DesnaWeapon;
        if (unit.HasFact(FeatureRefs.ErastilFeature.Reference.Get()))
          return ErastilWeapon;
        if (unit.HasFact(FeatureRefs.GorumFeature.Reference.Get()))
          return GorumWeapon;
        if (unit.HasFact(FeatureRefs.IomedaeFeature.Reference.Get()))
          return IomedaeWeapon;
        if (unit.HasFact(FeatureRefs.LamashtuFeature.Reference.Get()))
          return LamashtuWeapon;
        if (unit.HasFact(FeatureRefs.NorgorberFeature.Reference.Get()))
          return NorgorberWeapon;
        if (unit.HasFact(FeatureRefs.SarenraeFeature.Reference.Get()))
          return SarenraeWeapon;
        if (unit.HasFact(FeatureRefs.ToragFeature.Reference.Get()))
          return ToragWeapon;
        if (unit.HasFact(FeatureRefs.UrgathoaFeature.Reference.Get()))
          return UrgathoaWeapon;
        return IomedaeWeapon;
      }
      #endregion
    }
  }
}