﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CraftSharp.Protocol.Handlers.StructuredComponents.Components;
using Unity.Mathematics;
using UnityEngine;

using CraftSharp.Protocol.Handlers.StructuredComponents.Core;

namespace CraftSharp
{
    public class ItemPalette : IdentifierPalette<Item>
    {
        private static readonly char SP = Path.DirectorySeparatorChar;
        public static readonly ItemPalette INSTANCE = new();
        protected override string Name => "Item Palette";
        protected override Item UnknownObject => Item.UNKNOWN;

        private readonly Dictionary<ResourceLocation, Func<ItemStack, float3[]>> itemColorRules = new();
        private readonly Dictionary<ResourceLocation, ResourceLocation> blockIdToBlockItemId = new();

        public StructuredComponentRegistry ComponentRegistry;

        public bool IsTintable(ResourceLocation identifier)
        {
            return itemColorRules.ContainsKey(identifier);
        }

        public Func<ItemStack, float3[]> GetTintRule(ResourceLocation identifier)
        {
            return itemColorRules.GetValueOrDefault(identifier);
        }
        
        public ResourceLocation GetItemIdForBlock(ResourceLocation blockId)
        {
            return blockIdToBlockItemId.GetValueOrDefault(blockId, ResourceLocation.INVALID);
        }
        
        public Item GetItemForBlock(ResourceLocation blockId)
        {
            return GetById(blockIdToBlockItemId.GetValueOrDefault(blockId, ResourceLocation.INVALID));
        }

        protected override void ClearEntries()
        {
            base.ClearEntries();
            itemColorRules.Clear();
            blockIdToBlockItemId.Clear();
        }

        private static float3 GetEffectsColor(ResourceLocation[] effectIds)
        {
            int effectCount = effectIds.Length;
            if (effectCount == 0)
            {
                return ColorConvert.GetFloat3(0x385DC6); // Water bottle, blue
            }
            int rSum = 0, gSum = 0, bSum = 0;
            var palette = MobEffectPalette.INSTANCE;

            foreach (var effectId in effectIds)
            {
                var effect = palette.GetById(effectId);
                rSum += (effect.Color & 0xFF0000) >> 16;
                gSum += (effect.Color & 0xFF00) >> 8;
                bSum +=  effect.Color & 0xFF;

                effectCount++;
            }
            
            int finalColor =
                (Mathf.RoundToInt(rSum / (float) effectCount) << 16) |
                (Mathf.RoundToInt(gSum / (float) effectCount) << 8) |
                 Mathf.RoundToInt(bSum / (float) effectCount);
            
            return ColorConvert.GetFloat3(finalColor);
        }
        
        private static float3[] GetPotionColor(ItemStack itemStack)
        {
            if (itemStack.TryGetComponent<PotionContentsComponent>(
                StructuredComponentIds.POTION_CONTENTS_ID, out var potionContentsComp))
            {
                // Potion color override
                if (potionContentsComp.HasCustomColor)
                {
                    return new[] { ColorConvert.GetFloat3(potionContentsComp.CustomColor) };
                }
                
                // Default effects for potion (Custom effects doesn't affect potion color)
                if (potionContentsComp.HasPotionId)
                {
                    var potionId = potionContentsComp.PotionId;
                    var potion = PotionPalette.INSTANCE.GetById(potionId);

                    return new[] { GetEffectsColor(potion.Effects.Select(x => x.EffectId).ToArray()) };
                }
            }

            return new[] { ColorConvert.GetFloat3(0xFF00FF) }; // Uncraftable potion, magenta
        }

        /// <summary>
        /// Load item data from external files.
        /// </summary>
        /// <param name="componentRegistry">Component registry</param>
        /// <param name="dataVersion">Item data version</param>
        /// <param name="flag">Data load flag</param>
        public void PrepareData(StructuredComponentRegistry componentRegistry, string dataVersion, DataLoadFlag flag)
        {
            // Clear loaded stuff...
            ClearEntries();

            string itemsPath = PathHelper.GetExtraDataFile($"items{SP}items-{dataVersion}.json");
            string colorsPath = PathHelper.GetExtraDataFile("item_colors.json");

            if (!File.Exists(itemsPath) || !File.Exists(colorsPath))
            {
                Debug.LogWarning("Item data not complete!");
                flag.Finished = true;
                flag.Failed = true;
                return;
            }

            ComponentRegistry = componentRegistry;

            try
            {
                var items = Json.ParseJson(File.ReadAllText(itemsPath, Encoding.UTF8));

                foreach (var (key, itemDef) in items.Properties)
                {
                    if (int.TryParse(itemDef.Properties["protocol_id"].StringValue, out int numId))
                    {
                        var itemId = ResourceLocation.FromString(key);
                        var actionType = ItemActionTypeHelper.GetItemActionType(itemDef.Properties["action_type"].StringValue);

                        ResourceLocation? itemBlockId = null;
                        if (itemDef.Properties.TryGetValue("block", out var val))
                        {
                            itemBlockId = ResourceLocation.FromString(val.StringValue);
                            blockIdToBlockItemId.Add(itemBlockId.Value, itemId);
                        }
                        
                        EquipmentSlot equipmentSlot = EquipmentSlot.Mainhand;
                        if (itemDef.Properties.TryGetValue("equipment_slot", out val))
                        {
                            equipmentSlot = EquipmentSlotHelper.GetEquipmentSlot(val.StringValue);
                        }
                        
                        var defaultComponents = new Dictionary<ResourceLocation, StructuredComponent>();
                        if (itemDef.Properties.TryGetValue("default_components", out val))
                        {
                            foreach (var (id, compData) in val.Properties)
                            {
                                var compId = ResourceLocation.FromString(id);
                                var comp = componentRegistry.ParseComponentFromJson(compId, compData);
                                
                                defaultComponents.Add(compId, comp);
                            }
                        }
                        
                        Item newItem = new(itemId, actionType, itemBlockId, equipmentSlot, defaultComponents);

                        if (actionType is ItemActionType.Axe or ItemActionType.Pickaxe or ItemActionType.Shovel or ItemActionType.Hoe or ItemActionType.Sword)
                        {
                            newItem.TierType = TierTypeHelper.GetTierType(itemDef.Properties["tier"].StringValue);
                        }

                        AddEntry(itemId, numId, newItem);
                    }
                }

                // Hardcoded placeholder types for internal and network use
                AddDirectionalEntry(Item.UNKNOWN.ItemId, -2, Item.UNKNOWN);
                AddDirectionalEntry(Item.NULL.ItemId,    -1, Item.NULL);

                // Load item color rules...
                Json.JSONData colorRules = Json.ParseJson(File.ReadAllText(colorsPath, Encoding.UTF8));
                
                if (colorRules.Properties.TryGetValue("dynamic", out var dynamicRulesProperty))
                {
                    foreach (var (ruleName, ruleValue) in dynamicRulesProperty.Properties)
                    {
                        Func<ItemStack, float3[]> ruleFunc = ruleName switch
                        {
                            "potion"  => GetPotionColor,

                            _         => _ => new[] { float3.zero }
                        };

                        foreach (var itemId in ruleValue.DataArray
                                     .Select(item => ResourceLocation.FromString(item.StringValue)))
                        {
                            if (!itemColorRules.TryAdd(itemId, ruleFunc))
                            {
                                Debug.LogWarning($"Failed to apply dynamic color rules to {itemId}!");
                            }
                        }
                    }
                }

                if (colorRules.Properties.TryGetValue("fixed", out var fixedRulesProperty))
                {
                    foreach (var fixedRule in fixedRulesProperty.Properties)
                    {
                        var itemId = ResourceLocation.FromString(fixedRule.Key);

                        var fixedColor = VectorUtil.Json2Float3(fixedRule.Value) / 255F;

                        if (!itemColorRules.TryAdd(itemId, ruleFunc))
                        {
                            Debug.LogWarning($"Failed to apply fixed color rules to {itemId}!");
                        }

                        continue;

                        float3[] ruleFunc(ItemStack itemStack) => new[] { fixedColor };
                    }
                }

                if (colorRules.Properties.TryGetValue("fixed_multicolor", out fixedRulesProperty))
                {
                    foreach (var fixedRule in fixedRulesProperty.Properties)
                    {
                        var itemId = ResourceLocation.FromString(fixedRule.Key);

                        var colorList = fixedRule.Value.DataArray.ToArray();
                        var fixedColors = new float3[colorList.Length];

                        for (int c = 0;c < colorList.Length;c++)
                            fixedColors[c] = VectorUtil.Json2Float3(colorList[c]) / 255F;

                        if (!itemColorRules.TryAdd(itemId, ruleFunc))
                        {
                            Debug.LogWarning($"Failed to apply fixed multi-color rules to {itemId}!");
                        }

                        continue;

                        float3[] ruleFunc(ItemStack itemStack) => fixedColors;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading items: {e.Message}");
                flag.Failed = true;
            }
            finally
            {
                FreezeEntries();
                flag.Finished = true;
            }
        }
    }
}
