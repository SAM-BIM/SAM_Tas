// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static List<TBD.IZoneSurface> ZoneSurfaces(this TBD.zone zone)
        {
           if(zone == null)
            {
                return null;
            }
            
            List<TBD.IZoneSurface> result = new List<TBD.IZoneSurface>();

            int index = 0;
            TBD.IZoneSurface zoneSurface = zone.GetSurface(index);
            while (zoneSurface != null)
            {
                result.Add(zoneSurface);
                index++;

                zoneSurface = zone.GetSurface(index);
            }

            return result;
        }
    }
}
