// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Planar;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static Point2D ToTPD(this Point2D point2D)
        {
            if (point2D == null)
            {
                return null;
            }

            return new Point2D(System.Convert.ToInt32(point2D.X * 100), -System.Convert.ToInt32(point2D.Y * 100));
        }
    }
}
