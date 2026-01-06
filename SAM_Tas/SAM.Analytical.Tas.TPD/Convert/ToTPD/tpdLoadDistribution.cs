// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdLoadDistribution ToTPD(this LoadDistribution loadDistribution)
        {
            switch (loadDistribution)
            {
                case LoadDistribution.Daily:
                    return tpdLoadDistribution.tpdLoadDistributionDaily;

                case LoadDistribution.None:
                    return tpdLoadDistribution.tpdLoadDistributionNone;

                case LoadDistribution.Even:
                    return tpdLoadDistribution.tpdLoadDistributionEven;
            }

            throw new System.NotImplementedException();
        }
    }
}
