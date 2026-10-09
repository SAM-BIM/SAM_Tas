// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt
{
    /// <summary>
    /// What a Tas project folder offers to the "tas-model" engine: its files, internal conditions, glazing constructions,
    /// plant rooms and controllers, and the current results. <see cref="Query.TasModelInventory(string)"/> reads it from
    /// the Tas files (licensed, COM); the catalogue (<see cref="Query.TasModelCatalogue(TasModelInventory, IEnumerable{TasGlazingSystem}, TasGlazingFilter)"/>)
    /// and the script generator (<see cref="Create.TasScript"/>) need only this data, so they run and are tested without Tas.
    /// </summary>
    public sealed class TasModelInventory
    {
        public TasModelInventory(
            string tbdFileName,
            string tsdFileName,
            string tpdFileName,
            IEnumerable<TasInternalConditionInfo> internalConditions = null,
            IEnumerable<TasGlazingConstructionInfo> glazingConstructions = null,
            IEnumerable<TasPlantRoomInfo> plantRooms = null)
        {
            TbdFileName = tbdFileName;
            TsdFileName = tsdFileName;
            TpdFileName = tpdFileName;
            InternalConditions = (internalConditions ?? Enumerable.Empty<TasInternalConditionInfo>()).Where(x => x != null).ToList().AsReadOnly();
            GlazingConstructions = (glazingConstructions ?? Enumerable.Empty<TasGlazingConstructionInfo>()).Where(x => x != null).ToList().AsReadOnly();
            PlantRooms = (plantRooms ?? Enumerable.Empty<TasPlantRoomInfo>()).Where(x => x != null).ToList().AsReadOnly();
        }

        /// <summary>The TBD file name in the project folder (with extension); null when there is none.</summary>
        public string TbdFileName { get; }

        /// <summary>The TSD file name; null when there is none.</summary>
        public string TsdFileName { get; }

        /// <summary>The TPD file name; null when there is none.</summary>
        public string TpdFileName { get; }

        public IReadOnlyList<TasInternalConditionInfo> InternalConditions { get; }

        public IReadOnlyList<TasGlazingConstructionInfo> GlazingConstructions { get; }

        public IReadOnlyList<TasPlantRoomInfo> PlantRooms { get; }

        /// <summary>The unit text of the TPD's stored annual cost results ("£" for the Systems Demo); null when unknown.</summary>
        public string TpdCostUnit { get; set; }

        /// <summary>Annual heating demand of the existing TSD (kWh); null when unknown.</summary>
        public double? HeatingDemand { get; set; }

        /// <summary>Annual cooling demand of the existing TSD (kWh); null when unknown.</summary>
        public double? CoolingDemand { get; set; }

        /// <summary>Annual plant energy stored in the TPD (kWh); null when unknown.</summary>
        public double? PlantEnergy { get; set; }

        /// <summary>Annual plant cost stored in the TPD (in <see cref="TpdCostUnit"/>); null when unknown.</summary>
        public double? PlantCost { get; set; }

        /// <summary>Annual plant CO2 stored in the TPD (kg); null when unknown.</summary>
        public double? PlantCO2 { get; set; }

        /// <summary>True when the project can run a building simulation (it has a TBD).</summary>
        public bool HasBuilding => !string.IsNullOrEmpty(TbdFileName);

        /// <summary>True when building results can be read: a TBD to simulate or an existing TSD.</summary>
        public bool HasBuildingResults => HasBuilding || !string.IsNullOrEmpty(TsdFileName);

        /// <summary>
        /// True when the plant can be simulated: a TPD and a TBD (the TSD is simulated from it, or an existing TSD is
        /// pointed at the evaluation's copy of it before FixTSDPath, PR7a F1).
        /// </summary>
        public bool HasPlant => !string.IsNullOrEmpty(TpdFileName) && HasBuilding;

        /// <summary>True when the TPD reports cost in pounds sterling, the only currency the engine names (GBP).</summary>
        public bool CostInPounds => TpdCostUnit == "£";
    }
}
