// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static tpdZoneComponentPosition ToTPD(this SystemSpaceComponentPosition SystemSpaceComponentPosition)
        {
            switch (SystemSpaceComponentPosition)
            {
                case SystemSpaceComponentPosition.InZone:
                    return tpdZoneComponentPosition.tpdZoneComponentInZone;

                case SystemSpaceComponentPosition.ParallelTerminalUnit:
                    return tpdZoneComponentPosition.tpdZoneComponentParallelTerminalUnit;

                case SystemSpaceComponentPosition.TerminalUnit:
                    return tpdZoneComponentPosition.tpdZoneComponentTerminalUnit;
            }

            throw new System.NotImplementedException();
        }
    }
}
