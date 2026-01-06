// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static List<PlantControlArc> ChainPlantControlArcs(this PlantController plantController)
        {
            if (plantController == null)
            {
                return null;
            }

            List<PlantControlArc> result = new List<PlantControlArc>();

            int count = plantController.GetChainArcCount();
            for (int i = 1; i <= count; i++)
            {
                PlantControlArc plantControlArc = plantController.GetChainArc(i);
                if (plantControlArc == null)
                {
                    continue;
                }

                result.Add(plantControlArc);
            }

            return result;
        }
    }
}
