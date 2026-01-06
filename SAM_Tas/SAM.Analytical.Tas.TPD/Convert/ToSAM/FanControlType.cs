// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static FanControlType ToSAM(this tpdFanControlType tpdFanControlType)
        {
            switch(tpdFanControlType)
            {
                case tpdFanControlType.tpdFanControlFixedSpeed:
                    return FanControlType.FixedSpeed;

                case tpdFanControlType.tpdFanControlVariableSpeed:
                    return FanControlType.VariableSpeed;
            }

            return FanControlType.FixedSpeed;
        }
    }
}
