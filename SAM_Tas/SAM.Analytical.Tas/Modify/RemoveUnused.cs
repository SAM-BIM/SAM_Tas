// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static List<Guid> RemoveUnused(this SAMT3DDocument sAMT3DDocument)
        {
            if (sAMT3DDocument == null)
                return null;

            return RemoveUnused(sAMT3DDocument.T3DDocument);
        }

        public static List<Guid> RemoveUnused(this TAS3D.Building building)
        {
            if(building == null)
            {
                return null;
            }

            List<Guid> result = new List<Guid>();
            result.AddRange(RemoveUnusedZones(building));
            result.AddRange(RemoveUnusedWindows(building));
            result.AddRange(RemoveUnusedElements(building, new string[] { "Exposed Floor", "External Wall", "Ground Floor", "Internal Ceiling", "Internal Floor", "Internal Wall", "Roof" }));

            return result;
        }

        public static List<Guid> RemoveUnused(this TAS3D.T3DDocument t3DDocument)
        {
            return RemoveUnused(t3DDocument?.Building);
        }
    }
}
