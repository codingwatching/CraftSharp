using System;
using System.Globalization;
using UnityEngine;
using Unity.Mathematics;

namespace CraftSharp
{
    public static class VectorUtil
    {
        // [1.2, 1.3] in Json -> float2(1.2, 1.3) in Unity
        public static float2 Json2Float2(Json.JSONData data)
        {
            var numbers = data.DataArray;
            if (numbers.Count == 2)
                return new float2(
                        float.Parse(numbers[0].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[1].StringValue, CultureInfo.InvariantCulture.NumberFormat));
            
            Debug.LogWarning($"Cannot convert to float2: Invalid json array \"{data.ToJson()}\"");
            return float2.zero;
        }

        // [1.2, 1.3, 1.4] in Json -> float3(1.2, 1.3, 1.4) in Unity
        public static float3 Json2Float3(Json.JSONData data)
        {
            var numbers = data.DataArray;
            if (numbers.Count == 3)
                return new float3(
                        float.Parse(numbers[0].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[1].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[2].StringValue, CultureInfo.InvariantCulture.NumberFormat));
            
            Debug.LogWarning($"Cannot convert to float3: Invalid json array \"{data.ToJson()}\"");
            return float3.zero;
        }

        // [1.2, 1.3, 1.4] in Json -> float3(1.4, 1.3, 1.2) in Unity, with x and z values swapped
        // See https://minecraft.fandom.com/wiki/Coordinates
        public static float3 Json2SwappedFloat3(Json.JSONData data)
        {
            var numbers = data.DataArray;
            if (numbers.Count == 3)
                return new float3(
                        float.Parse(numbers[2].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[1].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[0].StringValue, CultureInfo.InvariantCulture.NumberFormat));
            
            Debug.LogWarning($"Cannot convert to swapped float3: Invalid json array \"{data.ToJson()}\"");
            return float3.zero;
        }

        // [1.2, 1.3, 1.4, 1.5] in Json -> float4(1.2, 1.3, 1.4, 1.5) in Unity
        public static float4 Json2Float4(Json.JSONData data)
        {
            var numbers = data.DataArray;
            if (numbers.Count == 4)
                return new float4(
                        float.Parse(numbers[0].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[1].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[2].StringValue, CultureInfo.InvariantCulture.NumberFormat),
                        float.Parse(numbers[3].StringValue, CultureInfo.InvariantCulture.NumberFormat));
            
            Debug.LogWarning($"Cannot convert to float4: Invalid json array \"{data.ToJson()}\"");
            return float4.zero;
        }

        // { "rotation": [ 30, 225, 0 ], "translation": [ 0, 1, 0], "scale":[ 0.5, 0.5, 0.5 ] } in Json
        // -> float3x3(0, 1, 0, 0, 225, 30, 0.5, 0.5, 0.5) in Unity, with x and z values in translation
        // and scale swapped. First translation, then rotation, and scale comes last
        public static float3x3 Json2DisplayTransform(Json.JSONData data)
        {
            try
            {
                float3 t = data.Properties.ContainsKey("translation") ? Json2SwappedFloat3(data.Properties["translation"]) : float3.zero;
                float3 r = data.Properties.ContainsKey("rotation") ? Json2Float3(data.Properties["rotation"]) : float3.zero;
                float3 s = data.Properties.ContainsKey("scale") ? Json2SwappedFloat3(data.Properties["scale"]) : Vector3.one;

                return new float3x3(t, r, s);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Cannot convert to float3x3: {e.Message}");
            }
            return new float3x3(float3.zero, float3.zero, Vector3.one);
        }
    }
}