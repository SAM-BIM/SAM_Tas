// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using SAM.Core.Tas;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last)
        {
            return Simulate(path_TBD, path_TSD, day_First, day_Last, SizingType.Undefined, null);
        }

        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last, SizingType sizingType, IEnumerable<SurfaceOutputSpec> surfaceOutputSpecs)
        {
            if (string.IsNullOrWhiteSpace(path_TBD) || string.IsNullOrWhiteSpace(path_TSD))
                return false;

            bool result = false;

            using (SAMTBDDocument sAMTBDDocument = new (path_TBD))
            {
                TBD.TBDDocument tBDDocument = sAMTBDDocument?.TBDDocument;
                if (tBDDocument == null)
                    return false;

                bool dirty = false;

                if (sizingType != SizingType.Undefined)
                {
                    SetSizingTypes(tBDDocument.Building, sizingType);
                    dirty = true;
                }

                SurfaceOutputSpec firstSpec = null;
                if (surfaceOutputSpecs != null)
                {
                    foreach (SurfaceOutputSpec spec in surfaceOutputSpecs)
                    {
                        if (spec == null)
                            continue;
                        if (firstSpec == null)
                            firstSpec = spec;
                    }
                }

                if (firstSpec != null)
                {
                    Core.Tas.Modify.UpdateSurfaceOutputSpecs(tBDDocument, surfaceOutputSpecs);
                    Core.Tas.Modify.AssignSurfaceOutputSpecs(tBDDocument, firstSpec.Name);
                    dirty = true;
                }

                if (dirty)
                {
                    sAMTBDDocument.Save();
                }

                result = Simulate(tBDDocument, path_TSD, day_First, day_Last);

                if (result)
                {
                    sAMTBDDocument.Save();
                }
            }

            return result;
        }

        public static bool Simulate(SAMTBDDocument sAMTBDDocument, string path_TSD, int day_First, int day_Last)
        {
            return Simulate(sAMTBDDocument?.TBDDocument, path_TSD, day_First, day_Last);
        }

        public static bool Simulate(TBD.TBDDocument tBDDocument, string path_TSD, int day_First, int day_Last)
        {
            if (tBDDocument == null || string.IsNullOrWhiteSpace(path_TSD))
                return false;

            tBDDocument.simulate(day_First, day_Last, 0, 1, 0, 0, path_TSD, 1, 0);

            return Core.Query.WaitToUnlock(path_TSD);
        }
    }
}