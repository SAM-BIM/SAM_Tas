// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static List<Guid> AddDesignDays(this string path_TBD, IEnumerable<DesignDay> coolingDesignDays, IEnumerable<DesignDay> heatingDesignDays, int repetitions = 30)
        {
            if (string.IsNullOrWhiteSpace(path_TBD) || (coolingDesignDays == null && heatingDesignDays == null))
                return null;

            List<Guid> result = null;

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
            {
                result = AddDesignDays(sAMTBDDocument, coolingDesignDays, heatingDesignDays, repetitions);
                if (result != null)
                    sAMTBDDocument.Save();
            }

            return result;
        }

        public static List<Guid> AddDesignDays(this SAMTBDDocument sAMTBDDocument, IEnumerable<DesignDay> coolingDesignDays, IEnumerable<DesignDay> heatingDesignDays, int repetitions = 30)
        {
            if (sAMTBDDocument == null)
                return null;

            return AddDesignDays(sAMTBDDocument.TBDDocument, coolingDesignDays, heatingDesignDays, repetitions);
        }

        public static List<Guid> AddDesignDays(this TBD.TBDDocument tBDDocument, IEnumerable<DesignDay> coolingDesignDays, IEnumerable<DesignDay> heatingDesignDays, int repetitions = 30)
        {
            return AddDesignDays(tBDDocument?.Building, coolingDesignDays, heatingDesignDays, repetitions);
        }

        public static List<Guid> AddDesignDays(this TBD.Building building, IEnumerable<DesignDay> coolingDesignDays, IEnumerable<DesignDay> heatingDesignDays, int repetitions = 30)
        {
            if(building == null)
            {
                return null;
            }

            building.ClearDesignDays();

            TBD.Calendar calendar = building.GetCalendar();

            List<TBD.dayType> dayTypes = Query.DayTypes(calendar);
            Dictionary<string, TBD.dayType> dayTypesByName = BuildDayTypeIndex(dayTypes);
            if (!dayTypesByName.ContainsKey("HDD"))
            {
                TBD.dayType dayType = calendar.AddDayType();
                dayType.name = "HDD";
            }

            if (!dayTypesByName.ContainsKey("CDD"))
            {
                TBD.dayType dayType = calendar.AddDayType();
                dayType.name = "CDD";
            }

            dayTypes = building.DayTypes();
            dayTypesByName = BuildDayTypeIndex(dayTypes);

            List<Guid> result = new List<Guid>();

            if(coolingDesignDays != null && coolingDesignDays.Count() != 0)
            {
                dayTypesByName.TryGetValue("CDD", out TBD.dayType dayType);
                List<TBD.CoolingDesignDay> coolingDesignDays_TBD = building.CoolingDesignDays();
                Dictionary<string, TBD.CoolingDesignDay> coolingByName = new Dictionary<string, TBD.CoolingDesignDay>(coolingDesignDays_TBD?.Count ?? 0);
                if (coolingDesignDays_TBD != null)
                {
                    foreach (TBD.CoolingDesignDay c in coolingDesignDays_TBD)
                    {
                        if (!string.IsNullOrEmpty(c?.name))
                            coolingByName[c.name] = c;
                    }
                }

                foreach(DesignDay designDay in coolingDesignDays)
                {
                    if(designDay == null)
                    {
                        continue;
                    }

                    if (designDay.Name == null || !coolingByName.TryGetValue(designDay.Name, out TBD.CoolingDesignDay coolingDesignDay_TBD) || coolingDesignDay_TBD == null)
                    {
                        coolingDesignDay_TBD = building.AddCoolingDesignDay();
                    }

                    coolingDesignDay_TBD.Update(designDay, dayType, repetitions);
                    result.Add(Guid.Parse(coolingDesignDay_TBD.GUID));
                }
            }

            if (heatingDesignDays != null && heatingDesignDays.Count() != 0)
            {
                dayTypesByName.TryGetValue("HDD", out TBD.dayType dayType);

                List<TBD.HeatingDesignDay> heatingDesignDays_TBD = building.HeatingDesignDays();
                Dictionary<string, TBD.HeatingDesignDay> heatingByName = new Dictionary<string, TBD.HeatingDesignDay>(heatingDesignDays_TBD?.Count ?? 0);
                if (heatingDesignDays_TBD != null)
                {
                    foreach (TBD.HeatingDesignDay h in heatingDesignDays_TBD)
                    {
                        if (!string.IsNullOrEmpty(h?.name))
                            heatingByName[h.name] = h;
                    }
                }

                foreach (DesignDay designDay in heatingDesignDays)
                {
                    if (designDay == null)
                    {
                        continue;
                    }

                    if (designDay.Name == null || !heatingByName.TryGetValue(designDay.Name, out TBD.HeatingDesignDay heatingDesignDay_TBD) || heatingDesignDay_TBD == null)
                    {
                        heatingDesignDay_TBD = building.AddHeatingDesignDay();
                    }

                    heatingDesignDay_TBD.Update(designDay, dayType, repetitions);
                    result.Add(Guid.Parse(heatingDesignDay_TBD.GUID));
                }
            }

            return result;

        }

        private static Dictionary<string, TBD.dayType> BuildDayTypeIndex(IEnumerable<TBD.dayType> dayTypes)
        {
            Dictionary<string, TBD.dayType> result = new Dictionary<string, TBD.dayType>();
            if (dayTypes == null)
                return result;
            foreach (TBD.dayType d in dayTypes)
            {
                if (!string.IsNullOrEmpty(d?.name))
                    result[d.name] = d;
            }
            return result;
        }
    }
}