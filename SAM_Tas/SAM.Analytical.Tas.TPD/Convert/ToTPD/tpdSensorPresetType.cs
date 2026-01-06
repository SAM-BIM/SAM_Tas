// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdSensorPresetType ToTPD(this NormalControllerLimit normalControllerLimit)
        {
            switch (normalControllerLimit)
            {
                case NormalControllerLimit.Lower:
                    return tpdSensorPresetType.tpdLowerLimit;

                case NormalControllerLimit.Upper:
                    return tpdSensorPresetType.tpdUpperLimit;

                case NormalControllerLimit.LowerAndUpper:
                    return tpdSensorPresetType.tpdLowerAndUpperLimit;
            }

            throw new System.NotImplementedException();
        }
    }
}
