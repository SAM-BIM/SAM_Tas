// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>Suggested range of a zone heating setpoint (PR7a proposal), widened to include the current value.</summary>
        public const double HeatingSetpointMinimum = 16, HeatingSetpointMaximum = 24;

        /// <summary>Suggested range of a zone cooling setpoint (PR7a proposal), widened to include the current value.</summary>
        public const double CoolingSetpointMinimum = 21, CoolingSetpointMaximum = 28;

        /// <summary>
        /// The suffix of the heating-design-day internal condition SAM writes for every space ("&lt;space&gt; - HDD"). It is
        /// not offered: the annual simulation does not use the design day.
        /// </summary>
        public const string HeatingDesignDaySuffix = " - HDD";

        /// <summary>
        /// The catalogue of a Tas project folder for the "tas-model" engine: reads the folder's Tas files
        /// (<see cref="TasModelInventory(string)"/>, licensed Tas, COM) and builds the catalogue from them
        /// (<see cref="TasModelCatalogue(TasModelInventory, IEnumerable{TasGlazingSystem}, TasGlazingFilter)"/>).
        /// </summary>
        /// <param name="projectFolder">The Tas project folder (TBD, TSD, TPD). Only read.</param>
        /// <param name="glazingPool">The glazing systems a glazing choice may offer; null or empty for no glazing choice.</param>
        /// <param name="glazingFilter">Which pool systems become options; null for the defaults.</param>
        public static OptimisationCatalogue TasModelCatalogue(string projectFolder, IEnumerable<TasGlazingSystem> glazingPool = null, TasGlazingFilter glazingFilter = null)
        {
            return TasModelCatalogue(TasModelInventory(projectFolder), glazingPool, glazingFilter);
        }

        /// <summary>
        /// The catalogue of what a Tas model offers to the "tas-model" engine, with current values, units, suggested
        /// ranges and, for a glazing choice, its options. Only what the engine can run is listed, so the window and the AI
        /// text never offer anything else:
        /// <list type="bullet">
        /// <item>Zone heating / cooling setpoint per internal condition (owner decision 1 of PR7a), when the thermostat
        /// profile is a value or 24-hour profile with factor 1; heating not when it says "no heating" (≤ −50 °C), cooling not
        /// when it says "no cooling" (≥ 150 °C); the heating-design-day conditions ("… - HDD") are not listed.</item>
        /// <item>A glazing system choice per glazing construction used by building elements, when the pool gives at least
        /// one option besides the current glazing (<see cref="TasGlazingOptions"/>). The description lists every option
        /// with g, Ug, light and source.</item>
        /// <item>A controller setpoint per TPD controller (°C for a temperature sensor, otherwise no unit); no range is
        /// suggested (it depends on the controller).</item>
        /// <item>Annual heating and cooling demand and overheating hours (threshold 28 °C) when there is a TBD or a TSD;
        /// annual plant energy and CO2 when the plant can be simulated; annual plant cost only when the TPD reports it
        /// in "£" (GBP).</item>
        /// </list>
        /// </summary>
        public static OptimisationCatalogue TasModelCatalogue(TasModelInventory tasModelInventory, IEnumerable<TasGlazingSystem> glazingPool = null, TasGlazingFilter glazingFilter = null)
        {
            if (tasModelInventory == null)
            {
                throw new ArgumentNullException(nameof(tasModelInventory));
            }

            List<OptimisationCatalogueEntry> variables = new List<OptimisationCatalogueEntry>();
            List<OptimisationCatalogueEntry> outputs = new List<OptimisationCatalogueEntry>();
            List<TasGlazingSystem> pool = (glazingPool ?? Enumerable.Empty<TasGlazingSystem>()).Where(x => x != null).ToList();

            if (tasModelInventory.HasBuilding)
            {
                foreach (TasInternalConditionInfo internalCondition in tasModelInventory.InternalConditions)
                {
                    if (string.IsNullOrEmpty(internalCondition.Name) || internalCondition.Name.EndsWith(HeatingDesignDaySuffix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    double? cooling = CoolingSetpoint(internalCondition);
                    double? heating = HeatingSetpoint(internalCondition);
                    if (heating.HasValue)
                    {
                        double maximum = cooling.HasValue ? System.Math.Min(HeatingSetpointMaximum, cooling.Value) : HeatingSetpointMaximum;
                        variables.Add(new OptimisationCatalogueEntry(
                            internalCondition.Name.Trim() + " heating setpoint",
                            new OptimisationTarget(TasModelKind.HeatingSetpoint, Reference(TasModelKind.InternalConditionKey, internalCondition.Name)),
                            SetpointDescription("heating", "lower limit", internalCondition, internalCondition.Heating),
                            OptimisationQuantity.Temperature,
                            "°C",
                            heating.Value,
                            System.Math.Min(HeatingSetpointMinimum, heating.Value),
                            System.Math.Max(maximum, heating.Value)));
                    }

                    if (cooling.HasValue)
                    {
                        variables.Add(new OptimisationCatalogueEntry(
                            internalCondition.Name.Trim() + " cooling setpoint",
                            new OptimisationTarget(TasModelKind.CoolingSetpoint, Reference(TasModelKind.InternalConditionKey, internalCondition.Name)),
                            SetpointDescription("cooling", "upper limit", internalCondition, internalCondition.Cooling),
                            OptimisationQuantity.Temperature,
                            "°C",
                            cooling.Value,
                            System.Math.Min(CoolingSetpointMinimum, cooling.Value),
                            System.Math.Max(CoolingSetpointMaximum, cooling.Value)));
                    }
                }

                foreach (TasGlazingConstructionInfo glazingConstruction in tasModelInventory.GlazingConstructions)
                {
                    List<TasGlazingOption> options = TasGlazingOptions(glazingConstruction, pool, glazingFilter);
                    if (options.Count < 2)
                    {
                        continue;
                    }

                    variables.Add(new OptimisationCatalogueEntry(
                        "Glazing system (" + glazingConstruction.Name.Trim() + ")",
                        new OptimisationTarget(TasModelKind.GlazingChoice, Reference(TasModelKind.GlazingConstructionKey, glazingConstruction.Name)),
                        string.Format(CultureInfo.InvariantCulture, "Glazing of the {0} building elements using “{1}”, chosen from real glazing systems in g order (frames kept). Options: {2}",
                            glazingConstruction.Elements.Count, glazingConstruction.Name, string.Join("; ", options.Select(x => x.Summary))),
                        OptimisationQuantity.Unspecified,
                        null,
                        1,
                        1,
                        options.Count,
                        options.Select(x => x.Text)));
                }
            }

            if (tasModelInventory.HasPlant)
            {
                foreach (TasPlantRoomInfo plantRoom in tasModelInventory.PlantRooms)
                {
                    foreach (TasPlantControllerInfo controller in plantRoom.Controllers)
                    {
                        bool temperature = controller.IsTemperatureSensor;
                        variables.Add(new OptimisationCatalogueEntry(
                            controller.Name.Trim() + " setpoint (" + plantRoom.Name.Trim() + ")",
                            new OptimisationTarget(TasModelKind.ControllerSetpoint, Reference(TasModelKind.PlantRoomKey, plantRoom.Name, TasModelKind.ControllerKey, controller.Name)),
                            string.Format(CultureInfo.InvariantCulture, "Setpoint of controller “{0}” in plant room “{1}” ({2})", controller.Name, plantRoom.Name, controller.SensorType),
                            temperature ? OptimisationQuantity.Temperature : OptimisationQuantity.Unspecified,
                            temperature ? "°C" : null,
                            Finite(controller.Setpoint) ? controller.Setpoint : (double?)null));
                    }
                }
            }

            if (tasModelInventory.HasBuildingResults)
            {
                outputs.Add(new OptimisationCatalogueEntry("Annual heating demand", new OptimisationMeasure(TasModelKind.AnnualHeatingDemand), "Annual building heating demand (TSD heating profile, summed)", OptimisationQuantity.Energy, "kWh", tasModelInventory.HeatingDemand));
                outputs.Add(new OptimisationCatalogueEntry("Annual cooling demand", new OptimisationMeasure(TasModelKind.AnnualCoolingDemand), "Annual building cooling demand (TSD cooling profile, summed)", OptimisationQuantity.Energy, "kWh", tasModelInventory.CoolingDemand));
                outputs.Add(new OptimisationCatalogueEntry(
                    "Overheating hours",
                    new OptimisationMeasure(TasModelKind.OverheatingHours, null, new Dictionary<string, double> { { TasModelKind.ThresholdParameter, TasModelKind.DefaultThreshold } }),
                    "Occupied hours with the resultant temperature above the threshold, in the worst occupied zone, over the year (a ranking measure, not a TM59/TM52 verdict)",
                    OptimisationQuantity.Time,
                    "h"));
            }

            if (tasModelInventory.HasPlant)
            {
                outputs.Add(new OptimisationCatalogueEntry("Annual plant energy", new OptimisationMeasure(TasModelKind.AnnualPlantEnergy), "Annual plant energy consumption, every fuel and category (TPD annual results)", OptimisationQuantity.Energy, "kWh", tasModelInventory.PlantEnergy));
                if (tasModelInventory.CostInPounds)
                {
                    outputs.Add(new OptimisationCatalogueEntry("Annual plant cost", new OptimisationMeasure(TasModelKind.AnnualPlantCost), "Annual plant cost, every fuel source (TPD annual results)", OptimisationQuantity.Currency, "GBP", tasModelInventory.PlantCost));
                }

                outputs.Add(new OptimisationCatalogueEntry("Annual plant CO2", new OptimisationMeasure(TasModelKind.AnnualPlantCO2), "Annual plant CO2 emission, every category (TPD annual results)", OptimisationQuantity.Carbon, "kgCO2e", tasModelInventory.PlantCO2));
            }

            return new OptimisationCatalogue(variables, outputs, "read from the Tas model");
        }

        /// <summary>The current heating setpoint when it can be changed: supported profile, not "no heating" (≤ −50 °C).</summary>
        internal static double? HeatingSetpoint(TasInternalConditionInfo internalCondition)
        {
            TasSetpointProfile profile = internalCondition?.Heating;
            return profile != null && profile.Supported && profile.Setpoint.Value > -50 ? profile.Setpoint : null;
        }

        /// <summary>The current cooling setpoint when it can be changed: supported profile, not "no cooling" (≥ 150 °C).</summary>
        internal static double? CoolingSetpoint(TasInternalConditionInfo internalCondition)
        {
            TasSetpointProfile profile = internalCondition?.Cooling;
            return profile != null && profile.Supported && profile.Setpoint.Value < 150 ? profile.Setpoint : null;
        }

        private static string SetpointDescription(string what, string limit, TasInternalConditionInfo internalCondition, TasSetpointProfile profile)
        {
            string shape = profile.Type == TasSetpointProfileType.Value
                ? "value profile; its setback and schedule are kept"
                : string.Format(CultureInfo.InvariantCulture, "24-hour profile; the {0} hours at the setpoint change, the others are kept", profile.ChangedHours);
            return string.Format(CultureInfo.InvariantCulture, "Zone {0} setpoint (thermostat {1}) of internal condition “{2}”{3}, {4} zones ({5})",
                what, limit, internalCondition.Name, string.IsNullOrWhiteSpace(internalCondition.Description) ? string.Empty : " (" + internalCondition.Description.Trim() + ")", internalCondition.ZoneCount, shape);
        }

        private static Dictionary<string, string> Reference(params string[] keysAndValues)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < keysAndValues.Length; i += 2)
            {
                result[keysAndValues[i]] = keysAndValues[i + 1];
            }

            return result;
        }
    }
}
