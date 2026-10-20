// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>Direction of heat flow through an opaque element; selects the internal film Rsi of ISO 6946 Table 7. Classification from geometry is not part of PR-A.</summary>
    public enum HeatFlowDirection
    {
        Horizontal,
        Upward,
        Downward
    }

    /// <summary>The element context of the F-OPQ film convention.</summary>
    public enum ElementKind
    {
        /// <summary>Internal film Rsi on the inside, Rse on the outside.</summary>
        External,

        /// <summary>Internal elements: Rsi on both sides.</summary>
        Internal,

        /// <summary>Layer resistance plus Rsi only; not gated.</summary>
        GroundOrAdiabatic
    }

    /// <summary>The thermal resistance of one layer, in m2K/W. Built from raw layer data; an invalid layer is never silently zero.</summary>
    public sealed class LayerResistance
    {
        private LayerResistance(double value) { Value = value; }

        public double Value { get; }

        public bool IsValid => !double.IsNaN(Value) && !double.IsInfinity(Value) && Value > 0;

        /// <summary>A solid layer: thickness (m) / conductivity (W/mK). A non-positive or NaN input gives an invalid layer.</summary>
        public static LayerResistance Solid(double thickness, double conductivity)
        {
            if (double.IsNaN(thickness) || double.IsNaN(conductivity) || thickness <= 0 || conductivity <= 0 || double.IsInfinity(thickness) || double.IsInfinity(conductivity))
            {
                return new LayerResistance(double.NaN);
            }

            return new LayerResistance(thickness / conductivity);
        }

        /// <summary>A layer whose resistance the caller states (for example a gas gap). S07 gives no rule for deriving it, so none is invented here.</summary>
        public static LayerResistance Fixed(double resistance)
        {
            return new LayerResistance(resistance);
        }
    }

    /// <summary>
    /// The film convention and the U-value recomputation of S07 3b/3c. The constants are the owner-approved ones
    /// (S07 "Film convention"); the engine-reported U and g stay informational and are not computed here.
    /// </summary>
    public static partial class Query
    {
        /// <summary>F-OPQ (ISO 6946:2017 Table 7): external surface resistance, m2K/W.</summary>
        public const double OpaqueRse = 0.04;

        /// <summary>F-OPQ: internal surface resistance, horizontal heat flow (walls).</summary>
        public const double OpaqueRsiHorizontal = 0.13;

        /// <summary>F-OPQ: internal surface resistance, upward heat flow (roofs, ceilings).</summary>
        public const double OpaqueRsiUpward = 0.10;

        /// <summary>F-OPQ: internal surface resistance, downward heat flow (floors).</summary>
        public const double OpaqueRsiDownward = 0.17;

        /// <summary>F-GLZ-U (EN 673): internal surface coefficient, W/m2K.</summary>
        public const double GlazingHi = 7.7;

        /// <summary>F-GLZ-U (EN 673): external surface coefficient, W/m2K.</summary>
        public const double GlazingHe = 25;

        /// <summary>F-GLZ-g (EN 410, as <c>SA/Classes/Glazing.cs</c>): external surface coefficient, W/m2K. Reserved for the informational derived g.</summary>
        public const double GlazingGHe = 23;

        /// <summary>F-GLZ-g (EN 410): internal surface coefficient, W/m2K.</summary>
        public const double GlazingGHi = 8;

        /// <summary>S07 3b: the opaque U-value, 1 / (film + sum of layer resistances + film).</summary>
        public static ExpectedValue ExpectedOpaqueU(IEnumerable<LayerResistance> layers, HeatFlowDirection direction, ElementKind kind)
        {
            double layerSum = LayerSum(layers, out string problem);
            if (problem != null)
            {
                return ExpectedValue.Invalid(problem);
            }

            double rsi = direction == HeatFlowDirection.Upward ? OpaqueRsiUpward : direction == HeatFlowDirection.Downward ? OpaqueRsiDownward : OpaqueRsiHorizontal;

            double total;
            switch (kind)
            {
                case ElementKind.Internal:
                    total = rsi + layerSum + rsi;
                    break;

                case ElementKind.GroundOrAdiabatic:
                    total = rsi + layerSum;
                    break;

                default:
                    total = rsi + layerSum + OpaqueRse;
                    break;
            }

            return ExpectedValue.FromValue(1 / total);
        }

        /// <summary>S07 3c: the glazing U-value with the EN 673 films, 1 / (1/hi + sum of layer resistances + 1/he).</summary>
        public static ExpectedValue ExpectedGlazingU(IEnumerable<LayerResistance> layers)
        {
            double layerSum = LayerSum(layers, out string problem);
            if (problem != null)
            {
                return ExpectedValue.Invalid(problem);
            }

            return ExpectedValue.FromValue(1 / (1 / GlazingHi + layerSum + 1 / GlazingHe));
        }

        private static double LayerSum(IEnumerable<LayerResistance> layers, out string problem)
        {
            problem = null;
            List<LayerResistance> list = layers?.ToList();
            if (list == null || list.Count == 0)
            {
                problem = "the construction has no layers";
                return double.NaN;
            }

            double sum = 0;
            foreach (LayerResistance layer in list)
            {
                if (layer == null || !layer.IsValid)
                {
                    problem = "a layer has no valid resistance";
                    return double.NaN;
                }

                sum += layer.Value;
            }

            return sum;
        }
    }
}
