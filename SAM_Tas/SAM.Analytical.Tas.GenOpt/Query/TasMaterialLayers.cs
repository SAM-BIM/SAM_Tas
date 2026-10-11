// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The TBD layer SAM_Tas writes for a SAM construction layer (<c>Modify.UpdateConstruction</c> and the TBD
        /// <c>Modify.UpdateMaterial</c> of the material's kind): the layer's thickness and the material's properties, as
        /// the TBD's floats. Pure: no Tas. A layer whose material is null is <see cref="TasMaterialKind.Other"/> with no
        /// properties (SAM_Tas then writes only its name and thickness).
        /// </summary>
        public static TasMaterialLayer TasMaterialLayer(ConstructionLayer constructionLayer, IMaterial material)
        {
            if (constructionLayer == null)
            {
                return null;
            }

            float thickness = System.Convert.ToSingle(constructionLayer.Thickness);
            List<KeyValuePair<string, float>> properties = new List<KeyValuePair<string, float>>();
            if (material is TransparentMaterial transparentMaterial)
            {
                properties.Add(Property(GenOpt.TasMaterialLayer.Conductivity, transparentMaterial.ThermalConductivity));
                properties.Add(Property(GenOpt.TasMaterialLayer.VapourDiffusionFactor, transparentMaterial.GetValue<double>(Analytical.MaterialParameter.VapourDiffusionFactor)));
                properties.Add(Property(GenOpt.TasMaterialLayer.SolarTransmittance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.SolarTransmittance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.LightTransmittance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.LightTransmittance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalSolarReflectance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.ExternalSolarReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalSolarReflectance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.InternalSolarReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalLightReflectance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.ExternalLightReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalLightReflectance, transparentMaterial.GetValue<double>(TransparentMaterialParameter.InternalLightReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalEmissivity, transparentMaterial.GetValue<double>(TransparentMaterialParameter.ExternalEmissivity)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalEmissivity, transparentMaterial.GetValue<double>(TransparentMaterialParameter.InternalEmissivity)));
                properties.Add(new KeyValuePair<string, float>(GenOpt.TasMaterialLayer.Blind, transparentMaterial.GetValue<bool>(TransparentMaterialParameter.IsBlind) ? 1 : 0));
                return new TasMaterialLayer(constructionLayer.Name, thickness, TasMaterialKind.Transparent, properties);
            }

            if (material is GasMaterial gasMaterial)
            {
                properties.Add(new KeyValuePair<string, float>(GenOpt.TasMaterialLayer.Conductivity, NonNegative(gasMaterial.ThermalConductivity)));
                properties.Add(new KeyValuePair<string, float>(GenOpt.TasMaterialLayer.SpecificHeat, NonNegative(gasMaterial.SpecificHeatCapacity)));
                properties.Add(Property(GenOpt.TasMaterialLayer.Density, gasMaterial.Density));
                properties.Add(Property(GenOpt.TasMaterialLayer.DynamicViscosity, gasMaterial.DynamicViscosity));
                properties.Add(Property(GenOpt.TasMaterialLayer.ConvectionCoefficient, gasMaterial.GetValue<double>(GasMaterialParameter.HeatTransferCoefficient)));
                properties.Add(Property(GenOpt.TasMaterialLayer.VapourDiffusionFactor, gasMaterial.GetValue<double>(Analytical.MaterialParameter.VapourDiffusionFactor)));
                return new TasMaterialLayer(constructionLayer.Name, thickness, TasMaterialKind.Gas, properties);
            }

            if (material is OpaqueMaterial opaqueMaterial)
            {
                properties.Add(Property(GenOpt.TasMaterialLayer.Conductivity, opaqueMaterial.ThermalConductivity));
                properties.Add(Property(GenOpt.TasMaterialLayer.SpecificHeat, opaqueMaterial.SpecificHeatCapacity));
                properties.Add(Property(GenOpt.TasMaterialLayer.Density, opaqueMaterial.Density));
                properties.Add(Property(GenOpt.TasMaterialLayer.VapourDiffusionFactor, opaqueMaterial.GetValue<double>(Analytical.MaterialParameter.VapourDiffusionFactor)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalSolarReflectance, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.ExternalSolarReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalSolarReflectance, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.InternalSolarReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalLightReflectance, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.ExternalLightReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalLightReflectance, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.InternalLightReflectance)));
                properties.Add(Property(GenOpt.TasMaterialLayer.ExternalEmissivity, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.ExternalEmissivity)));
                properties.Add(Property(GenOpt.TasMaterialLayer.InternalEmissivity, opaqueMaterial.GetValue<double>(OpaqueMaterialParameter.InternalEmissivity)));
                return new TasMaterialLayer(constructionLayer.Name, thickness, TasMaterialKind.Opaque, properties);
            }

            return new TasMaterialLayer(constructionLayer.Name, thickness, TasMaterialKind.Other);
        }

        /// <summary>
        /// The TBD layers SAM_Tas writes for <paramref name="constructionLayers"/>, each with its material from
        /// <paramref name="materialLibrary"/> by name (layers without a name are skipped, as SAM_Tas does).
        /// </summary>
        public static List<TasMaterialLayer> TasMaterialLayers(IEnumerable<ConstructionLayer> constructionLayers, MaterialLibrary materialLibrary)
        {
            return (constructionLayers ?? Enumerable.Empty<ConstructionLayer>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => TasMaterialLayer(x, materialLibrary?.GetMaterial(x.Name)))
                .ToList();
        }

        /// <summary>
        /// How <paramref name="actual"/> differs from <paramref name="expected"/>, in words (empty when they are the same):
        /// the number of layers, and each layer's material name, kind, thickness and properties, compared as the TBD's
        /// floats (to float precision; two NaNs are the same). Equal layers mean the same thermal input to Tas, not the
        /// same material identity.
        /// </summary>
        public static List<string> TasMaterialLayerDifferences(IReadOnlyList<TasMaterialLayer> expected, IReadOnlyList<TasMaterialLayer> actual)
        {
            List<string> result = new List<string>();
            if (expected == null || actual == null)
            {
                if (expected != actual)
                {
                    result.Add(expected == null ? "no layers were expected" : "its layers are not known");
                }

                return result;
            }

            if (expected.Count != actual.Count)
            {
                result.Add(string.Format(CultureInfo.InvariantCulture, "{0} layers, not {1}", actual.Count, expected.Count));
                return result;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                TasMaterialLayer x = expected[i];
                TasMaterialLayer y = actual[i];
                string layer = string.Format(CultureInfo.InvariantCulture, "layer {0}", i + 1);
                if (!string.Equals(x.Name, y.Name, StringComparison.Ordinal))
                {
                    result.Add(layer + " is “" + y.Name + "”, not “" + x.Name + "”");
                    continue;
                }

                layer += " “" + x.Name + "”";
                if (x.Kind != y.Kind)
                {
                    result.Add(layer + " is " + KindText(y.Kind) + ", not " + KindText(x.Kind));
                    continue;
                }

                if (!Same(x.Thickness, y.Thickness))
                {
                    result.Add(layer + " is " + Text(y.Thickness) + " m thick, not " + Text(x.Thickness));
                }

                foreach (KeyValuePair<string, float> property in x.Properties)
                {
                    KeyValuePair<string, float> other = y.Properties.FirstOrDefault(p => p.Key == property.Key);
                    if (other.Key == null)
                    {
                        result.Add(layer + " has no " + property.Key);
                    }
                    else if (!Same(property.Value, other.Value))
                    {
                        result.Add(layer + " " + property.Key + " " + Text(other.Value) + ", not " + Text(property.Value));
                    }
                }

                foreach (KeyValuePair<string, float> property in y.Properties.Where(p => !x.Properties.Any(q => q.Key == p.Key)))
                {
                    result.Add(layer + " has " + property.Key + " " + Text(property.Value) + ", which it should not have");
                }
            }

            return result;
        }

        private static KeyValuePair<string, float> Property(string name, double value)
        {
            return new KeyValuePair<string, float>(name, System.Convert.ToSingle(value));
        }

        /// <summary>As SAM_Tas writes a gas's conductivity and specific heat: negative or NaN become 0.</summary>
        private static float NonNegative(double value)
        {
            float result = System.Convert.ToSingle(value);
            return result < 0 || float.IsNaN(result) ? 0 : result;
        }

        /// <summary>Equal as floats, to float precision (a value that went through another route, such as gbXML, may differ in its last bit); two NaNs are the same.</summary>
        private static bool Same(float x, float y)
        {
            if (float.IsNaN(x) || float.IsNaN(y))
            {
                return float.IsNaN(x) && float.IsNaN(y);
            }

            return System.Math.Abs(x - y) <= 1e-6f * System.Math.Max(1f, System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)));
        }

        private static string Text(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string KindText(TasMaterialKind kind)
        {
            return kind == TasMaterialKind.Other ? "a layer without a material" : "a " + kind.ToString().ToLowerInvariant() + " layer";
        }
    }
}
