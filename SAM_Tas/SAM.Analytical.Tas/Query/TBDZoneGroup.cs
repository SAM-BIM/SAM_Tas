// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static string TBDZoneGroup(int index)
        {
            switch (index)
            {
                case 0:
                    return "Default";

                case 1:
                    return "HVAC";

                case 2:
                    return "Output";

                case 3:
                    return "Zone Set";
            }

            return null;
        }

        /// <summary>
        /// Reverse of <see cref="TBDZoneGroup(int)"/>: maps the category string captured on import
        /// (stored on the SAM Zone as <c>ZoneParameter.TBDZoneGroup</c>) back to the TBD
        /// <c>ZoneGroupType</c>, so an exported zone group lands under its original TBD category.
        /// Unknown/empty values fall back to <c>tbdDefaultZG</c>.
        /// </summary>
        public static TBD.ZoneGroupType TBDZoneGroupType(string name)
        {
            switch (name)
            {
                case "Default":
                    return (TBD.ZoneGroupType)0;

                case "HVAC":
                    return (TBD.ZoneGroupType)1;

                case "Output":
                    return (TBD.ZoneGroupType)2;

                case "Zone Set":
                    return (TBD.ZoneGroupType)3;
            }

            return TBD.ZoneGroupType.tbdDefaultZG;
        }
    }
}