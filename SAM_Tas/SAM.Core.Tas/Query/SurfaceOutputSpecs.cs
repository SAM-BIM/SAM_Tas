// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Core.Tas
{
    public static partial class Query
    {
        public static List<TBD.SurfaceOutputSpec> SurfaceOutputSpecs(this TBD.Building building)
        {
            List<TBD.SurfaceOutputSpec> result = new List<TBD.SurfaceOutputSpec>();

            int index = 0;
            TBD.SurfaceOutputSpec surfaceOutputSpec = building.GetSurfaceOutputSpec(index);
            while (surfaceOutputSpec != null)
            {
                result.Add(surfaceOutputSpec);
                index++;

                surfaceOutputSpec = building.GetSurfaceOutputSpec(index);
            }

            return result;
        }
    }
}
