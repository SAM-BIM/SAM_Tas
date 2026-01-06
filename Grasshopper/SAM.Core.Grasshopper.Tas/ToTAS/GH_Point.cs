// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Grasshopper.Kernel.Types;
using SAM.Geometry.Rhino;

namespace SAM.Geometry.Grasshopper
{
    public static partial class Convert
    {
        public static GH_Point ToGrasshopper(this Spatial.Point3D point3D)
        {
            return new GH_Point(point3D.ToRhino());
        }
    }
}
