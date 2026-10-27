using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Il2CppAlta.Alchemy;
using Il2CppAlta.Cooking;
using Il2CppAlta.Liquid;
using UnityEngine;

namespace CookingLib
{
    internal static class LiquidFactory
    {
        internal const uint WaterHash = 44872;

        const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        static readonly FieldInfo skinField = typeof(LiquidData).GetField("isConsumableThroughSkin", PrivateInstance);
        static readonly FieldInfo splashField = typeof(LiquidData).GetField("splashParticlesStrength", PrivateInstance);
        static readonly FieldInfo dataField = typeof(LiquidDefinition).GetField("data", PrivateInstance);

        internal static LiquidData BuildData(LiquidSpec spec, EffectKinds allowed)
        {
            LiquidVisualData look = Names.Need<LiquidVisualData>(spec.Look, "liquid look", "Liquid looks");

            List<EffectDefinitionApplication> effects = new List<EffectDefinitionApplication>();
            foreach (EffectSpec effect in spec.Effects)
            {
                EffectDefinition definition = EffectNames.Need(effect.Effect, allowed);
                effects.Add(new EffectDefinitionApplication(definition, effect.Strength, EffectSpread.Check(effect, definition)));
            }

            List<LiquidVisualChunkDefinition> chunks = new List<LiquidVisualChunkDefinition>();
            foreach (string chunk in spec.Chunks)
                chunks.Add(Names.Need<LiquidVisualChunkDefinition>(chunk, "chunk", "Chunks"));

            LiquidData data = new LiquidData(look, effects.ToArray(), new CookableItem[0], chunks.ToArray(), spec.Color);
            skinField.SetValue(data, spec.ConsumableThroughSkin);
            splashField.SetValue(data, spec.SplashStrength);
            return data;
        }

        internal static LiquidDefinition BuildDefinition(string name, LiquidData data, uint hash)
        {
            LiquidDefinition definition = ScriptableObject.CreateInstance<LiquidDefinition>();
            definition.name = name;
            definition.hideFlags = HideFlags.DontUnloadUnusedAsset;
            dataField.SetValue(definition, data);
            Registry.SetHash(definition, hash);
            return definition;
        }

        internal static string Fingerprint(LiquidData data)
        {
            StringBuilder key = new StringBuilder();
            key.Append(data.LiquidVisual == null ? 0 : data.LiquidVisual.Hash).Append('|');

            Color c = data.ColorTint;
            key.Append(Quantize(c.r)).Append(',').Append(Quantize(c.g)).Append(',').Append(Quantize(c.b)).Append(',').Append(Quantize(c.a)).Append('|');
            key.Append(data.IsConsumableThroughSkin).Append('|');

            List<string> effects = new List<string>();
            if (data.Effects != null)
            {
                foreach (EffectDefinitionApplication effect in data.Effects)
                    effects.Add((effect.EffectToModify == null ? 0 : effect.EffectToModify.Hash) + ":" + Mathf.RoundToInt(effect.StrengthMultiplier * 1000f));
            }
            effects.Sort(StringComparer.Ordinal);
            key.Append(string.Join(";", effects.ToArray())).Append('|');

            if (data.ChunkVisuals != null)
            {
                foreach (LiquidVisualChunkDefinition chunk in data.ChunkVisuals)
                    key.Append(chunk == null ? 0 : chunk.Hash).Append(';');
            }
            return key.ToString();
        }

        internal static bool IsValidColor(Color c) =>
            c.r >= 0f && c.g >= 0f && c.b >= 0f && c.a >= 0f && c.r <= 1f && c.g <= 1f && c.b <= 1f && c.a <= 1f;

        internal static bool IsWithin100(float value) => !float.IsNaN(value) && Mathf.Abs(value) <= 100f;

        static int Quantize(float channel) => Mathf.RoundToInt(Mathf.Clamp01(channel) * 1023f);
    }
}
