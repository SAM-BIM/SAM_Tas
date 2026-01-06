// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static List<TBD.buildingElement> BuildingElements(this TBD.Building building)
        {
            List<TBD.buildingElement> result = new List<TBD.buildingElement>();

            int index = 0;
            TBD.buildingElement buildingElement = building.GetBuildingElement(index);
            while (buildingElement != null)
            {
                result.Add(buildingElement);
                index++;

                buildingElement = building.GetBuildingElement(index);
            }

            return result;
        }
    }
}
