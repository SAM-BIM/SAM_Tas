// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static HeatFlowDirection HeatFlowDirection(string key, out bool external)
        {
            external = false;

            if(string.IsNullOrWhiteSpace(key))
            {
                return Analytical.HeatFlowDirection.Undefined;
            }

            string key_Temp = key.ToLower();
            if(key_Temp.Contains("external"))
            {
                external = true;
            }

            foreach(HeatFlowDirection heatFlowDirection in Enum.GetValues(typeof(HeatFlowDirection)))
            {
                if(key_Temp.Contains(heatFlowDirection.ToString().ToLower()))
                {
                    return heatFlowDirection;
                }
            }

            return Analytical.HeatFlowDirection.Undefined;
        }
    }
}
