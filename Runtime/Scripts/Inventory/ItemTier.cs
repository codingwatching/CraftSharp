﻿using System.Collections.Generic;

namespace CraftSharp
{
    public enum TierLevel
    {
        Wood,
        Stone,
        Iron,
        Diamond,
        Netherite,
        Gold
    }

    public record ItemTier(
        int Level,
        int Durability,
        float Speed,
        float Damage,
        int EnchantmentValue)
    {
        public static readonly Dictionary<TierLevel, ItemTier> Tiers = new()
        {
            { TierLevel.Wood, new ItemTier(0, 59, 2.0f, 0.0f, 15) },
            { TierLevel.Stone, new ItemTier(1, 131, 4.0f, 1.0f, 5) },
            { TierLevel.Iron, new ItemTier(2, 250, 6.0f, 2.0f, 14) },
            { TierLevel.Diamond, new ItemTier(3, 1561, 8.0f, 3.0f, 10) },
            { TierLevel.Netherite, new ItemTier(4, 2031, 9.0f, 4.0f, 15) },
            { TierLevel.Gold, new ItemTier(0, 32, 12.0f, 0.0f, 22) }
        };

        public int Level { get; } = Level;
        public int Durability { get; } = Durability;
        public float Speed { get; } = Speed;
        public float Damage { get; } = Damage;
        public int EnchantmentValue { get; } = EnchantmentValue;

        // public Ingredient repairIngredient { get; }
    }
}