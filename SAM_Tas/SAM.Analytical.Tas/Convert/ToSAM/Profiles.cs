// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static List<Profile> ToSAM_Profiles(this TBD.InternalCondition internalCondition)
        {
            if(internalCondition == null)
            {
                return null;
            }

            List<Profile> result = new List<Profile>();

            TBD.InternalGain internalGain = internalCondition.GetInternalGain();
            if(internalGain != null)
            {
                Profile profile = null;

                profile = ToSAM(internalGain, TBD.Profiles.ticI, ProfileType.Infiltration, internalCondition.name);
                if(profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticLG, ProfileType.Lighting, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticOLG, ProfileType.Occupancy, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticOSG, ProfileType.Occupancy, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticESG, ProfileType.EquipmentSensible, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticELG, ProfileType.EquipmentLatent, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticCOG, ProfileType.Pollutant, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(internalGain, TBD.Profiles.ticV, ProfileType.Ventilation, internalCondition.name);

                //Same guard as the reuse index applies (Query.IsCollectableSlot): a zero-length ticV is a TAS
                //function profile, and emitting a library entry for it would make VentilationProfileName
                //resolve to a zero-value profile that the export's ordinary value writer then overwrites with
                //24 hourly values. Leave it uncollected so the reference dangles as it did before ticV joined
                //this collector.
                if (profile != null && Query.IsCollectableSlot((int)TBD.Profiles.ticV, profile.GetValues()))
                {
                    result.Add(profile);
                }
            }

            TBD.Thermostat thermostat = internalCondition.GetThermostat();
            if(thermostat != null)
            {
                Profile profile = null;

                profile = ToSAM(thermostat, TBD.Profiles.ticUL, ProfileType.Cooling, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(thermostat, TBD.Profiles.ticLL, ProfileType.Heating, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(thermostat, TBD.Profiles.ticHLL, ProfileType.Humidification, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

                profile = ToSAM(thermostat, TBD.Profiles.ticHUL, ProfileType.Dehumidification, internalCondition.name);
                if (profile != null)
                {
                    result.Add(profile);
                }

            }

            return result;
        }

        public static List<Profile> ToSAM_Profiles(this TBD.Building building)
        {
            List<TBD.InternalCondition> internalConditions_TBD = building?.InternalConditions();
            if(internalConditions_TBD == null|| internalConditions_TBD.Count == 0)
            {
                return null;
            }

            List<Profile> result = new List<Profile>();
            foreach(TBD.InternalCondition internalCondition_TBD in internalConditions_TBD)
            {
                List<Profile> profiles = internalCondition_TBD?.ToSAM_Profiles();
                if(profiles == null || profiles.Count ==0 )
                {
                    continue;
                }

                profiles.ForEach(x => result.Add(x));
            }


            return result;
        }
    }
}
