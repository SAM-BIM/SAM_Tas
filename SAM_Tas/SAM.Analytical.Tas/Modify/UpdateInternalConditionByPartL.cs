using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool UpdateInternalConditionByPartL(this TBD.TBDDocument tBDDocument, TIC.Document tICDocument, AnalyticalModel analyticalModel)
        {
            if(tBDDocument == null || tICDocument == null || analyticalModel == null)
            {
                return false;
            }

            List<TBD.zone> zones = tBDDocument.Building.Zones();
            if(zones == null || zones.Count == 0)
            {
                return false;
            }

            List<Space> spaces = analyticalModel.GetSpaces();
            if(spaces == null || spaces.Count == 0)
            {
                return false;
            }

            TBD.Calendar calendar = tBDDocument.Building.GetCalendar();
            List<TBD.dayType> dayTypes = calendar.DayTypes();
            if(dayTypes != null)
            {
            }

            List<TIC.InternalCondition> internalConditions_TIC = Query.InternalConditions(tICDocument);

            // Pre-fetch and index zones by normalized name (matches space.Match(zones) semantics).
            // Was doing a linear .Find via space.Match() per space -> O(spaces * zones).
            Dictionary<string, TBD.zone> zonesByKey = new Dictionary<string, TBD.zone>(zones.Count);
            foreach (TBD.zone z in zones)
            {
                string n = z?.name;
                if (!string.IsNullOrWhiteSpace(n))
                    zonesByKey[n.Trim().ToUpper()] = z;
            }

            // Pre-fetch building dayTypes once. Was being re-fetched (COM rebuild) per space.
            List<TBD.dayType> dayTypes_Building = tBDDocument.Building.DayTypes();

            // Track existing internal conditions in a single in-memory list that we keep in sync as we add new ones,
            // so we never re-call Query.InternalConditions(tBDDocument) inside the per-space loop.
            List<TBD.InternalCondition> internalConditions_TBD = Query.InternalConditions(tBDDocument) ?? new List<TBD.InternalCondition>();

            // Names of newly-added ICs — HashSet for O(1) "did we add this?" check after the loop.
            HashSet<string> internalConditionNames_New = new HashSet<string>();

            bool result = false;
            foreach (Space space in spaces)
            {
                InternalCondition internalCondition = space?.InternalCondition;
                if(internalCondition == null)
                {
                    continue;
                }

                if(!internalCondition.TryGetValue(InternalConditionParameter.NCMData, out NCMData nCMData) || nCMData == null)
                {
                    continue;
                }

                string name = nCMData.Name;
                if(string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                TBD.zone zone = null;
                if (!string.IsNullOrEmpty(space?.Name))
                    zonesByKey.TryGetValue(space.Name.Trim().ToUpper(), out zone);
                if(zone == null)
                {
                    continue;
                }

                TBD.InternalCondition internalCondition_TBD = null;

                List<TBD.InternalCondition> internalConditions_TBD_Temp = internalConditions_TBD.FindAll(x => x.name.StartsWith(name));
                if(internalConditions_TBD_Temp != null && internalConditions_TBD_Temp.Count != 0)
                {
                    internalConditions_TBD_Temp.Sort((x, y) => x.name.Length.CompareTo(y.name.Length));
                    internalCondition_TBD = internalConditions_TBD_Temp[0];
                }

                if(internalCondition_TBD == null)
                {
                    List<TIC.InternalCondition> internalConditions_TIC_Temp = internalConditions_TIC.FindAll(x => x.name.StartsWith(name));
                    if(internalConditions_TIC_Temp != null && internalConditions_TIC_Temp.Count != 0)
                    {
                        internalConditions_TIC_Temp.Sort((x, y) => x.name.Length.CompareTo(y.name.Length));
                        TIC.InternalCondition internalCondition_TIC = internalConditions_TIC_Temp[0];
                        dynamic @dynamic = tICDocument.ToGlobalMem(internalCondition_TIC);
                        internalCondition_TBD = tBDDocument.Building.AddInternalConditionFromGlobal(@dynamic);
                        if (internalCondition_TBD != null)
                            internalConditions_TBD.Add(internalCondition_TBD); // keep our list in sync
                    }
                }

                if(internalCondition_TBD == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(internalCondition_TBD.name))
                    internalConditionNames_New.Add(internalCondition_TBD.name);

                zone.AssignIC(internalCondition_TBD, true);

                if (dayTypes_Building != null && dayTypes_Building.Count != 0)
                {
                    foreach(TBD.dayType dayType in dayTypes_Building)
                    {
                        bool add = !(dayType.name == "HDD" || dayType.name == "CDD");
                        internalCondition_TBD.SetDayType(dayType, add);
                    }
                }

                result = true;
            }

            internalConditions_TBD = Query.InternalConditions(tBDDocument);
            if(internalConditions_TBD != null)
            {
                List<string> names = new List<string>();
                foreach (TBD.InternalCondition internalCondition_TBD in internalConditions_TBD)
                {
                    if (internalConditionNames_New.Contains(internalCondition_TBD.name))
                    {
                        continue;
                    }

                    names.Add(internalCondition_TBD.name);
                }

                foreach(string name in names)
                {
                    tBDDocument.Building.RemoveInternalCondition(name);
                }
            }

            tBDDocument.Building.ClearDesignDays();

            if(zones != null)
            {
                foreach(TBD.zone zone in zones)
                {
                    zone.sizeCooling = (int)TBD.SizingType.tbdSizing;
                    zone.sizeHeating = (int)TBD.SizingType.tbdSizing;
                }
            }

            return result;
        }
    }
}