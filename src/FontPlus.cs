using SML;
using System;
using System.IO;
using UnityEngine;
using HarmonyLib;
using Home.Shared;
using System.Collections.Generic;
using System.Linq;
using Home.Common;
using TMPro;
using Services;
using Home.HomeScene;
using Home.Common.Settings;
using Server.Shared.Extensions;
using UnityEngine.UI;

namespace FontPlus;

[Mod.SalemMod]
public class FontPlus
{
    public static string Path = System.IO.Path.GetDirectoryName(Application.dataPath) + "/SalemModLoader/ModFolders/FontPlus/";

    public static void Start()
    {
        GenerateDirectories();
        Console.WriteLine("FontPlus works!");
    }

    private static void GenerateDirectories()
    {

        if (Directory.Exists(Path)) return;

        Directory.CreateDirectory(Path);
    }
}

[HarmonyPatch(typeof(SettingsController))]
public class InitFonts
{

    [HarmonyPatch(nameof(SettingsController.OnDropdownValueChanged))]
    [HarmonyPrefix]
    public static bool fix (SettingsController __instance, bool isOn) {
        Toggle toggle = __instance.FontToggleGroup.GetFirstActiveToggle();
        if ((UnityEngine.Object)(object)toggle != null)
        {
            FontListItem item = __instance.FontListItems.Find((FontListItem f) => (UnityEngine.Object)(object)f.toggle == (UnityEngine.Object)(object)toggle);
            if (item != null) {
                int index = item.GetIndex();
                
                __instance.SetFontToggle(index);
            } else {
                Debug.LogError($"[Font+] Something went wrong! Unable to find toggle {toggle.name} in FontListItems!");
            }
        }
        return false;
    }
    public static bool reload = false;
    
    [HarmonyPatch(nameof(SettingsController.InitializeFontDropdown))]
    [HarmonyPrefix]
    public static void Prefix(SettingsController __instance)
    {
        FontHelper.UpdateFonts();
        Init(__instance);
    }

    private static ToggleGroup _defaultFontToggleGroup;

    private static void Init(SettingsController __instance)
    {
        if (!reload) return;
        Debug.Log($"[Font+] Settings is null: { __instance == null }");
        Debug.Log($"[Font+] FontListItems is null: { __instance.FontListItems == null }");
        __instance.FontToggleGroup.m_Toggles.Clear();
        __instance.FontListItems.Clear();
        int num = 0;
        Debug.Log("[Font+] Iterating...");
        foreach (BMG_FontData font in ApplicationController.ApplicationContext.FontControllerSource.fonts)
        {
            FontListItem fontListItem = UnityEngine.Object.Instantiate(__instance.fontListItemTemplate, __instance.fontListItemTemplate.transform.parent);
            if (fontListItem != null)
            {
                fontListItem.SetData(font, num);
                fontListItem.gameObject.name = "Item: " + font.fontName;
                fontListItem.gameObject.SetActive(value: true);
                __instance.FontToggleGroup.RegisterToggle(fontListItem.toggle);
                __instance.FontListItems.Add(fontListItem);
                num++;
            } else {
                UnityEngine.Object.Destroy(fontListItem);
            }
        }
        
        __instance._fontSelectedValue = Service.Home.UserService.Settings.ChatFont;
        __instance.FontToggleGroup.SetAllTogglesOff();

        Debug.Log($"Chat Font selection during initialization is: {__instance._fontSelectedValue}");
        __instance.FontDropdownListGO.SetActive(value: false);
        __instance._fontDropdownOpen = false;
        string fontName = ApplicationController.ApplicationContext.FontControllerSource.fonts[__instance._fontSelectedValue].fontName;
        __instance.FontDropdownLabel.SetText(fontName);
        __instance.FontListItems[__instance._fontSelectedValue].toggle.isOn = true;
        __instance.FontToggleGroup.EnsureValidState();
        if (!Leo.IsLoginScene())
        {
            __instance.FontCanvas.sortingLayerName = "UI-Popup";
        }

        __instance.UpdatePreviewText();
        reload = false;
    }
        

    [HarmonyPatch(nameof(SettingsController.CloseDialogIfOpen))]
    [HarmonyPrefix]
    public static void SaveValue() {
        ModSettings.SetInt("PrevLoadedFontIndex", Service.Home.UserService.Settings.ChatFont, "voidbehemoth.fontplus");
    }
}

public class FontHelper
{
    private static string _defaultMentionMaterial;
    private static string _defaultStandardMaterial;
    private static string _defaultSDF;
    private static Material _defaultStandardFontMaterial;

    private static List<BMG_FontData> _defaultFonts = new List<BMG_FontData>();

    public static void UpdateFonts()
    {
        if (_defaultFonts.IsEmpty()) _defaultFonts.AddRange(ApplicationController.ApplicationContext.FontControllerSource.fonts);

        if (_defaultMentionMaterial == null) _defaultMentionMaterial = ApplicationController.ApplicationContext.FontControllerSource.fonts.First().mentionMaterial;
        if (_defaultStandardMaterial == null) _defaultStandardMaterial = ApplicationController.ApplicationContext.FontControllerSource.fonts.First().standardMaterial;
        if (_defaultSDF == null) _defaultSDF = ApplicationController.ApplicationContext.FontControllerSource.fonts.First().sdfName;
        if (_defaultStandardFontMaterial == null) _defaultStandardFontMaterial = ApplicationController.ApplicationContext.FontControllerSource.fonts.First().standardFontMaterial;

        List<string> filePaths = Directory.EnumerateFiles(FontPlus.Path, "*.ttf", SearchOption.AllDirectories).ToList();

        if (ModSettings.GetBool("Import System Fonts", "voidbehemoth.fontplus")) filePaths.AddRange(Font.GetPathsToOSFonts().ToList());

        ApplicationController.ApplicationContext.FontControllerSource.fonts.Clear();
        ApplicationController.ApplicationContext.FontControllerSource.fonts.AddRange(_defaultFonts);

        foreach (string path in filePaths)
        {
            Font font = new Font(path);
            BMG_FontData fontData = FontDataFromFont(font);
            ApplicationController.ApplicationContext.FontControllerSource.fonts.Add(fontData);
        }

        int config = ModSettings.GetInt("PrevLoadedFontIndex", "voidbehemoth.fontplus");

        int val = Math.Max(Math.Min(config, filePaths.Count + _defaultFonts.Count - 1), 0);

        Service.Home.UserService.Settings.ChatFont = val;
        ModSettings.SetInt("PrevLoadedFontIndex", val, "voidbehemoth.fontplus");
    }

    private static BMG_FontData FontDataFromFont(Font font)
    {
        BMG_FontData data = new BMG_FontData
        {
            tmp_FontAsset = TMP_FontAsset.CreateFontAsset(font),
            comment = "",
            fontName = font.name,
            sdfName = _defaultSDF
        };
        data.standardFontMaterial = data.tmp_FontAsset.material;
        data.mentionMaterial = _defaultMentionMaterial;
        data.standardMaterial = data.tmp_FontAsset.material.ToString();



        return data;
    }
}

[DynamicSettings]
public class FontPlusSettings
{
    private static bool isDeveloper {
        get {
            string accountName = Service.Home.UserService.UserInfo.AccountName;
            return accountName == "VoidBehemoth";
        }
    }

    public ModSettings.CheckboxSetting ImportSysFonts
    {
        get
        {
            return new ModSettings.CheckboxSetting
            {
                Name = "Import System Fonts",
                Description = "Imports all fonts installed on the system.",
                DefaultValue = true,
                Available = true,
                AvailableInGame = false,
                OnChanged = (bool enabled) =>
                {
                    FontHelper.UpdateFonts();
                    InitFonts.reload = true;
                }
            };
        }
    }

    public ModSettings.IntegerInputSetting PrevLoadedFontIndex
    {
        get
        {
            return new ModSettings.IntegerInputSetting
            {
                Name = "PrevLoadedFontIndex",
                Description = "Internal.",
                DefaultValue = 0,
                MinValue = 0,
                Available = isDeveloper,
                AvailableInGame = isDeveloper
            };
        }
    }
}