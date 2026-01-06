// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool RemoveInternalCondition(this TBD.Building building, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            List<bool> result = RemoveInternalConditions(building, new string[] { name });

            return result != null && result.Count > 0 && result[0];
        }
    }
}
