// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static AirSystem ToSAM(this global::TPD.ISystem system)
        {
            if (system == null)
            {
                return null;
            }

            dynamic @dynamic = system;

            AirSystem result = new AirSystem(@dynamic.Name);
            Modify.SetReference(result, @dynamic.GUID);
            
            result.Description = dynamic.Description;

            return result;
        }
    }
}
