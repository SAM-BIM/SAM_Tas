// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdFancoilControlMethod ToTPD(this FanCoilControlMethod fanCoilControlMethod)
        {
            switch (fanCoilControlMethod)
            {
                case FanCoilControlMethod.CAV:
                    return tpdFancoilControlMethod.tpdFancoilControlCAV;

                case FanCoilControlMethod.VAV:
                    return tpdFancoilControlMethod.tpdFancoilControlVAV;

                case FanCoilControlMethod.OnOff:
                    return tpdFancoilControlMethod.tpdFancoilControlOnOff;
            }

            throw new System.NotImplementedException();
        }
    }
}
