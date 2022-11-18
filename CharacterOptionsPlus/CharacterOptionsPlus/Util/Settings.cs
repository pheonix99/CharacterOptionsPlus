﻿using BlueprintCore.Utils;
using CharacterOptionsPlus.Feats;
using Kingmaker.Blueprints;
using Kingmaker.Localization;
using ModMenu.Settings;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager.ModEntry;
using Menu = ModMenu.ModMenu;

namespace CharacterOptionsPlus.Util
{
  internal static class Settings
  {
    private static readonly string RootKey = "cop.settings";
    private static readonly string RootStringKey = "COP.Settings";

    private static readonly ModLogger Logger = Logging.GetLogger(nameof(Settings));

    internal static bool IsEnabled(string key)
    {
      return Menu.GetSettingValue<bool>(GetKey(key));
    }

    internal static bool IsTTTBaseEnabled()
    {
      return UnityModManager.modEntries.Where(
          mod => mod.Info.Id.Equals("TabletopTweaks-Base") && mod.Enabled && !mod.ErrorOnLoading)
        .Any();
    }

    internal static void Init()
    {
      Logger.Log("Initializing settings.");
      var settings =
        SettingsBuilder.New(RootKey, GetString("Title"))
          .AddImage(
            ResourcesLibrary.TryGetResource<Sprite>("assets/illustrations/wolfie.png"), height: 200, imageScale: 0.75f)
          .AddDefaultButton(OnDefaultsApplied);

      settings.AddSubHeader(GetString("Homebrew.Title"), startExpanded: true)
        .AddToggle(
          Toggle.New(
            GetKey(GloriousHeat.OriginalFeatSetting),
            defaultValue: false,
            GetString("Homebrew.GloriousHeat"))
          .WithLongDescription(GetString("Homebrew.GloriousHeat.Description")));

      settings.AddSubHeader(GetString("Fixes.Title"), startExpanded: false);
      foreach (var (key, name, description) in BugFixes.Entries)
      {
        settings.AddToggle(
          Toggle.New(GetKey(key), defaultValue: true, GetString(name, usePrefix: false))
            .WithLongDescription(GetString(description, usePrefix: false)));
      }

      settings.AddSubHeader(GetString("Archetypes.Title"));
      foreach (var (guid, name) in Guids.Archetypes)
      {
        settings.AddToggle(
          Toggle.New(GetKey(guid), defaultValue: true, GetString(name, usePrefix: false))
            .WithLongDescription(GetString("EnableFeature")));
      }

      settings.AddSubHeader(GetString("ClassFeatures.Title"));
      foreach (var (guid, name) in Guids.ClassFeatures)
      {
        settings.AddToggle(
          Toggle.New(GetKey(guid), defaultValue: true, GetString(name, usePrefix: false))
            .WithLongDescription(GetString("EnableFeature")));
      }

      settings.AddSubHeader(GetString("Feats.Title"));
      foreach (var (guid, name) in Guids.Feats)
      {
        settings.AddToggle(
          Toggle.New(GetKey(guid), defaultValue: true, GetString(name, usePrefix: false))
            .WithLongDescription(GetString("EnableFeature")));
      }

      settings.AddSubHeader(GetString("Spells.Title"));
      foreach (var (guid, name) in Guids.Spells)
      {
        settings.AddToggle(
          Toggle.New(GetKey(guid), defaultValue: true, GetString(name, usePrefix: false))
            .WithLongDescription(GetString("EnableFeature")));
      }

      Menu.AddSettings(settings);
    }

    private static void OnDefaultsApplied()
    {
      Logger.Log($"Default settings restored.");
    }

    private static LocalizedString GetString(string key, bool usePrefix = true)
    {
      var fullKey = usePrefix ? $"{RootStringKey}.{key}" : key;
      return LocalizationTool.GetString(fullKey);
    }

    private static string GetKey(string partialKey)
    {
      return $"{RootKey}.{partialKey}";
    }
  }
}
