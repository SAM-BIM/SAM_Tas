// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Query
    {
        public static List<SystemZone> SystemZones(this global::TPD.System system)
        {
            List<SystemComponent> systemComponents = SystemComponents<SystemComponent>(system);
            if (systemComponents == null)
            {
                return null;
            }

            List<SystemZone> result = new List<SystemZone>();
            foreach (SystemComponent systemComponent in systemComponents)
            {
                if (systemComponent is SystemZone)
                {
                    result.Add((SystemZone)systemComponent);
                }
                else if (systemComponent is ComponentGroup)
                {
                    List<SystemZone> systemZones = SystemZones((ComponentGroup)systemComponent);
                    if (systemZones != null)
                    {
                        result.AddRange(systemZones);
                    }
                }
            }

            return result;
        }

        public static List<SystemZone> SystemZones(ComponentGroup componentGroup)
        {
            List<SystemComponent> systemComponents = SystemComponents<SystemComponent>(componentGroup);
            if(systemComponents == null)
            {
                return null;
            }

            List<SystemZone> result = new List<SystemZone>();
            foreach(SystemComponent systemComponent in systemComponents)
            {
                if (systemComponent is SystemZone)
                {
                    result.Add((SystemZone)systemComponent);
                }
                else if (systemComponent is ComponentGroup)
                {
                    List<SystemZone> systemZones = SystemZones((ComponentGroup)systemComponent);
                    if(systemZones != null)
                    {
                        result.AddRange(systemZones);
                    }
                }
            }

            return result;
        }
    }
}
