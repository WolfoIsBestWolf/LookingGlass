using BepInEx.Configuration;
using LookingGlass.Base;
using MonoMod.RuntimeDetour;
using RiskOfOptions;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RoR2;
using RoR2.ContentManagement;
using RoR2.UI;
using System;
using System.Reflection;
using UnityEngine;

namespace LookingGlass.HiddenItems
{
    internal class UnHiddenItems : BaseThing
    {
        public UnHiddenItems()
        {
            Setup();
            SetupRiskOfOptions();
        }
        private static Hook overrideHook;
        public static ConfigEntry<bool> noHiddenItems;
        public void Setup()
        {
            noHiddenItems = BasePlugin.instance.Config.Bind<bool>("Misc", "Unhide Hidden Items", false, "Unhides normally hidden items such as the Drizzle/MonsoonHelpers");
            noHiddenItems.SettingChanged += NoHiddenItems_SettingChanged;
        }

        public void SetupRiskOfOptions()
        {
            ModSettingsManager.AddOption(new CheckBoxOption(noHiddenItems, new CheckBoxConfig() {name = "Unhide Internal Items", restartRequired = false}));
            ItemCatalog.availability.CallWhenAvailable(CallLate);   
        }
        private void CallLate()
        {
            //Doesnt work if called in Awake(), so ig call late.
            NoHiddenItems_SettingChanged(null, null);
        }

        private void NoHiddenItems_SettingChanged(object _, EventArgs __)
        {
            if (noHiddenItems.Value)
            {
                if (overrideHook == null)
                {
                    var targetMethod = typeof(ItemInventoryDisplay).GetMethod(nameof(ItemInventoryDisplay.ItemIsVisible), BindingFlags.NonPublic | BindingFlags.Static);
                    overrideHook = new Hook(targetMethod, ItemIsVisible);
                }
            }
            if (!noHiddenItems.Value)
            {
                if (overrideHook != null)
                {
                    overrideHook.Undo();
                    overrideHook = null;
                }
            }
        }

        static bool ItemIsVisible(Func<ItemIndex, bool> orig, ItemIndex item)
        {
            return true;
        }
    }
}
