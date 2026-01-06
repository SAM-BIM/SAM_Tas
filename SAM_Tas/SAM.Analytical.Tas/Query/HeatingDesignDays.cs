// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static List<TBD.HeatingDesignDay> HeatingDesignDays(this TBD.Building building)
        {
            List<TBD.HeatingDesignDay> result = new List<TBD.HeatingDesignDay>();

            int index = 0;
            TBD.HeatingDesignDay heatingDesignDay = building?.GetHeatingDesignDay(index);
            while (heatingDesignDay != null)
            {
                result.Add(heatingDesignDay);
                index++;

                heatingDesignDay = building?.GetHeatingDesignDay(index);
            }

            return result;
        }
    }
}
