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
        public override string Name => "Item Palette";
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
            string listsPath  = PathHelper.GetExtraDataFile("item_lists.json");
            string colorsPath = PathHelper.GetExtraDataFile("item_colors.json");

            if (!File.Exists(itemsPath) || !File.Exists(listsPath) || !File.Exists(colorsPath))
            {
                Debug.LogWarning("Item data not complete!");
                flag.Finished = true;
                flag.Failed = true;
                return;
            }

            try
            {
                // First read special item lists...
                var lists = new Dictionary<string, HashSet<ResourceLocation>>
                {
                    { "non_stackable", new() },
                    { "stacklimit_16", new() },
                    { "uncommon", new() },
                    { "rare", new() },
                    { "epic", new() }
                };

                Json.JSONData spLists = Json.ParseJson(File.ReadAllText(listsPath, Encoding.UTF8));
                foreach (var pair in lists)
                {
                    if (spLists.Properties.ContainsKey(pair.Key))
                    {
                        foreach (var block in spLists.Properties[pair.Key].DataArray)
                            pair.Value.Add(ResourceLocation.FromString(block.StringValue));
                    }
                }

                // References for later use
                var rarityU = lists["uncommon"];
                var rarityR = lists["rare"];
                var rarityE = lists["epic"];
                var nonStackables = lists["non_stackable"];
                var stackLimit16s = lists["stacklimit_16"];

                if (File.Exists(itemsPath))
                {
                    var items = Json.ParseJson(File.ReadAllText(itemsPath, Encoding.UTF8));

                    foreach (var item in items.Properties)
                    {
                        if (int.TryParse(item.Key, out int numId))
                        {
                            var itemId = ResourceLocation.FromString(item.Value.StringValue);

                            ItemRarity rarity = ItemRarity.Common;

                            if (rarityE.Contains(itemId))
                                rarity = ItemRarity.Epic;
                            else if (rarityR.Contains(itemId))
                                rarity = ItemRarity.Rare;
                            else if (rarityU.Contains(itemId))
                                rarity = ItemRarity.Uncommon;

                            int stackLimit = Item.DEFAULT_STACK_LIMIT;

                            if (nonStackables.Contains(itemId))
                                stackLimit = 1;
                            else if (stackLimit16s.Contains(itemId))
                                stackLimit = 16;

                            Item newItem = new(itemId)
                            {
                                Rarity = rarity,
                                StackLimit = stackLimit
                            };

                            AddEntry(itemId, numId, newItem);
                        }
                    }
                }

                // Hardcoded placeholder types for internal and network use
                AddDirectionalEntry(Item.UNKNOWN.ItemId, -2, Item.UNKNOWN);
                AddDirectionalEntry(Item.NULL.ItemId,    -1, Item.NULL);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading items: {e.Message}");
                flag.Failed = true;
            }
            finally
            {
                FreezeEntries();
            }

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

            flag.Finished = true;
        }
    }
}
