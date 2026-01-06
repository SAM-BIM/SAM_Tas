// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static DesignCondition ToSAM(this DesignConditionLoad designConditionLoad)
        {
            if (designConditionLoad == null)
            {
                return null;
            }

            DesignCondition result = new DesignCondition(
                designConditionLoad.Name, 
                designConditionLoad.Description,
                designConditionLoad.PrecondHours,
                designConditionLoad.StartHour,
                designConditionLoad.EndHour);

            return result;
        }
    }
}
