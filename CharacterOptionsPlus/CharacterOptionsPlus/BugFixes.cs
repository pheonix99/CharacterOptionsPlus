﻿using BlueprintCore.Blueprints.Configurators.Classes.Selection;
using BlueprintCore.Blueprints.Configurators.Classes.Spells;
using BlueprintCore.Blueprints.Configurators.Items.Ecnchantments;
using BlueprintCore.Blueprints.CustomConfigurators;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Selection;
using BlueprintCore.Blueprints.CustomConfigurators.Classes.Spells;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Conditions.Builder;
using BlueprintCore.Conditions.Builder.ContextEx;
using BlueprintCore.Utils.Types;
using CharacterOptionsPlus.Util;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using System.Collections.Generic;
using static Kingmaker.UnitLogic.Mechanics.Actions.ContextActionSavingThrow;
using static UnityModManagerNet.UnityModManager.ModEntry;

namespace CharacterOptionsPlus
{
  internal class BugFixes
  {
    private static readonly Logging.Logger Logger = Logging.GetLogger(nameof(BugFixes));

    internal static void Configure()
    {
      if (Settings.IsEnabled(ConeOfColdWitchSpell))
        FixConeOfColdWitchSpell();
      if (Settings.IsEnabled(HeavenlyFireResourceAmount))
        FixHeavenlyFireResourceAmount();
      if (Settings.IsEnabled(PackRagerTeamworkSelection))
        FixPackRagerTeamworkSelection();
      if (Settings.IsEnabled(SerpentineBiteDC))
        FixSerpentineBiteDC();
      if (Settings.IsEnabled(SerpentineFriendBonus))
        FixSerpentineFriendBonus();
      if (Settings.IsEnabled(SlayerEvasionTalent))
        AddSlayerEvasionTalent();
    }

    internal const string PackRagerTeamworkSelection = "pack-rager-teamwork-selection-fix";
    internal static void FixPackRagerTeamworkSelection()
    {
      Logger.Log("Patching PackRager Teamwork Selection");
      FeatureSelectionConfigurator.For(FeatureSelectionRefs.PackRagerTeamworkFeatSelection)
        .SetGroup(FeatureGroup.TeamworkFeat)
        .Configure();
    }

    internal const string HeavenlyFireResourceAmount = "heavenly-fire-resource-amount-fix";
    internal static void FixHeavenlyFireResourceAmount()
    {
      Logger.Log("Patching Heavenly Fire resource amount");
      AbilityResourceConfigurator.For(AbilityResourceRefs.BloodlineCelestialHeavenlyFireResource)
        .SetMaxAmount(ResourceAmountBuilder.New(3).IncreaseByStat(StatType.Charisma))
        .Configure();
    }

    internal const string SerpentineBiteDC = "serpentine-bite-dc";
    internal static void FixSerpentineBiteDC()
    {
      Logger.Log("Patching Serpentine Bite DC");
      PatchSerpentinePoison(WeaponEnchantmentRefs.BloodlineSerpentineSerpentsFangPoison1Enchantment.ToString());
      PatchSerpentinePoison(WeaponEnchantmentRefs.BloodlineSerpentineSerpentsFangPoison2Enchantment.ToString());
      PatchSerpentinePoison(WeaponEnchantmentRefs.BloodlineSerpentineSerpentsFangPoison3Enchantment.ToString());
    }

    private static void PatchSerpentinePoison(string poison)
    {
      WeaponEnchantmentConfigurator.For(poison)
        .EditComponent<AddInitiatorAttackWithWeaponTrigger>(
          c =>
          {
            if (c.Action.Actions[0] is ContextActionSavingThrow savingThrow)
            {
              savingThrow.HasCustomDC = true;
              savingThrow.CustomDC = ContextValues.Rank();

              var serpentineBloodline = new ConditionalDCIncrease();
              serpentineBloodline.Condition =
                ConditionsBuilder.New()
                  .CasterHasFact(FeatureRefs.SerpentineBloodlineRequisiteFeature.ToString())
                  .Build();
              serpentineBloodline.Value = ContextValues.Rank(AbilityRankType.DamageDice);

              var serpentineHeritage = new ConditionalDCIncrease();
              serpentineHeritage.Condition =
                ConditionsBuilder.New()
                  .CasterHasFact(Guids.SerpentineHeritage)
                  .Build();
              serpentineHeritage.Value = ContextValues.Rank(AbilityRankType.DamageBonus);

              savingThrow.m_ConditionalDCIncrease = new ConditionalDCIncrease[2];
              savingThrow.m_ConditionalDCIncrease[0] = serpentineBloodline;
              savingThrow.m_ConditionalDCIncrease[1] = serpentineHeritage;
            }
            else
            {
              Logger.Warning($"Failed to patch Serpentine Bite DC for {poison}");
            }
          })
        // Base DC is 10 + Con mod
        .AddContextRankConfig(ContextRankConfigs.StatBonus(StatType.Constitution).WithBonusValueProgression(10))
        // Sorc / Magus add 1/2 level
        .AddContextRankConfig(
          ContextRankConfigs.SumClassLevelWithArchetype(
              classes:
                new string[]
                {
                  CharacterClassRefs.SorcererClass.ToString(),
                  CharacterClassRefs.MagusClass.ToString()
                },
              archetypes:
                new string[] { ArchetypeRefs.EldritchScionArchetype.ToString() },
              type: AbilityRankType.DamageDice,
              min: 1)
            .WithDiv2Progression())
        // Eldritch Heritage add 1/2 Effective Level
        .AddContextRankConfig(
          ContextRankConfigs.CustomProperty(
              Guids.EldritchHeritageEffectiveLevel, type: AbilityRankType.DamageBonus, min: 1)
            .WithDiv2Progression())
        .Configure();
    }

    internal const string SerpentineFriendBonus = "serpentine-friend-bonus";
    internal static void FixSerpentineFriendBonus()
    {
      Logger.Log("Patching Serpentine Friend DC");
      FeatureConfigurator.For(FeatureRefs.BloodlineSerpentineSerpentfriendFeature)
        .EditComponents<AddStatBonus>(c => c.Value = 3, c => true)
        .Configure();
    }

    internal const string ConeOfColdWitchSpell = "cone-of-cold-witch-spell-fix";
    internal static void FixConeOfColdWitchSpell()
    {
      Logger.Log("Patching Cone of Cold Witch Spell");
      SpellListConfigurator.For(SpellListRefs.WitchSpellList)
        .ModifySpellsByLevel(
          spells =>
          {
            if (spells.SpellLevel == 6)
              spells.m_Spells.Add(AbilityRefs.ConeOfCold.Cast<BlueprintAbilityReference>().Reference);
          })
        .Configure();
    }

    internal const string SlayerEvasionTalent = "slayer-evasion-talent";
    internal static void AddSlayerEvasionTalent()
    {
      Logger.Log("Adding Evasion to Slayer Talents");
      FeatureSelectionConfigurator.For(FeatureSelectionRefs.SlayerTalentSelection10)
        .AddToAllFeatures(FeatureRefs.Evasion.ToString())
        .Configure();
    }

    internal static readonly List<(string key, string name, string description)> Entries =
      new()
      {
        (ConeOfColdWitchSpell, "ConeOfCold.WitchSpell.Name", "ConeOfCold.WitchSpell.Description"),
        (HeavenlyFireResourceAmount, "HeavenlyFireResourceAmount.Name", "HeavenlyFireResourceAmount.Description"),
        (PackRagerTeamworkSelection, "PackRagerTeamworkSelection.Name", "PackRagerTeamworkSelection.Description"),
        (SerpentineBiteDC, "SerpentineBiteDC.Name", "SerpentineBiteDC.Description"),
        (SerpentineFriendBonus, "SerpentineFriendBonus.Name", "SerpentineFriendBonus.Description"),
        (SlayerEvasionTalent, "SlayerEvasionTalent.Name", "SlayerEvasionTalent.Description"),
      };
  }
}
