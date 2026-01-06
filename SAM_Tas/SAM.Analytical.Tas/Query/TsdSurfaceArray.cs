// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static tsdSurfaceArray? TsdSurfaceArray(this PanelDataType panelDataType)
        {
            if (panelDataType == Tas.PanelDataType.Undefined)
                return null;

            return (tsdSurfaceArray)(int)panelDataType;
        }
    }
}
