using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static TBD.InternalCondition AddInternalCondition(this TBD.Building building, Space space, ProfileLibrary profileLibrary, AdjacencyCluster adjacencyCluster = null)
        {
            return AddInternalCondition(building, space, profileLibrary, null, adjacencyCluster);
        }

        // dayTypes_NonHDD: pre-computed building.DayTypes() with the "HDD" entry removed. When the
        // caller is iterating many spaces, hoisting this list eliminates a per-space TBD COM walk
        // (GetDayTypeCount + dayTypes(i) + .name access). Pass null to compute on demand.
        public static TBD.InternalCondition AddInternalCondition(this TBD.Building building, Space space, ProfileLibrary profileLibrary, IEnumerable<TBD.dayType> dayTypes_NonHDD, AdjacencyCluster adjacencyCluster = null)
        {
            if (building == null || space == null || profileLibrary == null)
                return null;

            TBD.InternalCondition internalCondition = building.AddIC(null);
            if (internalCondition == null)
                return null;

            IEnumerable<TBD.dayType> dayTypes = dayTypes_NonHDD;
            if (dayTypes == null)
            {
                List<TBD.dayType> dayTypes_Temp = building.DayTypes();
                if (dayTypes_Temp != null)
                {
                    dayTypes_Temp.RemoveAll(x => x.name.Equals("HDD"));
                    dayTypes = dayTypes_Temp;
                }
            }

            if (dayTypes != null)
            {
                foreach (TBD.dayType dayType in dayTypes)
                    internalCondition.SetDayType(dayType, true);
            }

            UpdateInternalCondition(internalCondition, space, profileLibrary, adjacencyCluster);

            return internalCondition;
        }
    }
}
