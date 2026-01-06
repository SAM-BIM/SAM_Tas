// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static FreeCoolingType ToSAM(this tpdFreeCoolingType tpdFreeCoolingType)
        {
            switch(tpdFreeCoolingType)
            {
                case tpdFreeCoolingType.tpdFreeCoolingNone:
                    return FreeCoolingType.None;

                case tpdFreeCoolingType.tpdFreeCoolingOnOff:
                    return FreeCoolingType.OnOff;

                case tpdFreeCoolingType.tpdFreeCoolingVariable:
                    return FreeCoolingType.Variable;
            }

            return FreeCoolingType.None;
        }
    }
}
