// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using TPD;
using SAM.Analytical.Systems;
using SAM.Geometry.Planar;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static AirSystemGroup ToSAM(this ComponentGroup componentGroup, BoundingBox2D boundingBox2D = null)
        {
            if (componentGroup == null)
            {
                return null;
            }

            dynamic @dynamic = componentGroup;

            AirSystemGroup result = new AirSystemGroup(dynamic.Name);
            Modify.SetReference(result, @dynamic.GUID);
            
            result.Description = dynamic.Description;

            if(boundingBox2D != null)
            {
                result = new DisplayAirSystemGroup(result, boundingBox2D);
            }

            return result;
        }
    }
}
