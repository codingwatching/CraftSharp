﻿#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp
{
    /// <summary>
    /// Represents a Minecraft World
    /// </summary>
    public class World
    {
        // Using biome colors of minecraft:plains as default
        // See https://minecraft.fandom.com/wiki/Plains
        public const int DEFAULT_FOLIAGE = 0x77AB2F;
        public const int DEFAULT_GRASS = 0x91BD59;
        public const int DEFAULT_WATER = 0x3F76E4;

        public static readonly Biome DUMMY_BIOME = new(ResourceLocation.INVALID,
                0, DEFAULT_FOLIAGE, DEFAULT_GRASS, DEFAULT_WATER, 0, 0);
        
        #region Static data storage and access
        
        /// <summary>
        /// The dimension info of the world
        /// </summary>
        private static DimensionType curDimensionType = new();

        private static ResourceLocation curDimensionId = ResourceLocation.INVALID;
        private static readonly Dictionary<ResourceLocation, DimensionType> knownDimensionTypes = new();

        public static bool BiomesInitialized { get; private set; } = false;

        private class BiomePalette : IdentifierPalette<Biome>
        {
            protected override string Name => "Biome Palette";

            public void Register(ResourceLocation id, int numId, Biome obj)
            {
                base.AddEntry(id, numId, obj);
            }

            public void Clear()
            {
                base.ClearEntries();
            }

            protected override Biome UnknownObject => DUMMY_BIOME;
        }

        /// <summary>
        /// The biomes of the world
        /// </summary>
        private static readonly BiomePalette BiomeRegistry = new();

        public static readonly ResourceLocation DIMENSION_TYPE_ID = new("dimension_type");

        public static readonly ResourceLocation WORLDGEN_BIOME_ID = new("worldgen/biome");

        /// <summary>
        /// Storage of all dimensional type data - 1.19.1 and above
        /// </summary>
        public static void StoreDimensionTypeList((ResourceLocation id, int numId, object? obj)[] dimensionTypeList)
        {
            foreach (var (dimensionId, _, dimensionDef) in dimensionTypeList)
            {
                Dictionary<string, object> dimensionType = (Dictionary<string, object>) dimensionDef!;
                StoreOneDimensionType(dimensionId, dimensionType);
            }
        }

        /// <summary>
        /// Store one dimension type - Directly used in 1.16.2 to 1.18.2
        /// </summary>
        /// <param name="dimensionTypeId">Dimension name</param>
        /// <param name="dimensionType">Dimension Type nbt data</param>
        public static void StoreOneDimensionType(ResourceLocation dimensionTypeId, Dictionary<string, object> dimensionType)
        {
            if (knownDimensionTypes.ContainsKey(dimensionTypeId))
                knownDimensionTypes.Remove(dimensionTypeId);
            knownDimensionTypes.Add(dimensionTypeId, new DimensionType(dimensionTypeId, dimensionType));
        }

        /// <summary>
        /// Set current dimension type - 1.16 and above
        /// </summary>
        /// <param name="dimensionTypeId">	The id of the dimension type</param>
        public static void SetDimensionType(ResourceLocation dimensionTypeId)
        {
            if (!knownDimensionTypes.ContainsKey(dimensionTypeId))
            {
                knownDimensionTypes.Add(dimensionTypeId, new DimensionType());
                Debug.LogWarning($"{dimensionTypeId} is not registered. Using a dummy overworld-like dimension type.");
            }

            curDimensionType = knownDimensionTypes[dimensionTypeId]; // Should not fail
        }

        /// <summary>
        /// Set current dimension id
        /// </summary>
        public static void SetDimensionId(ResourceLocation dimensionId)
        {
            curDimensionId = dimensionId;
        }

        /// <summary>
        /// Get current dimension type
        /// </summary>
        /// <returns>Current dimension</returns>
        public static DimensionType GetDimensionType()
        {
            return curDimensionType;
        }

        /// <summary>
        /// Get current dimension id
        /// </summary>
        public static ResourceLocation GetDimensionId()
        {
            return curDimensionId;
        }

        public static Color32[] FoliageColormapPixels { get; set; } = { };
        public static Color32[] GrassColormapPixels { get; set; } = { };

        public static int ColormapSize { get; set; }
        
        /// <summary>
        /// Storage of all dimensional data - 1.19.1 and above
        /// </summary>
        public static void StoreBiomeList((ResourceLocation id, int numId, object? obj)[] biomeList)
        {
            // Clear up registry
            BiomeRegistry.Clear();
            
            if (FoliageColormapPixels.Length == 0 || GrassColormapPixels.Length == 0)
            {
                Debug.LogWarning("Biome colormap is not available. Color lookup will not be performed.");
            }

            foreach (var (id, numId, obj) in biomeList)
            {
                StoreOneBiome(id, numId, ((Dictionary<string, object>) obj!)!);
            }

            BiomesInitialized = true;
        }

        /// <summary>
        /// Store one biome
        /// </summary>
        private static void StoreOneBiome(ResourceLocation biomeId, int numId, Dictionary<string, object> biomeDef)
        {
            //Debug.Log($"Biome registered:\n{Json.Dictionary2Json(biomeData)}");

            int sky = 0, foliage = 0, grass = 0, water = 0, fog = 0, waterFog = 0;
            float temperature = 0F, downfall = 0F;
            Precipitation precipitation = Precipitation.None;

            if (biomeDef.TryGetValue("downfall", out var value))
                downfall = (float) value;
                            
            if (biomeDef.TryGetValue("temperature", out var value1))
                temperature = (float) value1;
            
            if (biomeDef.ContainsKey("precipitation"))
            {
                precipitation = ((string) biomeDef["precipitation"]).ToLower() switch
                {
                    "rain" => Precipitation.Rain,
                    "snow" => Precipitation.Snow,
                    "none" => Precipitation.None,

                    _      => Precipitation.Unknown
                };

                if (precipitation == Precipitation.Unknown)
                    Debug.LogWarning($"Unexpected precipitation type: {biomeDef["precipitation"]}");
            }

            if (biomeDef.TryGetValue("effects", out var value2))
            {
                var effects = (Dictionary<string, object>)value2;

                if (effects.TryGetValue("sky_color", out var effect))
                    sky = (int) effect;
                
                var adjustedTemp = Mathf.Clamp01(temperature);
                var adjustedRain = Mathf.Clamp01(downfall) * adjustedTemp;

                int sampleX = (int) ((1F - adjustedTemp) * ColormapSize);
                int sampleY = (int) (adjustedRain * ColormapSize);

                if (effects.TryGetValue("foliage_color", out var effect1))
                    foliage = (int) effect1;
                else // Read foliage color from color map. See https://minecraft.fandom.com/wiki/Color
                {
                    var color = (FoliageColormapPixels.Length == 0) ? (Color32) Color.magenta :
                            FoliageColormapPixels[sampleY * ColormapSize + sampleX];
                    foliage = (color.r << 16) | (color.g << 8) | color.b;
                }
                
                if (effects.TryGetValue("grass_color", out var effect2))
                    grass = (int) effect2;
                else // Read grass color from color map. Same as above
                {
                    var color = (GrassColormapPixels.Length == 0) ? (Color32) Color.magenta :
                            GrassColormapPixels[sampleY * ColormapSize + sampleX];
                    grass = (color.r << 16) | (color.g << 8) | color.b;
                }
                
                if (effects.TryGetValue("fog_color", out var effect3))
                    fog = (int) effect3;
                
                if (effects.TryGetValue("water_color", out var effect4))
                    water = (int) effect4;
                
                if (effects.TryGetValue("water_fog_color", out var effect5))
                    waterFog = (int) effect5;
            }

            Biome biome = new(biomeId, sky, foliage, grass, water, fog, waterFog)
            {
                Temperature = temperature,
                Downfall = downfall,
                Precipitation = precipitation
            };

            BiomeRegistry.Register(biomeId, numId, biome);
        }

        #endregion

        #region World instance data storage and access

        /// <summary>
        /// The chunks contained into the Minecraft world
        /// </summary>
        private readonly ConcurrentDictionary<int2, ChunkColumn> columns = new();

        /// <summary>
        /// Read, set or unload the specified chunk column
        /// </summary>
        /// <param name="chunkX">ChunkColumn X</param>
        /// <param name="chunkZ">ChunkColumn Z</param>
        /// <returns>Chunk at the given location</returns>
        public ChunkColumn? this[int chunkX, int chunkZ]
        {
            get
            {
                columns.TryGetValue(new(chunkX, chunkZ), out ChunkColumn? chunkColumn);
                return chunkColumn;
            }
            set
            {
                int2 chunkCoord = new(chunkX, chunkZ);
                if (value is null)
                    columns.TryRemove(chunkCoord, out _);
                else
                    columns.AddOrUpdate(chunkCoord, value, (_, _) => value);
            }
        }

        /// <summary>
        /// Check whether the data of a chunk column is loaded
        /// </summary>
        /// <param name="chunkX">ChunkColumn X</param>
        /// <param name="chunkZ">ChunkColumn Z</param>
        /// <returns>True if chunk column data is ready</returns>
        public bool IsChunkColumnLoaded(int chunkX, int chunkZ)
        {
            // Chunk column data is sent one whole column per time,
            // a whole air chunk is represented by null
            if (columns.TryGetValue(new(chunkX, chunkZ), out ChunkColumn? chunkColumn))
                return chunkColumn is { FullyLoaded: true, LightingPresent: true };
            return false;
        }

        /// <summary>
        /// Store chunk at the specified location
        /// </summary>
        /// <param name="chunkX">ChunkColumn X</param>
        /// <param name="chunkY">ChunkColumn Y</param>
        /// <param name="chunkZ">ChunkColumn Z</param>
        /// <param name="chunkColumnSize">ChunkColumn size</param>
        /// <param name="chunk">Chunk data</param>
        public void StoreChunk(int chunkX, int chunkY, int chunkZ, int chunkColumnSize, Chunk? chunk)
        {
            ChunkColumn chunkColumn = columns.GetOrAdd(new(chunkX, chunkZ), (_) => new(chunkColumnSize));
            chunkColumn[chunkY] = chunk;
        }

        /// <summary>
        /// Create empty chunk column at the specified location
        /// </summary>
        /// <param name="chunkX">ChunkColumn X</param>
        /// <param name="chunkZ">ChunkColumn Z</param>
        /// <param name="chunkColumnSize">ChunkColumn size</param>
        public void CreateEmptyChunkColumn(int chunkX, int chunkZ, int chunkColumnSize)
        {
            columns.GetOrAdd(new(chunkX, chunkZ), (_) => new(chunkColumnSize));
        }

        /// <summary>
        /// Get chunk column at the specified location
        /// </summary>
        /// <param name="blockLoc">Location to retrieve chunk column</param>
        /// <returns>The chunk column</returns>
        public ChunkColumn? GetChunkColumn(BlockLoc blockLoc)
        {
            return this[blockLoc.GetChunkX(), blockLoc.GetChunkZ()];
        }

        public static readonly Block AIR_INSTANCE = new(0);

        /// <summary>
        /// Get block at the specified location
        /// </summary>
        /// <param name="blockLoc">Location to retrieve block from</param>
        /// <returns>Block at specified location or Air if the location is not loaded</returns>
        public Block GetBlock(BlockLoc blockLoc)
        {
            var column = GetChunkColumn(blockLoc);
            if (column != null)
            {
                return column.GetBlock(blockLoc);
            }
            return AIR_INSTANCE; // Air
        }

        /// <summary>
        /// Get block light at the specified location
        /// </summary>
        public byte GetBlockLight(BlockLoc blockLoc)
        {
            var column = GetChunkColumn(blockLoc);
            if (column != null)
                return column.GetBlockLight(blockLoc);
            
            return (byte) 0; // Not available
        }

        /// <summary>
        /// Set block light at the specified location
        /// </summary>
        public void SetBlockLight(BlockLoc blockLoc, byte newValue)
        {
            GetChunkColumn(blockLoc)?.SetBlockLight(blockLoc, newValue);
        }

        /// <summary>
        /// Set block light for a chunk
        /// </summary>
        public void SetBlockLightForChunk(int cx, int chunkYIndex, int cz, byte[,,] updatedLights)
        {
            // Get the current chunk column
            var chunkColumn = this[cx, cz];
            
            if (chunkColumn is not null) // Chunk column is not empty
            {
                var arr = chunkColumn.BlockLight;

                // Go through all valid xz locations within this chunk column
                for (int x = 0; x < 16; x++)
                {
                    for (int z = 0; z < 16; z++)
                    {
                        // Then go though all blocks in this line
                        for (int y = 0; y < 16; y++)
                        {
                            // BlockLight array has a 1-chunk padding on each end
                            arr[((y + ((chunkYIndex + 1) << 4)) << 8) | (z << 4) | x] = updatedLights[16 + x, 16 + y, 16 + z];
                        }
                    }
                }
            }
        }

        public void GetLightDataCacheForChunk(int chunkX, int chunkYIndex, int chunkZ, bool emissionOrBlockage, byte[,,] result)
        {
            // Min coordinate on each axis (inclusive)
            int minX = (chunkX - 1) << 4, minZ = (chunkZ - 1) << 4;
            // Min coordinate on each axis (inclusive)
            // Max coordinate on each axis (exclusive)
            int minCX = chunkX - 1;  // Min chunk X (inclusive)
            int minCZ = chunkZ - 1;  // Min chunk Z (inclusive)
            int maxCX = chunkX + 1;  // Max chunk X (inclusive)
            int maxCZ = chunkZ + 1;  // Max chunk Z (inclusive)

            for (int cx = minCX; cx <= maxCX; cx++)
                for (int cz = minCZ; cz <= maxCZ; cz++)
                {
                    // Get the current chunk column
                    var chunkColumn = this[cx, cz];
                    
                    if (chunkColumn is not null) // Chunk column is not empty
                    {
                        var maxBlocYIndex = chunkColumn.ColumnSize << 4;
                        var arr = emissionOrBlockage ? chunkColumn.LightEmissionCache : chunkColumn.LightBlockageCache;

                        // Go through all valid xz locations within this chunk column
                        for (int blocX = cx << 4; blocX < (cx + 1) << 4; blocX++)
                        {
                            int resX = blocX - minX;
                            for (int blocZ = cz << 4; blocZ < (cz + 1) << 4; blocZ++)
                            {
                                int resZ = blocZ - minZ;
                                // Then go though all blocks in this line
                                for (int resY = 0; resY < 48; resY++)
                                {
                                    int sy = (chunkYIndex << 4) + resY - 16;
                                    if (sy < 0 || sy >= maxBlocYIndex) continue; // Make sure we're not sampling at invalid height

                                    int index = (sy << 8) | ((resZ % 16) << 4) | (resX % 16);
                                    result[resX, resY, resZ] = arr[index];
                                }
                            }
                        }
                    }
                    /* Otherwise leave the values 0 */
                }
        }

        public void GetValuesFromSection<T>(int minX, int minY, int minZ, int sizeX, int sizeY, int sizeZ, Func<Block, T> valueGetter, T[,,] result)
        {
            // Min coordinate on each axis (inclusive)
            // Max coordinate on each axis (exclusive)
            int maxX = minX + sizeX, maxZ = minZ + sizeZ, maxY = minY + sizeY;
            int minCX = minX >> 4;        // Min chunk X (inclusive)
            int minCZ = minZ >> 4;        // Min chunk Z (inclusive)
            int maxCX = (maxX - 1) >> 4;  // Max chunk X (inclusive)
            int maxCZ = (maxZ - 1) >> 4;  // Max chunk Z (inclusive)

            for (int cx = minCX; cx <= maxCX; cx++)
                for (int cz = minCZ; cz <= maxCZ; cz++)
                {
                    // Get the current chunk column
                    var chunkColumn = this[cx, cz];

                    if (chunkColumn is not null) // Chunk column is not empty
                    {
                        // Go through all valid xz locations within this chunk column
                        for (int blocX = math.max(minX, cx << 4); blocX < math.min(maxX, (cx + 1) << 4); blocX++)
                        {
                            int resX = blocX - minX;
                            for (int blocZ = math.max(minZ, cz << 4); blocZ < math.min(maxZ, (cz + 1) << 4); blocZ++)
                            {
                                int resZ = blocZ - minZ;
                                // Then go though all blocks in this line
                                for (int blocY = minY; blocY < maxY; blocY++)
                                {
                                    int resY = blocY - minY;
                                    var blocLoc = new BlockLoc(blocX, blocY, blocZ);

                                    result[resX, resY, resZ] = valueGetter(chunkColumn.GetBlock(blocLoc));
                                }
                            }
                        }
                    }
                    else // Chunk column is empty
                    {
                        var val = valueGetter(AIR_INSTANCE);

                        // Go through all valid xz locations within this chunk column
                        for (int blocX = math.max(minX, cx << 4); blocX < math.min(maxX, (cx + 1) << 4); blocX++)
                        {
                            int resX = blocX - minX;
                            for (int blocZ = math.max(minZ, cz << 4); blocZ < math.min(maxZ, (cz + 1) << 4); blocZ++)
                            {
                                int resZ = blocZ - minZ;
                                // Then go though all blocks in this line
                                for (int blocY = minY; blocY < maxY; blocY++)
                                {
                                    int resY = blocY - minY;

                                    result[resX, resY, resZ] = val;
                                }
                            }
                        }
                    }
                }
        }

        /// <summary>
        /// Get all essential data for doing a chunk mesh build.
        /// </summary>
        public ChunkBuildData GetChunkBuildData(int chunkX, int chunkZ, int chunkYIndex)
        {
            var result = new ChunkBuildData();
            var blocs = result.Blocks = new Block[Chunk.PADDED, Chunk.PADDED, Chunk.PADDED];
            var light = result.Light = new byte[Chunk.PADDED, Chunk.PADDED, Chunk.PADDED];
            var color = result.Color = new float3[Chunk.SIZE, Chunk.SIZE, Chunk.SIZE];
            
            int minCX = chunkX - 1;  // Min chunk X
            int minCZ = chunkZ - 1;  // Min chunk Z
            int maxCX = chunkX + 1;  // Max chunk X
            int maxCZ = chunkZ + 1;  // Max chunk Z

            // Max coordinate on each axis (inclusive)
            int minX = (chunkX << 4) - 1,              minZ = (chunkZ << 4) - 1,              minY = (chunkYIndex << 4) + GetDimensionType().minY - 1;
            // Max coordinate on each axis (exclusive)
            int maxX = (chunkX << 4) + Chunk.SIZE + 1, maxZ = (chunkZ << 4) + Chunk.SIZE + 1, maxY = (chunkYIndex << 4) + GetDimensionType().minY + Chunk.SIZE + 1;

            for (int cx = minCX; cx <= maxCX; cx++)
                for (int cz = minCZ; cz <= maxCZ; cz++)
                {
                    // Get the current chunk column
                    var chunkColumn = this[cx, cz];

                    if (chunkColumn is not null) // Chunk column is not empty
                    {
                        // Go through all valid xz locations within this chunk column
                        for (int blocX = math.max(minX, cx << 4); blocX < math.min(maxX, (cx + 1) << 4); blocX++)
                        {
                            int resX = blocX - minX;
                            for (int blocZ = math.max(minZ, cz << 4); blocZ < math.min(maxZ, (cz + 1) << 4); blocZ++)
                            {
                                int resZ = blocZ - minZ;
                                // Then go though all blocks in this line
                                for (int blocY = minY; blocY < maxY; blocY++)
                                {
                                    int resY = blocY - minY;
                                    var blocLoc = new BlockLoc(blocX, blocY, blocZ);

                                    var bloc = chunkColumn.GetBlock(blocLoc);
                                    blocs[resX, resY, resZ] = bloc;
                                    light[resX, resY, resZ] = chunkColumn.GetBlockLight(blocLoc);
                                    
                                    if (resX is > 0 and <= Chunk.SIZE && resY is > 0 and <= Chunk.SIZE && resZ is > 0 and <= Chunk.SIZE)
                                    {
                                        // No padding for block color
                                        color[resX - 1, resY - 1, resZ - 1] = BlockStatePalette.INSTANCE.GetBlockColor(bloc.StateId, this, blocLoc, bloc.State);
                                    }
                                }
                            }
                        }
                    }
                    else // Chunk column is empty
                    {
                        // Go through all valid xz locations within this chunk column
                        for (int blocX = math.max(minX, cx << 4); blocX < math.min(maxX, (cx + 1) << 4); blocX++)
                        {
                            int resX = blocX - minX;
                            for (int blocZ = math.max(minZ, cz << 4); blocZ < math.min(maxZ, (cz + 1) << 4); blocZ++)
                            {
                                int resZ = blocZ - minZ;
                                // Then go though all blocks in this line
                                for (int blocY = minY; blocY < maxY; blocY++)
                                {
                                    int resY = blocY - minY;

                                    blocs[resX, resY, resZ] = AIR_INSTANCE;
                                    light[resX, resY, resZ] = 0;
                                }
                            }
                        }
                    }
                }
            
            return result;
        }

        /// <summary>
        /// Clear all terrain data from the world
        /// </summary>
        public void Clear()
        {
            columns.Clear();
        }

        public byte[] GetLiquidHeights(BlockLoc blockLoc)
        {
            // Height References
            //  NE---E---SE
            //  |         |
            //  N    @    S
            //  |         |
            //  NW---W---SW

            return new byte[] {
                16, 16, 16,
                16, 16, 16,
                16, 16, 16
            };
        }

        private const int COLOR_SAMPLE_RADIUS = 2;
        private const int COLOR_SAMPLE_RADIUS_SQR = COLOR_SAMPLE_RADIUS * COLOR_SAMPLE_RADIUS;

        /// <summary>
        /// Get biome at the specified location
        /// </summary>
        public Biome GetBiome(BlockLoc blockLoc)
        {
            var column = GetChunkColumn(blockLoc);
            if (column != null)
            {
                if (BiomeRegistry.TryGetByNumId(column.GetBiomeId(blockLoc), out Biome biome))
                {
                    return biome;
                }
            }
            
            return DUMMY_BIOME; // Not available
        }

        public float3 GetFoliageColor(BlockLoc blockLoc)
        {
            int cnt = 0;
            float3 colorSum = float3.zero;
            for (int x = -COLOR_SAMPLE_RADIUS;x <= COLOR_SAMPLE_RADIUS;x++)
                for (int y = -COLOR_SAMPLE_RADIUS;y <= COLOR_SAMPLE_RADIUS;y++)
                    for (int z = -COLOR_SAMPLE_RADIUS;z <= COLOR_SAMPLE_RADIUS;z++)
                    {
                        if (x * x + y * y + z * z > COLOR_SAMPLE_RADIUS_SQR)
                            continue;
                        
                        var b = GetBiome(blockLoc + new BlockLoc(x, y, z));
                        if (b != DUMMY_BIOME)
                        {
                            cnt++;
                            colorSum += b.FoliageColor;
                        }
                    }

            return cnt == 0 ? DUMMY_BIOME.FoliageColor : colorSum / cnt;
        }

        public float3 GetGrassColor(BlockLoc blockLoc)
        {
            int cnt = 0;
            float3 colorSum = float3.zero;
            for (int x = -COLOR_SAMPLE_RADIUS;x <= COLOR_SAMPLE_RADIUS;x++)
                for (int y = -COLOR_SAMPLE_RADIUS;y <= COLOR_SAMPLE_RADIUS;y++)
                    for (int z = -COLOR_SAMPLE_RADIUS;z <= COLOR_SAMPLE_RADIUS;z++)
                    {
                        if (x * x + y * y + z * z > COLOR_SAMPLE_RADIUS_SQR)
                            continue;
                        
                        var b = GetBiome(blockLoc + new BlockLoc(x, y, z));
                        if (b != DUMMY_BIOME)
                        {
                            cnt++;
                            colorSum += b.GrassColor;
                        }
                    }
            
            return cnt == 0 ? DUMMY_BIOME.GrassColor : colorSum / cnt;
        }

        public float3 GetWaterColor(BlockLoc blockLoc)
        {
            int cnt = 0;
            float3 colorSum = float3.zero;
            for (int x = -COLOR_SAMPLE_RADIUS;x <= COLOR_SAMPLE_RADIUS;x++)
                for (int y = -COLOR_SAMPLE_RADIUS;y <= COLOR_SAMPLE_RADIUS;y++)
                    for (int z = -COLOR_SAMPLE_RADIUS;z <= COLOR_SAMPLE_RADIUS;z++)
                    {
                        if (x * x + y * y + z * z > COLOR_SAMPLE_RADIUS_SQR)
                            continue;
                        
                        var b = GetBiome(blockLoc + new BlockLoc(x, y, z));
                        if (b != DUMMY_BIOME)
                        {
                            cnt++;
                            colorSum += b.WaterColor;
                        }
                    }
            
            return cnt == 0 ? DUMMY_BIOME.WaterColor : colorSum / cnt;
        }

        #endregion
    }
}