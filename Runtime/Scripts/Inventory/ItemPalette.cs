﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

namespace CraftSharp
{
    public class ItemPalette : IdentifierPalette<Item>
    {
        private static readonly char SP = Path.DirectorySeparatorChar;
        public static readonly ItemPalette INSTANCE = new();
        protected override string Name => "Item Palette";
        protected override Item UnknownObject => Item.UNKNOWN;

        private readonly Dictionary<ResourceLocation, Func<ItemStack, float3[]>> itemColorRules = new();


        public bool IsTintable(ResourceLocation identifier)
        {
            return itemColorRules.ContainsKey(identifier);
        }

        public Func<ItemStack, float3[]> GetTintRule(ResourceLocation identifier)
        {
            if (itemColorRules.ContainsKey(identifier))
                return itemColorRules[identifier];
            return null;
        }

        protected override void ClearEntries()
        {
            base.ClearEntries();
            itemColorRules.Clear();
        }

        /// <summary>
        /// Load item data from external files.
        /// </summary>
        /// <param name="dataVersion">Item data version</param>
        /// <param name="flag">Data load flag</param>
        public void PrepareData(string dataVersion, DataLoadFlag flag)
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

            try
            {
                var items = Json.ParseJson(File.ReadAllText(itemsPath, Encoding.UTF8));

                foreach (var (key, itemDef) in items.Properties)
                {
                    if (int.TryParse(itemDef.Properties["protocol_id"].StringValue, out int numId))
                    {
                        var itemId = ResourceLocation.FromString(key);

                        var rarity = itemDef.Properties["rarity"].StringValue switch
                        {
                            "common"   => ItemRarity.Common,
                            "uncommon" => ItemRarity.Uncommon,
                            "rare"     => ItemRarity.Rare,
                            "epic"     => ItemRarity.Epic,

                            _          => ItemRarity.Common
                        };

                        var actionType = itemDef.Properties["action_type"].StringValue switch
                        {
                            "none"             => ItemActionType.None,

                            "block"            => ItemActionType.Block,
                            "lighter"          => ItemActionType.Lighter,
                            "solid_bucket"     => ItemActionType.SolidBucket,
                            "fluid_bucket"     => ItemActionType.FluidBucket,
                            
                            "sword"            => ItemActionType.Sword,
                            "bow"              => ItemActionType.Bow,
                            "crossbow"         => ItemActionType.Crossbow,
                            "trident"          => ItemActionType.Trident,
                            "shield"           => ItemActionType.Shield,
                            "drinkable_bottle" => ItemActionType.DrinkableBottle,
                            "drinkable_bucket" => ItemActionType.DrinkableBucket,
                            "food_on_a_stick"  => ItemActionType.FoodOnAStick,
                            "empty_map"        => ItemActionType.EmptyMap,
                            "writable_book"    => ItemActionType.WritableBook,
                            "written_book"     => ItemActionType.WrittenBook,
                            "fishing_rod"      => ItemActionType.FishingRod,
                            "knowledge_book"   => ItemActionType.KnowledgeBook,
                            "spyglass"         => ItemActionType.Spyglass,
                            "bundle"           => ItemActionType.Bundle,
                            "instrument"       => ItemActionType.Instrument,

                            "shears"           => ItemActionType.Shears,
                            "axe"              => ItemActionType.Axe,
                            "pickaxe"          => ItemActionType.Pickaxe,
                            "shovel"           => ItemActionType.Shovel,
                            "hoe"              => ItemActionType.Hoe,
                            "bone_meal"        => ItemActionType.BoneMeal,
                            "record"           => ItemActionType.Record,
                            "empty_bottle"     => ItemActionType.EmptyBottle,
                            "empty_bucket"     => ItemActionType.EmptyBucket,
                            "debug_stick"      => ItemActionType.DebugStick,
                            "honeycomb"        => ItemActionType.Honeycomb,
                            "brush"            => ItemActionType.Brush,

                            "lead"             => ItemActionType.Lead,
                            "name_tag"         => ItemActionType.NameTag,

                            "splash_potion"    => ItemActionType.SplashPotion,
                            "lingering_potion" => ItemActionType.LingeringPotion,
                            "spawn_egg"        => ItemActionType.SpawnEgg,
                            "firework_rocket"  => ItemActionType.FireworkRocket,
                            "hanging_entity"   => ItemActionType.HangingEntity,
                            "armor_stand"      => ItemActionType.ArmorStand,
                            "boat"             => ItemActionType.Boat,
                            "minecart"         => ItemActionType.Minecart,
                            "end_crystal"      => ItemActionType.EndCrystal,
                            "eye_of_ender"     => ItemActionType.EyeOfEnder,
                            "ender_pearl"      => ItemActionType.EnderPearl,
                            "throwable_item"   => ItemActionType.ThrowableItem,
                            "mob_bucket"       => ItemActionType.MobBucket,                            

                            _                  => throw new InvalidDataException($"Item action type {itemDef.Properties["action_type"].StringValue} is not defined!")
                        };

                        var stackLimit = int.Parse(itemDef.Properties["stack_limit"].StringValue);
                        var edible = bool.Parse(itemDef.Properties["edible"].StringValue);

                        ResourceLocation? itemBlockId = null;

                        if (itemDef.Properties.TryGetValue("block", out Json.JSONData blockId))
                        {
                            itemBlockId = ResourceLocation.FromString(blockId.StringValue);
                        }

                        Item newItem = new(itemId, stackLimit, rarity, actionType, edible, itemBlockId);

                        if (edible) // Set food settings
                        {
                            newItem.AlwaysEdible = bool.Parse(itemDef.Properties["always_edible"].StringValue);
                            newItem.FastFood = bool.Parse(itemDef.Properties["fast_food"].StringValue);
                        }

                        if (actionType == ItemActionType.Axe || actionType == ItemActionType.Pickaxe || actionType == ItemActionType.Shovel ||
                            actionType == ItemActionType.Hoe || actionType == ItemActionType.Sword)
                        {
                            newItem.TierType = itemDef.Properties["tier"].StringValue switch
                            {
                                "wood"      => TierType.Wood,
                                "stone"     => TierType.Stone,
                                "iron"      => TierType.Iron,
                                "diamond"   => TierType.Diamond,
                                "netherite" => TierType.Netherite,
                                "gold"      => TierType.Gold,

                                _           => throw new InvalidDataException($"Item tier {itemDef.Properties["tier"].StringValue} is not defined!")
                            };
                        }

                        AddEntry(itemId, numId, newItem);
                    }
                }

                // Hardcoded placeholder types for internal and network use
                AddDirectionalEntry(Item.UNKNOWN.ItemId, -2, Item.UNKNOWN);
                AddDirectionalEntry(Item.NULL.ItemId,    -1, Item.NULL);

                // Load item color rules...
                Json.JSONData colorRules = Json.ParseJson(File.ReadAllText(colorsPath, Encoding.UTF8));

                if (colorRules.Properties.ContainsKey("fixed"))
                {
                    foreach (var fixedRule in colorRules.Properties["fixed"].Properties)
                    {
                        var itemId = ResourceLocation.FromString(fixedRule.Key);

                        if (idToNumId.TryGetValue(itemId, out int numId))
                        {
                            var fixedColor = VectorUtil.Json2Float3(fixedRule.Value) / 255F;
                            float3[] ruleFunc(ItemStack itemStack) => new float3[] { fixedColor };

                            if (!itemColorRules.TryAdd(itemId, ruleFunc))
                            {
                                Debug.LogWarning($"Failed to apply fixed color rules to {itemId} ({numId})!");
                            }
                        }
                        else
                        {
                            //Debug.LogWarning($"Applying fixed color rules to undefined item {itemId}!");
                        }
                    }
                }

                if (colorRules.Properties.ContainsKey("fixed_multicolor"))
                {
                    foreach (var fixedRule in colorRules.Properties["fixed_multicolor"].Properties)
                    {
                        var itemId = ResourceLocation.FromString(fixedRule.Key);

                        if (idToNumId.TryGetValue(itemId, out int numId))
                        {
                            var colorList = fixedRule.Value.DataArray.ToArray();
                            var fixedColors = new float3[colorList.Length];

                            for (int c = 0;c < colorList.Length;c++)
                                fixedColors[c] = VectorUtil.Json2Float3(colorList[c]) / 255F;

                            float3[] ruleFunc(ItemStack itemStack) => fixedColors;

                            if (!itemColorRules.TryAdd(itemId, ruleFunc))
                            {
                                Debug.LogWarning($"Failed to apply fixed multi-color rules to {itemId} ({numId})!");
                            }
                        }
                        else
                        {
                            //Debug.LogWarning($"Applying fixed multi-color rules to undefined item {itemId}!");
                        }
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
