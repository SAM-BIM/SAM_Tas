// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR9 safety review: a SAM construction layer described as SAM_Tas writes it into a TBD, compared with the TBD's.
    /// The "TBD" layers below are the values the licensed inventory read from the PR9 S2 Energy Simulation TBD
    /// (SAM_zoningAM.sam, "Windows: SIM_EXT_GLZ_SKY -pane" and its frame), so the SAM side must describe that model's
    /// materials exactly as they arrived there.
    /// </summary>
    [TestFixture]
    public class TasMaterialLayerTests
    {
        [Test]
        public void The_models_glazing_layers_are_described_as_the_Energy_Simulation_TBD_holds_them()
        {
            MaterialLibrary materialLibrary = Materials();
            List<ConstructionLayer> pane = new List<ConstructionLayer>
            {
                new ConstructionLayer(Inner, 0.006),
                new ConstructionLayer(Argon, 0.012),
                new ConstructionLayer(RoofLit, 0.006),
            };

            List<TasMaterialLayer> sam = Query.TasMaterialLayers(pane, materialLibrary);

            Assert.That(Query.TasMaterialLayerDifferences(sam, TbdPane()), Is.Empty);
            Assert.That(sam.Select(x => x.Kind), Is.EqualTo(new[] { TasMaterialKind.Transparent, TasMaterialKind.Gas, TasMaterialKind.Transparent }));

            // The frame: the TBD's material width (0.05, the layer) is not SAM's default thickness (0.001); only the
            // layer's thickness counts.
            List<TasMaterialLayer> frame = Query.TasMaterialLayers(new[] { new ConstructionLayer(Frame, 0.05) }, materialLibrary);
            Assert.That(Query.TasMaterialLayerDifferences(frame, TbdFrame()), Is.Empty);
        }

        [Test]
        public void A_layer_that_differs_in_material_thickness_or_a_property_is_reported()
        {
            MaterialLibrary materialLibrary = Materials();
            List<ConstructionLayer> pane = new List<ConstructionLayer> { new ConstructionLayer(Inner, 0.006), new ConstructionLayer(Argon, 0.012), new ConstructionLayer(RoofLit, 0.006) };

            // Same name, other physics: the outer pane's solar transmittance changed.
            MaterialLibrary changed = Materials(rooflitSolarTransmittance: 0.6);
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(pane, changed), TbdPane()), Is.EqualTo(new[] { "layer 3 “" + RoofLit + "” solar transmittance 0.58, not 0.6" }));

            // A thicker cavity, another material, a layer fewer, a material the model does not have.
            List<ConstructionLayer> thicker = new List<ConstructionLayer> { pane[0], new ConstructionLayer(Argon, 0.016), pane[2] };
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(thicker, materialLibrary), TbdPane()), Is.EqualTo(new[] { "layer 2 “" + Argon + "” is 0.012 m thick, not 0.016" }));
            List<ConstructionLayer> other = new List<ConstructionLayer> { pane[0], pane[1], new ConstructionLayer(Inner, 0.006) };
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(other, materialLibrary), TbdPane()), Is.EqualTo(new[] { "layer 3 is “" + RoofLit + "”, not “" + Inner + "”" }));
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(pane.Take(2), materialLibrary), TbdPane()), Is.EqualTo(new[] { "3 layers, not 2" }));
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(pane, new MaterialLibrary("empty")), TbdPane()).First(), Is.EqualTo("layer 1 “" + Inner + "” is a transparent layer, not a layer without a material"));

            // Two NaNs are the same (the argon's dynamic viscosity is NaN on both sides); a NaN and a number are not.
            List<TasMaterialLayer> tbd = TbdPane();
            tbd[1] = new TasMaterialLayer(Argon, 0.012f, TasMaterialKind.Gas, tbd[1].Properties.Select(x => x.Key == TasMaterialLayer.DynamicViscosity ? new KeyValuePair<string, float>(x.Key, 1e-5f) : x));
            Assert.That(Query.TasMaterialLayerDifferences(Query.TasMaterialLayers(pane, materialLibrary), tbd), Is.EqualTo(new[] { "layer 2 “" + Argon + "” dynamic viscosity 1E-05, not NaN" }));

            // A property on one side only is a difference either way round.
            List<TasMaterialLayer> fewer = TbdPane();
            fewer[0] = new TasMaterialLayer(Inner, 0.006f, TasMaterialKind.Transparent, fewer[0].Properties.Where(x => x.Key != TasMaterialLayer.InternalEmissivity));
            Assert.That(Query.TasMaterialLayerDifferences(fewer, TbdPane()), Is.EqualTo(new[] { "layer 1 “" + Inner + "” has internal emissivity 0.84, which it should not have" }));
            Assert.That(Query.TasMaterialLayerDifferences(TbdPane(), fewer), Is.EqualTo(new[] { "layer 1 “" + Inner + "” has no internal emissivity" }));
        }

        private const string Inner = "_Glazing Inner Pane_6mm_g0.78_Lt0.89";
        private const string Argon = "Ag90UP_Argon__12mm_1.403W/m2K";
        private const string RoofLit = "_Roof Lit Glazing Outer Pane_6mm_g0.58_Lt0.67";
        private const string Frame = "C00_Frame Notional building_7800kg/m3_0.176W/mK";

        /// <summary>The model's materials (SAM_zoningAM.sam), as SAM holds them.</summary>
        private static MaterialLibrary Materials(double rooflitSolarTransmittance = 0.58)
        {
            MaterialLibrary result = new MaterialLibrary("SAM_zoningAM");
            result.Add(Analytical.Create.TransparentMaterial(Inner, string.Empty, Inner, "PartL", 1, 0.006, 9999, 0.78, 0.89, 0.07, 0.07, 0.08, 0.08, 0.84, 0.84, false));
            result.Add(Analytical.Create.GasMaterial(Argon, string.Empty, Argon, "PartL", 0.01622, 521.9, 1.782, double.NaN, 0.012, 1, 1.403));
            result.Add(Analytical.Create.TransparentMaterial(RoofLit, string.Empty, RoofLit, "PartL", 1, 0.006, 9999, rooflitSolarTransmittance, 0.67, 0.19, 0.23, 0.25, 0.28, 0.84, 0.21, false));
            result.Add(Analytical.Create.OpaqueMaterial(Frame, string.Empty, Frame, "Frame - notional building PartL", 0.176, 450, 7800, 0.001, 9999, 1, 1, 1, 1, 0.85, 0.85, false));
            return result;
        }

        /// <summary>"Windows: SIM_EXT_GLZ_SKY -pane" as the licensed inventory reads it from the S2 TBD.</summary>
        private static List<TasMaterialLayer> TbdPane()
        {
            return new List<TasMaterialLayer>
            {
                Transparent(Inner, 0.78f, 0.89f, 0.07f, 0.07f, 0.08f, 0.08f, 0.84f, 0.84f),
                new TasMaterialLayer(Argon, 0.012f, TasMaterialKind.Gas, new Dictionary<string, float>
                {
                    { TasMaterialLayer.Conductivity, 0.01622f },
                    { TasMaterialLayer.SpecificHeat, 521.9f },
                    { TasMaterialLayer.Density, 1.782f },
                    { TasMaterialLayer.DynamicViscosity, float.NaN },
                    { TasMaterialLayer.ConvectionCoefficient, 1.403f },
                    { TasMaterialLayer.VapourDiffusionFactor, 1f },
                }),
                Transparent(RoofLit, 0.58f, 0.67f, 0.19f, 0.23f, 0.25f, 0.28f, 0.84f, 0.21f),
            };
        }

        /// <summary>The frame element's construction ("SIM_EXT_GLZ_SKY -frame", from the gbXML import) in the S2 TBD.</summary>
        private static List<TasMaterialLayer> TbdFrame()
        {
            return new List<TasMaterialLayer>
            {
                new TasMaterialLayer(Frame, 0.05f, TasMaterialKind.Opaque, new Dictionary<string, float>
                {
                    { TasMaterialLayer.Conductivity, 0.176f },
                    { TasMaterialLayer.SpecificHeat, 450f },
                    { TasMaterialLayer.Density, 7800f },
                    { TasMaterialLayer.VapourDiffusionFactor, 9999f },
                    { TasMaterialLayer.ExternalSolarReflectance, 1f },
                    { TasMaterialLayer.InternalSolarReflectance, 1f },
                    { TasMaterialLayer.ExternalLightReflectance, 1f },
                    { TasMaterialLayer.InternalLightReflectance, 1f },
                    { TasMaterialLayer.ExternalEmissivity, 0.85f },
                    { TasMaterialLayer.InternalEmissivity, 0.85f },
                }),
            };
        }

        private static TasMaterialLayer Transparent(string name, float solar, float light, float solarExternal, float solarInternal, float lightExternal, float lightInternal, float emissivityExternal, float emissivityInternal)
        {
            return new TasMaterialLayer(name, 0.006f, TasMaterialKind.Transparent, new Dictionary<string, float>
            {
                { TasMaterialLayer.Conductivity, 1f },
                { TasMaterialLayer.VapourDiffusionFactor, 9999f },
                { TasMaterialLayer.SolarTransmittance, solar },
                { TasMaterialLayer.LightTransmittance, light },
                { TasMaterialLayer.ExternalSolarReflectance, solarExternal },
                { TasMaterialLayer.InternalSolarReflectance, solarInternal },
                { TasMaterialLayer.ExternalLightReflectance, lightExternal },
                { TasMaterialLayer.InternalLightReflectance, lightInternal },
                { TasMaterialLayer.ExternalEmissivity, emissivityExternal },
                { TasMaterialLayer.InternalEmissivity, emissivityInternal },
                { TasMaterialLayer.Blind, 0f },
            });
        }
    }
}
