﻿namespace CraftSharp
{
    /// <summary>
    /// See https://wiki.vg/Particles
    /// </summary>
    public enum ParticleExtraDataType
    {
        None,
        /// <summary>
        /// VarInt
        /// </summary>
        Block,
        /// <summary>
        /// Float x4
        /// </summary>
        Dust,
        /// <summary>
        /// Float x7
        /// </summary>
        DustColorTransition, // Added in 1.17
        /// <summary>
        /// Int
        /// </summary>
        EntityEffect,        // Added in 1.20.5
        /// <summary>
        /// Float
        /// </summary>
        SculkCharge,         // Added in 1.19
        /// <summary>
        /// Slot
        /// </summary>
        Item,
        /// <summary>
        /// Location + [String + (Location | VarInt)] + VarInt
        /// </summary>
        Vibration,           // 1.17 - 1.18.2
        /// <summary>
        /// [String + (Location | VarInt + Float)] + VarInt
        /// </summary>
        VibrationV2,         // 1.19 - 1.20.4
        /// <summary>
        /// [VarInt + (Location | VarInt + Float)] + VarInt
        /// </summary>
        VibrationV3,         // 1.20.5+
        /// <summary>
        /// VarInt
        /// </summary>
        Shriek               // Added in 1.19
    }
}
