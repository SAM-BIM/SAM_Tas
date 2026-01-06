// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static string Key(this HeatFlowDirection heatFlowDirection, bool external)
        {
            if(heatFlowDirection == Analytical.HeatFlowDirection.Undefined)
            {
                return null;
            }

            return string.Format("{0} {1}", heatFlowDirection, external ? "External" : "Internal");
        }
    }
}
