﻿using BlueprintCore.Actions.Builder;
using BlueprintCore.Actions.Builder.ContextEx;
using BlueprintCore.Blueprints.CustomConfigurators.Classes;
using BlueprintCore.Blueprints.CustomConfigurators.UnitLogic.Abilities;
using BlueprintCore.Blueprints.References;
using BlueprintCore.Conditions.Builder;
using BlueprintCore.Conditions.Builder.ContextEx;
using BlueprintCore.Utils;
using BlueprintCore.Utils.Types;
using CharacterOptionsPlus.Actions;
using CharacterOptionsPlus.Util;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using System;
using System.Collections.Generic;

namespace CharacterOptionsPlus.Feats
{
  internal class PurifyingChannel
  {
    internal const string FeatName = "PurifyingChannel";
    internal const string FeatDisplayName = "PurifyingChannel.Name";
    private const string FeatDescription = "PurifyingChannel.Description";

    private const string IconPrefix = "assets/icons/";
    private const string IconName = IconPrefix + "gloriousheat.png";

    private static readonly Logging.Logger Logger = Logging.GetLogger(FeatName);

    internal static void Configure()
    {
      try
      {
        if (Settings.IsEnabled(Guids.PurifyingChannelFeat))
          ConfigureEnabled();
        else
          ConfigureDisabled();
      }
      catch (Exception e)
      {
        Logger.LogException("PurifyingChannel.Configure", e);
      }
    }

    private static void ConfigureDisabled()
    {
      Logger.Log($"Configuring {FeatName} (disabled)");

      FeatureConfigurator.New(FeatName, Guids.PurifyingChannelFeat).Configure();
    }

    // TODO: Evangelist from Homebrew Archetypes
    private static readonly List<BlueprintReference<BlueprintAbility>> PositiveHeal =
      new()
      {
          AbilityRefs.ChannelEnergy.Reference,
          AbilityRefs.ChannelEnergyHospitalerHeal.Reference,
          AbilityRefs.ChannelEnergyEmpyrealHeal.Reference,
          AbilityRefs.ChannelEnergyPaladinHeal.Reference,
          AbilityRefs.ShamanLifeSpiritChannelEnergy.Reference,
          AbilityRefs.OracleRevelationChannelAbility.Reference,
          AbilityRefs.WarpriestChannelEnergy.Reference,
          AbilityRefs.HexChannelerChannelEnergy.Reference,
      };
    private static void ConfigureEnabled()
    {
      Logger.Log($"Configuring {FeatName}");

      var selectiveChannel = FeatureRefs.SelectiveChannel.Reference.Get();
      FeatureConfigurator.New(FeatName, Guids.PurifyingChannelFeat, FeatureGroup.Feat)
        .SetDisplayName(FeatDisplayName)
        .SetDescription(FeatDescription)
        .SetIcon(IconName)
        .AddFeatureTagsComponent(FeatureTag.ClassSpecific | FeatureTag.Damage)
        .AddPrerequisiteFeature(selectiveChannel)
        .AddPrerequisiteStatValue(StatType.Charisma, 15)
        .AddRecommendationHasFeature(selectiveChannel)
        .Configure(delayed: true);

      foreach (var bp in PositiveHeal)
        AddPurifyToChannel(bp.Get());
    }

    private static void AddPurifyToChannel(BlueprintAbility channel)
    {
      Logger.Verbose($"Adding purifying channel to {channel.Name}");
      var purify =
        ActionsBuilder.New()
          .Conditional(
            ConditionsBuilder.New()
              .CasterHasFact(Guids.PurifyingChannelFeat)
              .IsEnemy(),
            ifTrue: ActionsBuilder.New()
              .Add<CountRunAction>(
                a =>
                {
                  a.Count = 1;
                  a.Counter = AbilitySharedValue.Duration;
                  a.Actions = ActionsBuilder.New()
                    .SavingThrow(
                      SavingThrowType.Will,
                      onResult: ActionsBuilder.New()
                        .ConditionalSaved(
                          failed: ActionsBuilder.New()
                            .ApplyBuff(BuffRefs.DazzledBuff.ToString(), ContextDuration.Fixed(1)))
                        .DealDamagePreRolled(
                          DamageTypes.Energy(DamageEnergyType.Fire), AbilitySharedValue.Heal, halfIfSaved: true))
                    .Build();
                }))
          .Build();

      AbilityConfigurator.For(channel)
        .EditComponent<AbilityEffectRunAction>(
          a => a.Actions.Actions = CommonTool.Append(a.Actions.Actions, purify.Actions))
        .AddContextCalculateSharedValue(
          valueType: AbilitySharedValue.Duration, value: ContextDice.Value(DiceType.Zero))
        .Configure();
    }
  }
}
