﻿using BepInEx.Configuration;
using LookingGlass.Base;
using MonoMod.RuntimeDetour;
using RiskOfOptions.OptionConfigs;
using RiskOfOptions.Options;
using RiskOfOptions;
using RoR2;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Text;
using static RoR2.Chat;
using System.Linq;
using LookingGlass.StatsDisplay;
using RoR2.Skills;
using System.Security.Cryptography;
using LookingGlass.ItemStatsNameSpace;
using UnityEngine;
using RoR2.Stats;

namespace LookingGlass.ItemStatsNameSpace
{
    internal class ItemStats : BaseThing
    {
        public static ConfigEntry<bool> itemStats;
        public static ConfigEntry<bool> itemStatsCalculations;
        public static ConfigEntry<bool> fullDescOnPickup;
        public static ConfigEntry<bool> itemStatsOnPing;
        public static ConfigEntry<float> itemStatsFontSize;
        public static ConfigEntry<bool> capChancePercentage;
        public static ConfigEntry<bool> abilityProcCoefficients;

        private static Hook overrideHook;
        private static Hook overrideHook2;
        private static Hook overrideHook3;
        private static Hook overrideHook4;
        public ItemStats()
        {
            Setup();
        }
        public void Setup()
        {
            InitHooks();
            ItemCatalog.availability.CallWhenAvailable(ItemDefinitions.RegisterAll);
            itemStats = BasePlugin.instance.Config.Bind<bool>("Misc", "Item Stats", true, "Shows full item descriptions on mouseover");
            itemStatsCalculations = BasePlugin.instance.Config.Bind<bool>("Misc", "Item Stats Calculations", true, "Gives calculations for vanilla items and modded items which have added specific support. (Sadly, items are not designed in a way to allow this to be automatic)");
            fullDescOnPickup = BasePlugin.instance.Config.Bind<bool>("Misc", "Full Item Description On Pickup", true, "Shows full item descriptions on pickup");
            itemStatsOnPing = BasePlugin.instance.Config.Bind<bool>("Misc", "Item Stats On Ping", true, "Shows item descriptions when you ping an item in the world");
            itemStatsFontSize = BasePlugin.instance.Config.Bind<float>("Misc", "Item Stats Font Size", 100f, "Changes the font size of item stats");
            capChancePercentage = BasePlugin.instance.Config.Bind<bool>("Misc", "Cap Chance Percentage", true, "Caps displayed chances at 100%. May interact weirdly with luck if turned off");
            abilityProcCoefficients = BasePlugin.instance.Config.Bind<bool>("Misc", "Ability Proc Coefficients", true, "Shows ability proc coefficients on supported survivors");
            SetupRiskOfOptions();
        }
        public void SetupRiskOfOptions()
        {
            //Config that people are likelier to turn off should be higher up in Risk Menu
            ModSettingsManager.AddOption(new CheckBoxOption(fullDescOnPickup, new CheckBoxConfig() { restartRequired = false }));
            ModSettingsManager.AddOption(new CheckBoxOption(itemStatsOnPing, new CheckBoxConfig() { restartRequired = false }));
            
            ModSettingsManager.AddOption(new CheckBoxOption(itemStats, new CheckBoxConfig() { restartRequired = false }));
            ModSettingsManager.AddOption(new CheckBoxOption(itemStatsCalculations, new CheckBoxConfig() { restartRequired = false, checkIfDisabled = ItemStatsDisabled }));
            ModSettingsManager.AddOption(new SliderOption(itemStatsFontSize, new SliderConfig() { restartRequired = false, min = 1, max = 300 }));
            ModSettingsManager.AddOption(new CheckBoxOption(capChancePercentage, new CheckBoxConfig() { restartRequired = false }));
            ModSettingsManager.AddOption(new CheckBoxOption(abilityProcCoefficients, new CheckBoxConfig() { restartRequired = false }));
        }
        private static bool ItemStatsDisabled()
        {
            return !itemStats.Value;
        }
        void InitHooks()
        {
            var targetMethod = typeof(GenericNotification).GetMethod(nameof(GenericNotification.SetItem), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var destMethod = typeof(ItemStats).GetMethod(nameof(PickupText), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            overrideHook = new Hook(targetMethod, destMethod, this);
            targetMethod = typeof(GenericNotification).GetMethod(nameof(GenericNotification.SetEquipment), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            destMethod = typeof(ItemStats).GetMethod(nameof(EquipmentText), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            overrideHook = new Hook(targetMethod, destMethod, this);

            targetMethod = typeof(RoR2.UI.ItemIcon).GetMethod(nameof(RoR2.UI.ItemIcon.SetItemIndex), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            destMethod = typeof(ItemStats).GetMethod(nameof(ItemIndexText), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            overrideHook2 = new Hook(targetMethod, destMethod, this);

            targetMethod = typeof(PingerController).GetMethod(nameof(PingerController.SetCurrentPing), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            destMethod = typeof(ItemStats).GetMethod(nameof(ItemPinged), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            overrideHook3 = new Hook(targetMethod, destMethod, this);

            targetMethod = typeof(RoR2.UI.SkillIcon).GetMethod(nameof(RoR2.UI.SkillIcon.Update), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            destMethod = typeof(ItemStats).GetMethod(nameof(SkillUpdate), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            overrideHook4 = new Hook(targetMethod, destMethod, this);
        }

        internal void EquipText(EquipmentIcon self)
        {
            CharacterBody body = StatsDisplayClass.cachedUserBody;
            // Multiplayer compatibility
            if (self.targetInventory)
            {
                CharacterMaster master = self.targetInventory.GetComponentInParent<CharacterMaster>();
                if (master && master.GetBody())
                {
                    // Use master body if exists
                    body = master.GetBody();
                }
            }
#pragma warning disable Publicizer001 // Accessing a member that was not originally public
            if (self.tooltipProvider && self.currentDisplayData.equipmentDef && body && body.inventory)
            {
                String cooldownReductionFormatted = ((1 - body.inventory.CalculateEquipmentCooldownScale()) * 100).ToString(StatsDisplayDefinitions.floatPrecision);
                String currentCooldownFormatted = (self.currentDisplayData.equipmentDef.cooldown * body.inventory.CalculateEquipmentCooldownScale()).ToString(StatsDisplayDefinitions.floatPrecision);
                
                self.tooltipProvider.overrideBodyText = $"{Language.GetString(self.currentDisplayData.equipmentDef.descriptionToken)}" +
                    $"\n\nCooldown Reduction: <style=\"cIsUtility>{cooldownReductionFormatted}%</style>" +
                    $"\nCooldown: <style=\"cIsUtility>{currentCooldownFormatted} seconds</style>"+
                    GetEquipmentExtras(self.currentDisplayData.equipmentDef.equipmentIndex);
            }
#pragma warning restore Publicizer001 // Accessing a member that was not originally public
        }
        void PickupText(Action<GenericNotification, ItemDef> orig, GenericNotification self, ItemDef itemDef)
        {
            orig(self, itemDef);
            if (fullDescOnPickup.Value)
                if (Language.GetString(itemDef.descriptionToken) == itemDef.descriptionToken)
                {
                    self.descriptionText.token = itemDef.pickupToken;
                }
                else
                {
                    self.descriptionText.token = itemDef.descriptionToken;
                }
        }
        void EquipmentText(Action<GenericNotification, EquipmentDef> orig, GenericNotification self, EquipmentDef equipmentDef)
        {
            orig(self, equipmentDef);
            if (fullDescOnPickup.Value)
                self.descriptionText.token = equipmentDef.descriptionToken;
        }
        void ItemIndexText(Action<ItemIcon, ItemIndex, int> orig, ItemIcon self, ItemIndex newItemIndex, int newItemCount)
        {
            orig(self, newItemIndex, newItemCount);
            if (itemStats.Value)
            {
                SetDescription(self, newItemIndex, newItemCount);
            }
        }
        void SkillUpdate(Action<SkillIcon> orig, SkillIcon self)
        {
            orig(self);
            GenericSkill targetSkill = self.targetSkill;
            if (targetSkill == null)
                return;
            string skillDescriptionToken = targetSkill.skillDescriptionToken;
            if (skillDescriptionToken == null)
                return;
            StringBuilder desc = new StringBuilder(Language.GetString(skillDescriptionToken));

            if (abilityProcCoefficients.Value)
            {
                //Why was there a "In Proc Dict" check for this?
                //Maybe could do if cooldown == 0 then dont show but it's fine
                desc.Append("\n\nSkill Cooldown: <style=\"cIsUtility\">" + CalculateSkillCooldown(self).ToString("0.00") + "</style> <style=\"cStack\">(Base: " + self.targetSkill.skillDef.baseRechargeInterval + ")</style>");

                bool blacklistedSkill = false;
                if (ProcCoefficientData.hasProcCoefficient(self.targetSkill.skillNameToken))
                {
                    blacklistedSkill = ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken) == -1;
                    //This way we could blacklist procs on most movement skills.
                    //So it doesn't say like "10% ATG" on Mando Slide
                    if (!blacklistedSkill)
                    {
                        desc.Append("\nProc Coefficient: <style=cIsDamage>" + ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken) + "</color>");
                    }
                    //If -1, show nothing
                    //If 0, show that it has 0 Proc Coeff for clarity
                    //But don't show like "0 % chance to trigger all the items"
                    blacklistedSkill = blacklistedSkill || ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken) == 0;
                }
                else if (self.targetSkill.skillNameToken == "VOIDSURVIVOR_PRIMARY_NAME" || self.targetSkill.skillNameToken == "VOIDSURVIVOR_SECONDARY_NAME")
                {
                    desc.Append("\nProc Coefficient: <style=cIsVoid>").Append((ProcCoefficientData.GetProcCoefficient("CORRUPTED_" + self.targetSkill.skillNameToken)).ToString("0.00")).Append("</style>");
                }
      
                if (!blacklistedSkill)
                {
                    CharacterBody body = self.targetSkill.characterBody;

                    int itemCount = 0;
                    ItemStatsDef itemStats;
                    foreach (var item in ItemCatalog.allItemDefs)
                    {
                        if (ItemDefinitions.allItemDefinitions.ContainsKey((int)item.itemIndex))
                        {
                            itemCount = body.inventory.GetItemCount(item.itemIndex);
                            if (itemCount > 0)
                            {
                                itemStats = ItemDefinitions.allItemDefinitions[(int)item.itemIndex];
                                if (itemStats.hasChance)
                                {
                                    desc.Append("\n").Append(Language.GetString(item.nameToken)).Append(": <style=cIsDamage>");

                                    desc.Append((itemStats.calculateValuesNew(body.master.luck, itemCount, ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken))[0] * 100).ToString("0.0")).Append("%</style>");

                                    //Math Ceil leads to 201 lost seers, 26 runic
                                    //No math leads to 9 on tri tip
                                    //What is this bro
                                    //Thank you UnityEngine.Mathf for actually working
                                    if (itemStats.chanceScaling == ItemStatsDef.ChanceScaling.Linear)
                                    {
                                        desc.Append(" <style=cStack>(");
                                        desc.Append(Mathf.CeilToInt(1 / itemStats.calculateValuesNew(0f, 1, ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken))[0]));
                                        desc.Append(" to cap)</style>");
                                    }
                                    else if (itemStats.chanceScaling == ItemStatsDef.ChanceScaling.RunicLens)
                                    {
                                        //Most ideally would calculated the chance with the min damage of the skill and add it to the chance or whatever
                                        //But that's not really possible
                                        desc.Append(" <style=cStack>(");
                                        desc.Append(Mathf.CeilToInt(0.75f / itemStats.calculateValuesNew(0f, 1, ProcCoefficientData.GetProcCoefficient(self.targetSkill.skillNameToken))[0]));
                                        desc.Append(" to cap)</style>");
                                    }
                                    if (self.targetSkill.skillNameToken == "VOIDSURVIVOR_PRIMARY_NAME" || self.targetSkill.skillNameToken == "VOIDSURVIVOR_SECONDARY_NAME")
                                    {
                                        // TODO align this text to the one above
                                        desc.Append("\n").Append("<style=cIsVoid>").Append((itemStats.calculateValuesNew(body.master.luck, itemCount, ProcCoefficientData.GetProcCoefficient("CORRUPTED_" + self.targetSkill.skillNameToken))[0] * 100).ToString("0.000")).Append("%</style>");

                                        if (itemStats.chanceScaling == ItemStatsDef.ChanceScaling.Linear)
                                        {
                                            desc.Append(" <style=cStack>(");
                                            desc.Append((int)Math.Ceiling(1 / itemStats.calculateValuesNew(0f, 1, ProcCoefficientData.GetProcCoefficient("CORRUPTED_" + self.targetSkill.skillNameToken))[0]));
                                            desc.Append(" to cap)</style>");
                                        }
                                    }
                                }
                            }
                        }

                    }
                }
            }
            self.tooltipProvider.overrideBodyText = desc.ToString();
        }

        float CalculateSkillCooldown(SkillIcon self)
        {   
            if (self.targetSkill.skillDef.baseRechargeInterval < 0.5f)
                return self.targetSkill.skillDef.baseRechargeInterval;

            //Post-SotS AttackSpeedCDR skills bypass the 0.5f cooldown cap
            //By modifying the .baseRechargeInterval
            if (self.targetSkill.baseRechargeInterval < 0.5f)
                return self.targetSkill.baseRechargeInterval;

            float calculated_skill_cooldown = self.targetSkill.baseRechargeInterval * self.targetSkill.cooldownScale - self.targetSkill.flatCooldownReduction;

            if (calculated_skill_cooldown < 0.5f)
                calculated_skill_cooldown = 0.5f;

            return calculated_skill_cooldown;

        }
        List<ItemDef> cachedItems = new List<ItemDef>();

        internal static void SetDescription(ItemIcon self, ItemIndex newItemIndex, int newItemCount)
        {
            var itemDef = ItemCatalog.GetItemDef(newItemIndex);
            if (itemDef.nameToken == "ITEM_MYSTICSITEMS_MANUSCRIPT_NAME")
                return;
            if (self.tooltipProvider != null && itemDef != null)
            {
                CharacterMaster master = null;

                var strip = self.GetComponentInParent<ScoreboardStrip>();
                if (strip && strip.master)
                {
                    master = strip.master;
                }
                self.tooltipProvider.overrideBodyText = GetDescription(itemDef, newItemIndex, newItemCount, master, false);
            }
        }
        public static string GetDescription(
            ItemDef itemDef, ItemIndex newItemIndex, int newItemCount, CharacterMaster master, bool withOneMore, bool forceNew = false)
        {
            if (Language.GetString(itemDef.descriptionToken) == itemDef.descriptionToken)
            {
                return Language.GetString(itemDef.pickupToken);
            }
            var itemDescription = $"<size={itemStatsFontSize.Value}%>{Language.GetString(itemDef.descriptionToken)}\n";
            try
            {
                if (itemStatsCalculations.Value && ItemDefinitions.allItemDefinitions.ContainsKey((int)newItemIndex))
                {
                    ItemStatsDef itemStats = ItemDefinitions.allItemDefinitions[(int)newItemIndex];
                    if (withOneMore && itemStats.descriptions.Count != 0)
                    {
                        if (newItemCount == 0 || forceNew)
                        {
                            itemDescription += $"\nWith this item, you will have:";
                        }
                        else
                        {
                            itemDescription += $"\nWith another stack, you will have:";
                        }
                        newItemCount++;
                    }
                    if (master == null)
                    {
                        master = LocalUserManager.GetFirstLocalUser().cachedMaster;
                    }
                    float luck = 0f;
                    if (master != null)
                    {
                        luck = master.luck;
                    }
                    List<float> values;
                    if (itemStats.calculateValues == null)
                    {
                        values = itemStats.calculateValuesNew(luck, newItemCount, 1f);
                    }
                    else
                    {
                        values = itemStats.calculateValues(master, newItemCount);
                    }
                    if (values is not null)
                    {
                        GetItemStatsFormatted(ref itemStats, ref values, ref itemDescription, true);
                    }
                }
            }
            catch (Exception)
            {
            }
            itemDescription += "</size>";
            return itemDescription;
        }


        //What does ref do pls help
        public static void GetItemStatsFormatted(ref ItemStatsDef statsDef, ref List<float> values, ref string input, bool white)
        {
            for (int i = 0; i < statsDef.descriptions.Count; i++)
            {
                if (white)
                {
                    input += $"\n<color=\"white\">{statsDef.descriptions[i]}</color>";
                }
                else
                {
                    input += $"\n{statsDef.descriptions[i]}";
                }
                switch (statsDef.valueTypes[i])
                {
                    case ItemStatsDef.ValueType.Healing:
                    case ItemStatsDef.ValueType.Armor:
                        input += "<style=\"cIsHealing";
                        break;
                    case ItemStatsDef.ValueType.Damage:
                        input += "<style=\"cIsDamage";
                        break;
                    case ItemStatsDef.ValueType.Utility:
                        input += "<style=\"cIsUtility";
                        break;
                    case ItemStatsDef.ValueType.Health:
                        input += "<style=\"cIsHealth";
                        break;
                    case ItemStatsDef.ValueType.Void:
                        input += "<style=\"cIsVoid";
                        break;
                    case ItemStatsDef.ValueType.Gold:
                    case ItemStatsDef.ValueType.HumanObjective:
                        input += "<style=\"cHumanObjective";
                        break;
                    case ItemStatsDef.ValueType.LunarObjective:
                        input += "<style=\"cLunarObjective";
                        break;
                    case ItemStatsDef.ValueType.Stack:
                        input += "<style=\"cStack";
                        break;
                    case ItemStatsDef.ValueType.WorldEvent:
                        input += "<style=\"cWorldEvent";
                        break;
                    case ItemStatsDef.ValueType.Artifact:
                        input += "<style=\"cArtifact";
                        break;
                    case ItemStatsDef.ValueType.UserSetting:
                        input += "<style=\"cUserSetting";
                        break;
                    case ItemStatsDef.ValueType.Death:
                        input += "<style=\"cDeath";
                        break;
                    case ItemStatsDef.ValueType.Sub:
                        input += "<style=\"cSub";
                        break;
                    case ItemStatsDef.ValueType.Mono:
                        input += "<style=\"cMono";
                        break;
                    case ItemStatsDef.ValueType.Shrine:
                        input += "<style=\"cShrine";
                        break;
                    case ItemStatsDef.ValueType.Event:
                        input += "<style=\"cEvent";
                        break;
                        //case ItemStatsDef.ValueType.Other:
                        //    itemDescription += "<color=\"white";
                        //    break;
                }
                switch (statsDef.measurementUnits[i])
                {
                    case ItemStatsDef.MeasurementUnits.Meters:
                        input += $"\">{values[i]:0.###}m</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.Percentage:
                        input += $"\">{values[i] * 100:0.###}%</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.FlatHealth:
                        input += $"\">{values[i]:0.###} HP</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.PercentHealth:
                        input += $"\">{values[i] * 100:0.###}% HP</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.FlatHealing:
                        input += $"\">{values[i]:0.###} HP/s</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.PercentHealing:
                        input += $"\">{values[i] * 100:0.###}% HP/s</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.Number:
                        input += $"\">{values[i]:0.###}</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.Money:
                        input += $"\">{values[i]:0.###}$</style>";
                        break;
                    case ItemStatsDef.MeasurementUnits.Seconds:
                        input += $"\">{values[i]:0.###} seconds</style>";
                        break;
                    default:
                        break;
                }
            }
        }

        public static string GetEquipmentExtras(EquipmentIndex equipmentIndex)
        {
            //Put the formatting values in it's own method, hopefully it doesn't create issues.
            string equipmentProcInfo = string.Empty;
            try
            {
                if (itemStatsCalculations.Value && ItemDefinitions.allEquipmentDefinitions.ContainsKey((int)equipmentIndex))
                {
                    ItemStatsDef statsDef = ItemDefinitions.allEquipmentDefinitions[(int)equipmentIndex];
                    List<float> values;
                    if (statsDef.calculateValues == null)
                    {
                        values = statsDef.calculateValuesNew(0, 1, 1f);
                    }
                    else
                    {
                        values = statsDef.calculateValues(null, 1);
                    }
                    if (values is not null)
                    {
                        GetItemStatsFormatted(ref statsDef, ref values, ref equipmentProcInfo, false);
                    }
                }
            }
            catch (Exception)
            {
            }
            return equipmentProcInfo;
        }


        //Heavily uses https://github.com/Moffein/ItemStats/blob/master/ItemStats/ItemStatsPlugin.cs
        void ItemPinged(Action<PingerController, PingerController.PingInfo> orig, PingerController self, PingerController.PingInfo newPingInfo)
        {
            orig(self, newPingInfo);
            if (!itemStatsOnPing.Value || !(self.hasAuthority && newPingInfo.targetGameObject))
                return;

            PickupDef pickupDef = null;
            GenericPickupController genericPickupController = newPingInfo.targetGameObject.GetComponent<GenericPickupController>();
            if (genericPickupController)
            {
                pickupDef = PickupCatalog.GetPickupDef(genericPickupController.pickupIndex);
            }
            else
            {
                ShopTerminalBehavior shopTerminalBehavior = newPingInfo.targetGameObject.GetComponent<ShopTerminalBehavior>();
                if (shopTerminalBehavior && !shopTerminalBehavior.pickupIndexIsHidden && !shopTerminalBehavior.Networkhidden && shopTerminalBehavior.pickupDisplay)
                {
                    pickupDef = PickupCatalog.GetPickupDef(shopTerminalBehavior.pickupIndex);
                }
            }
            if (pickupDef != null)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(pickupDef.itemIndex);
                CharacterMaster characterMaster = self.gameObject.GetComponent<CharacterMaster>();
                if (itemDef)
                {
                    if (characterMaster)
                    {
                        PushItemNotificationDuration(characterMaster, itemDef.itemIndex);
                    }
                }
                else
                {
                    EquipmentDef equipmentDef = EquipmentCatalog.GetEquipmentDef(pickupDef.equipmentIndex);
                    if (equipmentDef)
                    {
                        if (characterMaster)
                        {
                            PushEquipmentNotificationDuration(characterMaster, equipmentDef.equipmentIndex);
                        }
                    }
                }
            }
        }
        internal void PushItemNotificationDuration(CharacterMaster characterMaster, ItemIndex itemIndex)
        {
            if (!characterMaster.hasAuthority)
            {
                Log.Error("Can't PushItemNotification for " + Util.GetBestMasterName(characterMaster) + " because they aren't local.");
                return;
            }
            CharacterMasterNotificationQueue notificationQueueForMaster = CharacterMasterNotificationQueue.GetNotificationQueueForMaster(characterMaster);
            if (notificationQueueForMaster && itemIndex != ItemIndex.None)
            {
                ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                if (itemDef == null || itemDef.hidden)
                {
                    return;
                }
                notificationQueueForMaster.PushNotification(new CharacterMasterNotificationQueue.NotificationInfo(ItemCatalog.GetItemDef(itemIndex), null), 6f);
                PutLastNotificationFirst(notificationQueueForMaster);
            }
        }

        internal void PushEquipmentNotificationDuration(CharacterMaster characterMaster, EquipmentIndex equipmentIndex)
        {
            if (!characterMaster.hasAuthority)
            {
                Log.Error("Can't PushEquipmentNotification for " + Util.GetBestMasterName(characterMaster) + " because they aren't local.");
                return;
            }
            CharacterMasterNotificationQueue notificationQueueForMaster = CharacterMasterNotificationQueue.GetNotificationQueueForMaster(characterMaster);
            if (notificationQueueForMaster && equipmentIndex != EquipmentIndex.None)
            {
                notificationQueueForMaster.PushNotification(new CharacterMasterNotificationQueue.NotificationInfo(EquipmentCatalog.GetEquipmentDef(equipmentIndex), null), 6f);
                PutLastNotificationFirst(notificationQueueForMaster);
            }
        }

        internal void PutLastNotificationFirst(CharacterMasterNotificationQueue notificationQueueForMaster)
        {
            if (notificationQueueForMaster.notifications.Count > 1)
            {
                notificationQueueForMaster.notifications[0].duration = .01f;
                if (notificationQueueForMaster.notifications.Count > 2)
                {
                    var newNotification = notificationQueueForMaster.notifications.Last();
                    notificationQueueForMaster.notifications.Remove(newNotification);
                    notificationQueueForMaster.notifications.Insert(1, newNotification);
                }
            }
        }
    }
}
