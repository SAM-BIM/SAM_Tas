// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Systems;
using SAM.Core;
using SAM.Core.Systems;
using SAM.Core.Tas;
using System.Collections.Generic;
using TBD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Modify
    {
        public static bool CalculateResultantTemperature(string path_TPD, out string path_TBD, out string path_TSD)
        {
            path_TBD = null;
            path_TSD = null;

            if (string.IsNullOrWhiteSpace(path_TPD) || !System.IO.File.Exists(path_TPD))
            {
                return false;
            }

            string directory = System.IO.Path.GetDirectoryName(path_TPD);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path_TPD);

            string path_TBD_Existing = System.IO.Path.Combine(directory, fileName + ".tbd");

            if (string.IsNullOrWhiteSpace(path_TBD_Existing) || !System.IO.File.Exists(path_TBD_Existing))
            {
                return false;
            }

            SystemEnergyCentreConversionSettings systemEnergyCentreConversionSettings = new SystemEnergyCentreConversionSettings()
            {
                Simulate = false,
                IncludeComponentResults = true
            };

            SystemEnergyCentre systemEnergyCentre = Convert.ToSAM(path_TPD, systemEnergyCentreConversionSettings);
            if(systemEnergyCentre is null)
            {
                return false;
            }

            Dictionary<string, IndexedDoubles> dictionary = new Dictionary<string, IndexedDoubles>();

            List<SystemPlantRoom> systemPlantRooms = systemEnergyCentre.GetSystemPlantRooms();
            foreach(SystemPlantRoom systemPlantRoom in systemPlantRooms)
            {
                List<SystemSpaceResult> systemSpaceResults = systemPlantRoom.GetSystemResults<SystemSpaceResult>();
                if(systemSpaceResults is null)
                {
                    continue;
                }

                foreach(SystemSpaceResult systemSpaceResult in systemSpaceResults)
                {
                    if(string.IsNullOrWhiteSpace(systemSpaceResult?.Name))
                    {
                        continue;
                    }

                    dictionary[systemSpaceResult.Name] = systemSpaceResult[SpaceDataType.ZoneTemperature.ToString()];
                }
            }

            if(dictionary.Count == 0)
            {
                return false;
            }

            string suffix = "_TPDThermostat";

            string path_TBD_New = System.IO.Path.Combine(directory, fileName + suffix + ".tbd");

            string path_TSD_New = System.IO.Path.Combine(directory, fileName + suffix + ".tsd");

            System.IO.File.Copy(path_TBD_Existing, path_TBD_New, true);

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD_New))
            {
                TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                Building building = tBDDocument.Building;

                int index_Zone = 0;

                while(building.GetZone(index_Zone) is zone zone)
                {
                    index_Zone++;

                    if(!dictionary.TryGetValue(zone.name, out IndexedDoubles indexedDoubles) || indexedDoubles == null)
                    {
                        continue;
                    }

                    int index_IC = 0;
                    while (zone.GetIC(index_IC) is TBD.InternalCondition internalCondition)
                    {
                        index_IC++;

                        Thermostat thermostat = internalCondition.GetThermostat();
                        if(thermostat is null)
                        {
                            continue;
                        }

                        profile[] profiles = new profile[] { thermostat.GetProfile((int)Profiles.ticUL), thermostat.GetProfile((int)Profiles.ticLL) };

                        foreach(profile profile in profiles)
                        {
                            profile.type = ProfileTypes.ticYearlyProfile;
                            profile.factor = 1;

                            List<double> values = indexedDoubles.GetValues(new Range<int>(0, 8759));

                            float[] values_float = new float[values.Count + 1];
                            for (int i = 1; i < values_float.Length; i++)
                            {
                                values_float[i] = System.Convert.ToSingle(values[i]);
                            }

                            profile.SetYearlyValues(values_float);
                        }

                    }
                }

                sAMTBDDocument.Save();

                tBDDocument.simulate(1, 365, 0, 1, 0, 0, path_TSD_New, 1, 0);

                bool finished = Core.Query.WaitToUnlock(path_TSD_New);
                if (finished)
                {
                    sAMTBDDocument.Save();
                }

                path_TSD = path_TSD_New;
                path_TBD = path_TBD_New;

                return finished;
            }
        }

    }
}