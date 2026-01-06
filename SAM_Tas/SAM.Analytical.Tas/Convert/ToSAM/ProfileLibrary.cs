// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static ProfileLibrary ToSAM_ProfileLibrary(this TBD.Building building)
        {
            List<Profile> profiles = building?.ToSAM_Profiles();
            if(profiles == null)
            {
                return null;
            }

            ProfileLibrary result = new ProfileLibrary(building.name);
            profiles.ForEach(x => result.Add(x));

            return result;
        }
    }
}
