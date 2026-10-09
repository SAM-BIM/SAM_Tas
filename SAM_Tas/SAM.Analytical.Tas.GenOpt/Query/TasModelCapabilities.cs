// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The engine id an Optimisation Definition names in <c>model.engine</c> to run on Tas with bindings: SAM_Tas
        /// changes and reads the model itself through a generated TasGenExecute script (<see cref="Create.TasScript"/>).
        /// </summary>
        public const string TasModelEngine = "tas-model";

        /// <summary>
        /// Holds the shared instance in its own class, as <see cref="TasOptimisationCapabilities"/> does, so that no other
        /// <see cref="Query"/> member loads SAM.Core.Optimisation.dll.
        /// </summary>
        private static class TasModelCapabilitiesHolder
        {
            internal static readonly IOptimisationCapabilities Value = Build();

            private static IOptimisationCapabilities Build()
            {
                OptimisationReferenceKey internalCondition = new OptimisationReferenceKey(TasModelKind.InternalConditionKey, "internal condition");
                OptimisationReferenceKey glazingConstruction = new OptimisationReferenceKey(TasModelKind.GlazingConstructionKey, "glazing construction");
                OptimisationReferenceKey plantRoom = new OptimisationReferenceKey(TasModelKind.PlantRoomKey, "plant room");
                OptimisationReferenceKey controller = new OptimisationReferenceKey(TasModelKind.ControllerKey, "controller");

                return new OptimisationCapabilities(
                    TasModelEngine,
                    "Tas",
                    new[]
                    {
                        new OptimisationAlgorithmCapability(OptimisationAlgorithm.GoldenSection, 1, 1),
                        new OptimisationAlgorithmCapability(OptimisationAlgorithm.HookeJeeves, 1, null),
                        new OptimisationAlgorithmCapability(OptimisationAlgorithm.TryEveryOption, 1, 1),
                    },
                    new[] { ObjectiveSense.Minimise },
                    new[] { DesignVariableType.Continuous, DesignVariableType.Discrete },
                    false,
                    new[]
                    {
                        new OptimisationBindingCapability(TasModelKind.HeatingSetpoint, "Zone heating setpoint", OptimisationQuantity.Temperature, "°C", new[] { internalCondition }),
                        new OptimisationBindingCapability(TasModelKind.CoolingSetpoint, "Zone cooling setpoint", OptimisationQuantity.Temperature, "°C", new[] { internalCondition }),
                        new OptimisationBindingCapability(TasModelKind.GlazingChoice, "Glazing system", OptimisationQuantity.Unspecified, null, new[] { glazingConstruction }, null, true, TasModelKind.MaximumGlazingOptions),

                        // The unit depends on the controller's sensor (a temperature sensor is in °C), so the kind states none.
                        new OptimisationBindingCapability(TasModelKind.ControllerSetpoint, "Plant controller setpoint", OptimisationQuantity.Unspecified, null, new[] { plantRoom, controller }),
                    },
                    new[]
                    {
                        new OptimisationBindingCapability(TasModelKind.AnnualHeatingDemand, "Annual heating demand", OptimisationQuantity.Energy, "kWh"),
                        new OptimisationBindingCapability(TasModelKind.AnnualCoolingDemand, "Annual cooling demand", OptimisationQuantity.Energy, "kWh"),
                        new OptimisationBindingCapability(TasModelKind.OverheatingHours, "Overheating hours", OptimisationQuantity.Time, "h", parameters: new[]
                        {
                            new OptimisationBindingParameter(TasModelKind.ThresholdParameter, TasModelKind.DefaultThreshold, 20, 40, OptimisationQuantity.Temperature, "°C", "resultant temperature threshold"),
                        }),
                        new OptimisationBindingCapability(TasModelKind.AnnualPlantEnergy, "Annual plant energy", OptimisationQuantity.Energy, "kWh"),
                        new OptimisationBindingCapability(TasModelKind.AnnualPlantCost, "Annual plant cost", OptimisationQuantity.Currency, "GBP"),
                        new OptimisationBindingCapability(TasModelKind.AnnualPlantCO2, "Annual plant CO2", OptimisationQuantity.Carbon, "kgCO2e"),
                    });
            }
        }

        /// <summary>
        /// What the "tas-model" engine runs (PR7b): golden section on one variable, Hooke–Jeeves on one or more, try every
        /// option on one choice; minimise only; continuous and discrete (choice) variables; no constraints. Every variable
        /// has a target and every output a measure (<see cref="TasModelKind"/>): zone heating/cooling setpoints per TBD
        /// internal condition, a glazing system choice (at most <see cref="TasModelKind.MaximumGlazingOptions"/> options,
        /// the continuous glazing g-value is not offered), a TPD controller setpoint; annual heating/cooling demand,
        /// overheating hours, annual plant energy, cost and CO2. The "tas-script" capabilities are unchanged.
        /// </summary>
        public static IOptimisationCapabilities TasModelCapabilities()
        {
            return TasModelCapabilitiesHolder.Value;
        }
    }
}
