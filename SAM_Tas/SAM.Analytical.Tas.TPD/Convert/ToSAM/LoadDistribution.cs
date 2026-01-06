// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static LoadDistribution ToSAM(this tpdLoadDistribution tpdLoadDistribution)
        {
            switch(tpdLoadDistribution)
            {
                case tpdLoadDistribution.tpdLoadDistributionDaily:
                    return LoadDistribution.Daily;

                case tpdLoadDistribution.tpdLoadDistributionNone:
                    return LoadDistribution.None;

                case tpdLoadDistribution.tpdLoadDistributionEven:
                    return LoadDistribution.Even;
            }

            return LoadDistribution.None;
        }
    }
}
