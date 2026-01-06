// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Systems;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Convert
    {
        public static SystemPlantRoom ToSAM(this PlantRoom plantRoom)
        {
            if (plantRoom == null)
            {
                return null;
            }

            SystemPlantRoom result = new SystemPlantRoom(plantRoom.Name)
            {
            };

            Modify.SetReference(result, plantRoom.GUID());

            return result;
        }
    }
}
