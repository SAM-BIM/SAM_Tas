// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>The kind of a TBD material layer, which decides the properties Tas uses.</summary>
    public enum TasMaterialKind
    {
        /// <summary>No material (a layer whose material SAM could not find), or one of another kind.</summary>
        Other,

        Opaque,

        Transparent,

        Gas,
    }

    /// <summary>
    /// One layer of a TBD construction as the thermal calculation sees it: the material's name, the layer's thickness and
    /// the properties of its kind, as the TBD's floats (read-only data, no COM). <see cref="Query.TasMaterialLayer(ConstructionLayer, Core.IMaterial)"/>
    /// makes the layer SAM_Tas would write for a SAM layer, so a SAM construction can be compared with a TBD's
    /// (<see cref="Query.TasMaterialLayerDifferences"/>); the inventory reads the TBD's.
    /// </summary>
    public sealed class TasMaterialLayer
    {
        public const string Conductivity = "conductivity";
        public const string SpecificHeat = "specific heat";
        public const string Density = "density";
        public const string VapourDiffusionFactor = "vapour diffusion factor";
        public const string SolarTransmittance = "solar transmittance";
        public const string LightTransmittance = "light transmittance";
        public const string ExternalSolarReflectance = "external solar reflectance";
        public const string InternalSolarReflectance = "internal solar reflectance";
        public const string ExternalLightReflectance = "external light reflectance";
        public const string InternalLightReflectance = "internal light reflectance";
        public const string ExternalEmissivity = "external emissivity";
        public const string InternalEmissivity = "internal emissivity";
        public const string Blind = "blind";
        public const string DynamicViscosity = "dynamic viscosity";
        public const string ConvectionCoefficient = "convection coefficient";

        /// <param name="name">The material's name.</param>
        /// <param name="thickness">The layer's thickness (m), as the TBD's float.</param>
        /// <param name="kind">The material's kind.</param>
        /// <param name="properties">The properties of that kind (<see cref="Properties"/>), as the TBD's floats.</param>
        public TasMaterialLayer(string name, float thickness, TasMaterialKind kind, IEnumerable<KeyValuePair<string, float>> properties = null)
        {
            Name = name;
            Thickness = thickness;
            Kind = kind;
            Properties = (properties ?? Enumerable.Empty<KeyValuePair<string, float>>()).ToList().AsReadOnly();
        }

        public string Name { get; }

        public float Thickness { get; }

        public TasMaterialKind Kind { get; }

        /// <summary>
        /// The properties Tas uses for the kind, in a fixed order: opaque — conductivity, specific heat, density, vapour
        /// diffusion factor, solar and light reflectances, emissivities; transparent — conductivity, vapour diffusion
        /// factor, solar and light transmittance, reflectances, emissivities, blind (1 or 0); gas — conductivity, specific
        /// heat, density, dynamic viscosity, convection coefficient, vapour diffusion factor. A material's own width and
        /// description are not part of it (the layer's thickness is).
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, float>> Properties { get; }
    }
}
