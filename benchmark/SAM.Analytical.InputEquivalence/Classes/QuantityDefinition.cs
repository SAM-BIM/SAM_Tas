// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// One compared S07 section 5 quantity: its identifier, unit, tolerance and whether it is hour-aligned and
    /// gated. <see cref="Gated"/> follows the <b>proposed</b> OD-G2 definition (S07 section 6.1), which the owner has
    /// not approved; it only changes which owner decision a rank 3 item names, never whether it can pass.
    /// </summary>
    public sealed class QuantityDefinition
    {
        public QuantityDefinition(string id, string name, string unit, ToleranceRule tolerance, bool gated, string notGatedReason = null, bool hourlyAligned = false)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A quantity needs an identifier.", nameof(id));
            }

            Id = id;
            Name = name;
            Unit = unit;
            Tolerance = tolerance ?? throw new ArgumentNullException(nameof(tolerance));
            Gated = gated;
            NotGatedReason = notGatedReason;
            HourlyAligned = hourlyAligned;
        }

        public string Id { get; }
        public string Name { get; }
        public string Unit { get; }
        public ToleranceRule Tolerance { get; }

        /// <summary>Proposed OD-G2 definition: every quantity with a tolerance except the ones S07 marks informational, not gated or outside S07.</summary>
        public bool Gated { get; }

        public string NotGatedReason { get; }

        /// <summary>The quantity is an hourly value, so S07 section 7 caps it at NOT RUN until the alignment is verified.</summary>
        public bool HourlyAligned { get; }

        public override string ToString()
        {
            return Id + " " + Name + " [" + Unit + "] " + Tolerance;
        }
    }

    /// <summary>
    /// The Tas-applicable S07 section 5 quantities with the merged tolerances, as data. Every number is transcribed
    /// from S07 and locked by a test; none is relaxed. Not catalogued because S07 gives them no tolerance:
    /// engine-reported U and g, DNI, exhaust (8c), IZAM/TPD delivery and air-panel mixing (8e).
    /// </summary>
    public static class QuantityCatalog
    {
        private static readonly Dictionary<string, QuantityDefinition> definitions = Build();

        public static IReadOnlyCollection<QuantityDefinition> All => definitions.Values.ToList();

        public static QuantityDefinition Get(string id)
        {
            if (id == null || !definitions.TryGetValue(id, out QuantityDefinition result))
            {
                throw new KeyNotFoundException("Unknown S07 quantity '" + id + "'.");
            }

            return result;
        }

        public static bool TryGet(string id, out QuantityDefinition quantityDefinition)
        {
            quantityDefinition = null;
            return id != null && definitions.TryGetValue(id, out quantityDefinition);
        }

        private static Dictionary<string, QuantityDefinition> Build()
        {
            List<QuantityDefinition> list = new List<QuantityDefinition>
            {
                // 1a, 1b, 2: areas and volume
                new QuantityDefinition("1a", "Zone floor area", "m2", ToleranceRule.Create(0.005, 0.01, "m2"), true),
                new QuantityDefinition("1b", "Surface gross area; pane and frame area", "m2", ToleranceRule.Create(0.005, 0.01, "m2"), true),
                new QuantityDefinition("2", "Zone volume", "m3", ToleranceRule.Create(0.005, 0.01, "m3"), true),

                // 3: constructions and materials
                new QuantityDefinition("3a", "Opaque layer thickness, conductivity, density, specific heat, reflectance, emissivity", "unit", ToleranceRule.Create(1e-5, 1e-6, "unit"), true),
                new QuantityDefinition("3b", "Derived U, opaque", "W/m2K", ToleranceRule.Create(0.01, 0.005, "W/m2K"), true),
                new QuantityDefinition("3b.ground", "Derived U, ground or adiabatic element", "W/m2K", ToleranceRule.Create(0.01, 0.005, "W/m2K"), false, "film convention F-OPQ: ground and adiabatic elements are not gated"),
                new QuantityDefinition("3c.layer", "Glazing layers (tau, rho, epsilon, gas thickness)", "unit", ToleranceRule.Create(1e-5, 1e-6, "unit"), true),
                new QuantityDefinition("3c.U", "Derived U, glazing", "W/m2K", ToleranceRule.Create(0.01, 0.005, "W/m2K"), true),
                new QuantityDefinition("3c.g", "Derived g, glazing", "-", ToleranceRule.Create(0.01, 0.001, "-"), false, "derived g is informational (S07 section 6.1)"),

                // 4: gains
                new QuantityDefinition("4a.density", "Gain density (occupancy S/L, lighting, equipment S/L)", "W/m2", ToleranceRule.Create(0.001, 0.001, "W/m2"), true),
                new QuantityDefinition("4a.people", "People density", "p/m2", ToleranceRule.Create(0.001, 1e-5, "p/m2"), true),
                new QuantityDefinition("4b", "Zone gain total", "W", ToleranceRule.Create(0.005, 0.1, "W"), true),
                new QuantityDefinition("4c", "Radiant and visible fraction", "-", ToleranceRule.Create(null, 0.001, "-"), true),

                // 5, 6: setpoints and schedules
                new QuantityDefinition("5.temperature", "Setpoint (hourly)", "K", ToleranceRule.Create(null, 0.01, "K"), true, null, true),
                new QuantityDefinition("5.humidity", "Humidistat (hourly)", "%RH", ToleranceRule.Create(null, 0.1, "%RH"), true, null, true),
                new QuantityDefinition("6.hourly", "Schedule hourly value", "-", ToleranceRule.Create(null, 1e-4, "-"), true, null, true),
                new QuantityDefinition("6.eflh", "Equivalent full-load hours", "h", ToleranceRule.Create(0.001, null, "h"), true),

                // 7: infiltration
                new QuantityDefinition("7a", "Infiltration", "1/h", ToleranceRule.Create(0.001, 1e-4, "1/h"), true),
                new QuantityDefinition("7b", "Infiltration flow", "m3/s", ToleranceRule.Create(0.005, 1e-5, "m3/s"), true),

                // 8: ventilation and openings
                new QuantityDefinition("8a", "Ventilation requirement (four bases)", "unit", ToleranceRule.Create(0.001, 1e-6, "unit"), true),
                new QuantityDefinition("8b", "Delivered thermal outdoor air (hourly)", "m3/s", ToleranceRule.Create(0.005, 1e-5, "m3/s"), true, null, true),
                new QuantityDefinition("8d", "Window opening: discharge coefficient, factor", "-", ToleranceRule.Create(0.001, 0.001, "-"), true),

                // 9, 10: weather and design days (Tas rows)
                new QuantityDefinition("9", "Weather (Tas, float32 identity)", "unit", ToleranceRule.Create(1e-5, null, "unit"), true),
                new QuantityDefinition("9.ground", "Ground temperature", "C", ToleranceRule.Create(1e-5, null, "C"), false, "ground temperature is not gated (S07 section 5 row 9)"),
                new QuantityDefinition("10", "Design-day hourly weather (Tas)", "unit", ToleranceRule.Create(1e-5, null, "unit"), true),
                new QuantityDefinition("10.yearDay", "Design-day year day", "day", ToleranceRule.Exact("day"), true)
            };

            return list.ToDictionary(x => x.Id, StringComparer.Ordinal);
        }
    }
}
