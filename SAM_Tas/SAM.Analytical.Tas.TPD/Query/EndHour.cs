// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static int EndHour(this TPDDoc tPDDoc)
        { 
            if(tPDDoc?.EnergyCentre == null)
            {
                return -1;
            }

            return tPDDoc.EnergyCentre.GetEndHour();
        }
    }
}
