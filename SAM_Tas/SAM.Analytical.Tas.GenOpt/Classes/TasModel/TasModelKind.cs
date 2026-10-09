// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// The binding kinds, reference keys and parameters of the "tas-model" engine (PR7b). They are the words a definition
    /// writes in <c>target.kind</c>, <c>measure.kind</c>, <c>reference</c> and <c>parameters</c>; SAM.Core.Optimisation
    /// checks a definition against <see cref="Query.TasModelCapabilities"/>, which lists exactly these.
    /// </summary>
    public static class TasModelKind
    {
        /// <summary>Zone heating setpoint of a TBD internal condition (thermostat lower limit, °C).</summary>
        public const string HeatingSetpoint = "tbd.internal-condition.heating-setpoint";

        /// <summary>Zone cooling setpoint of a TBD internal condition (thermostat upper limit, °C).</summary>
        public const string CoolingSetpoint = "tbd.internal-condition.cooling-setpoint";

        /// <summary>A choice between real glazing systems for every element using one TBD glazing construction.</summary>
        public const string GlazingChoice = "tbd.glazing-construction.choice";

        /// <summary>Setpoint of a TPD plant controller.</summary>
        public const string ControllerSetpoint = "tpd.controller.setpoint";

        /// <summary>Annual heating demand from the TSD (kWh).</summary>
        public const string AnnualHeatingDemand = "tsd.annual-heating-demand";

        /// <summary>Annual cooling demand from the TSD (kWh).</summary>
        public const string AnnualCoolingDemand = "tsd.annual-cooling-demand";

        /// <summary>Occupied hours above a resultant temperature threshold in the worst occupied zone (h).</summary>
        public const string OverheatingHours = "tsd.overheating-hours";

        /// <summary>Annual plant energy from the TPD (kWh).</summary>
        public const string AnnualPlantEnergy = "tpd.annual-energy";

        /// <summary>Annual plant cost from the TPD (GBP).</summary>
        public const string AnnualPlantCost = "tpd.annual-cost";

        /// <summary>Annual plant CO2 from the TPD (kgCO2e).</summary>
        public const string AnnualPlantCO2 = "tpd.annual-co2";

        /// <summary>Reference key: the exact name of a TBD internal condition.</summary>
        public const string InternalConditionKey = "internalCondition";

        /// <summary>Reference key: the exact name of the TBD transparent construction the glazing elements use.</summary>
        public const string GlazingConstructionKey = "glazingConstruction";

        /// <summary>Reference key: the exact name of a TPD plant room.</summary>
        public const string PlantRoomKey = "plantRoom";

        /// <summary>Reference key: the exact name of a controller in that plant room.</summary>
        public const string ControllerKey = "controller";

        /// <summary>Parameter of <see cref="OverheatingHours"/>: the resultant temperature threshold (°C).</summary>
        public const string ThresholdParameter = "threshold";

        /// <summary>Default of <see cref="ThresholdParameter"/> (°C).</summary>
        public const double DefaultThreshold = 28;

        /// <summary>The most options a glazing choice may list: each option is one simulation (PR7a-2 owner default).</summary>
        public const int MaximumGlazingOptions = 8;

        /// <summary>True for a target kind that changes the TBD, so an evaluation runs the building simulation.</summary>
        public static bool IsBuildingTarget(string kind)
        {
            return kind == HeatingSetpoint || kind == CoolingSetpoint || kind == GlazingChoice;
        }

        /// <summary>True for a measure read from the TSD.</summary>
        public static bool IsBuildingMeasure(string kind)
        {
            return kind == AnnualHeatingDemand || kind == AnnualCoolingDemand || kind == OverheatingHours;
        }

        /// <summary>True for a target or measure that needs the plant simulation.</summary>
        public static bool IsPlantKind(string kind)
        {
            return kind == ControllerSetpoint || kind == AnnualPlantEnergy || kind == AnnualPlantCost || kind == AnnualPlantCO2;
        }
    }
}
