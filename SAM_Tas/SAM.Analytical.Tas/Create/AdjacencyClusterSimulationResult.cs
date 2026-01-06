// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors


using System.Reflection;
using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        public static AdjacencyClusterSimulationResult AdjacencyClusterSimulationResult(this BuildingData buildingData)
        {
            if (buildingData == null)
                return null;

            AdjacencyClusterSimulationResult result = new AdjacencyClusterSimulationResult(buildingData.name, Assembly.GetExecutingAssembly().GetName()?.Name, buildingData.GUID);
            return result;
        }
    }
}
