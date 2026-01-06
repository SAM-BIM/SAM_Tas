// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static HeatPumpType ToSAM(this global::TPD.tpdHeatPumpType tpdHeatPumpType)
        {
            switch(tpdHeatPumpType)
            {
                case global::TPD.tpdHeatPumpType.tpdHeatPumpHVRFBC:
                    return HeatPumpType.HVRFBC;

                case global::TPD.tpdHeatPumpType.tpdHeatPumpVRFBC:
                    return HeatPumpType.VRFBC;

                case global::TPD.tpdHeatPumpType.tpdHeatPumpVRF:
                    return HeatPumpType.VRF;

                case global::TPD.tpdHeatPumpType.tpdHeatPumpSingleSplit:
                    return HeatPumpType.SingleSplit;

                case global::TPD.tpdHeatPumpType.tpdHeatPumpMultiSplit:
                    return HeatPumpType.MultiSplit;
            }

            throw new System.NotImplementedException();
        }
    }
}
