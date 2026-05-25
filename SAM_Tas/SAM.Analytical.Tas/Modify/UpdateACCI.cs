// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool UpdateACCI(this TBD.Building building, IEnumerable<string> spaceNames)
        {
            if (building == null)
            {
                return false;
            }

            List<TBD.InternalCondition> internalConditions = building.InternalConditions();
            if(internalConditions == null || internalConditions.Count == 0)
            {
                return false;
            }


            List<double> dryBulbTemperatures = Weather.Tas.Query.AnnualParameter<double>(building.GetWeatherYear(), Weather.WeatherDataType.DryBulbTemperature);
            if(dryBulbTemperatures == null || dryBulbTemperatures.Count != 8760)
            {
                return false;
            }

            // Pre-compute the upper/lower-limit arrays for the entire year ONCE outside the
            // internal-condition loop. The mapping is `DryBulbTemperature → Range<double>`, which
            // depends only on weather, not on the IC. Previously each matching IC re-ran the
            // mapping 8760 times AND allocated a `Range<double>` per hour. With ~14 matching ICs
            // that was ~245k function calls and ~245k allocations for values that are constant
            // across ICs.
            float[] upperLimitYear = new float[8760];
            float[] lowerLimitYear = new float[8760];
            bool[] hourHasRange = new bool[8760];
            bool allHoursHaveRange = true;
            for (int h = 0; h < 8760; h++)
            {
                Range<double> range = Weather.Query.DryBulbTemperatureRange(dryBulbTemperatures[h]);
                if (range == null)
                {
                    allHoursHaveRange = false;
                    continue;
                }

                upperLimitYear[h] = System.Convert.ToSingle(range.Max);
                lowerLimitYear[h] = System.Convert.ToSingle(range.Min);
                hourHasRange[h] = true;
            }

            // O(1) lookup for the per-IC zone-name filter (was `IEnumerable.Contains` per zone).
            HashSet<string> spaceNameSet = (spaceNames != null && spaceNames.Any())
                ? new HashSet<string>(spaceNames)
                : null;

            bool result = false;
            foreach (TBD.InternalCondition internalCondition in internalConditions)
            {
                if(internalCondition.name.EndsWith(" - HDD") || internalCondition.name.EndsWith(" - CDD") || internalCondition.name.EndsWith("New Internal Condition") )
                {
                    continue;
                }

                // BEHAVIOUR-CHANGE (from earlier Stage 2 commit): the original predicate was
                // `spaceNames != null && spaceNames.Count() == 0`, which only ran the filter
                // when the supplied list was EMPTY — meaning callers passing a populated list
                // got NO filtering and callers passing an empty list got everything skipped.
                // The intent (matching the method's parameter name and the sibling overloads)
                // is "apply the filter when names were supplied". Flipped to `.Any()` and now
                // backed by a HashSet for O(1) zone-name membership tests.
                if (spaceNameSet != null)
                {
                    bool contains = false;
                    List<TBD.zone> zones = internalCondition.Zones();
                    if (zones != null)
                    {
                        foreach (TBD.zone zone in zones)
                        {
                            string zoneName = zone?.name;
                            if (zoneName != null && spaceNameSet.Contains(zoneName))
                            {
                                contains = true;
                                break;
                            }
                        }
                    }

                    if (!contains)
                    {
                        continue;
                    }
                }

                TBD.profile profile_UpperLimit = internalCondition.GetThermostat().GetProfile((int)TBD.Profiles.ticUL);
                if (profile_UpperLimit == null)
                {
                    continue;
                }

                TBD.profile profile_LowerLimit = internalCondition.GetThermostat().GetProfile((int)TBD.Profiles.ticLL);
                if (profile_LowerLimit == null)
                {
                    continue;
                }

                profile_UpperLimit.type = TBD.ProfileTypes.ticYearlyProfile;
                profile_LowerLimit.type = TBD.ProfileTypes.ticYearlyProfile;

                // HOT PATH FIX: the previous per-hour loop did 8760 COM-property assignments
                // PER PROFILE (×2 profiles per IC). Each `profile.yearlyValues[i] = …` is a
                // marshalled COM round-trip. For ~14 matching ICs that's ~245k COM calls and
                // dominated runtime (user-observed ~60 s for 14 spaces).
                //
                // `TBD.profileClass` exposes a bulk `SetYearlyValues(Object)` method that takes
                // a `float[]` in a SINGLE COM call — the same pattern already used in
                // SAM.Analytical.Tas/Modify/Update.cs:55. Switching cuts the work to 2 COM
                // round-trips per IC (or 4 in the rare splice path), which is the same shape as
                // GetYearlyValues elsewhere in the codebase.
                if (allHoursHaveRange)
                {
                    // Fast path: every weather hour mapped to a valid range. Write directly.
                    profile_UpperLimit.SetYearlyValues(upperLimitYear);
                    profile_LowerLimit.SetYearlyValues(lowerLimitYear);
                }
                else
                {
                    // Sparse path: some hours had no range (DryBulbTemperatureRange returned null).
                    // Preserve the previous per-hour `continue` semantics by reading the existing
                    // array, splicing only the valid hours, and writing back.
                    float[] upperArray = ToFloatArray(profile_UpperLimit.GetYearlyValues());
                    float[] lowerArray = ToFloatArray(profile_LowerLimit.GetYearlyValues());
                    for (int h = 0; h < 8760; h++)
                    {
                        if (!hourHasRange[h])
                        {
                            continue;
                        }

                        upperArray[h] = upperLimitYear[h];
                        lowerArray[h] = lowerLimitYear[h];
                    }
                    profile_UpperLimit.SetYearlyValues(upperArray);
                    profile_LowerLimit.SetYearlyValues(lowerArray);
                }

                result = true;
            }

            return result;
        }

        // Coerce the boxed SAFEARRAY returned by TBD.profile.GetYearlyValues() into a float[].
        // The interop typically marshals to System.Single[] directly; cast first, fall back to
        // element-wise copy if a different numeric array type comes back.
        private static float[] ToFloatArray(object yearlyValues)
        {
            if (yearlyValues == null)
            {
                return new float[8760];
            }

            if (yearlyValues is float[] floats && floats.Length >= 8760)
            {
                return floats;
            }

            // Conservative fallback: pad/truncate to exactly 8760 to keep the indexing contract
            // with the rest of UpdateACCI stable.
            float[] result = new float[8760];
            if (yearlyValues is System.Array array)
            {
                int copyLength = global::System.Math.Min(array.Length, 8760);
                for (int i = 0; i < copyLength; i++)
                {
                    result[i] = global::System.Convert.ToSingle(array.GetValue(i));
                }
            }
            return result;
        }

        public static bool UpdateACCI(this SAMTBDDocument sAMTBDDocument, IEnumerable<string> spaceNames)
        {
            if (sAMTBDDocument == null)
                return false;

            return UpdateACCI(sAMTBDDocument.TBDDocument?.Building, spaceNames);
        }

        public static bool UpdateACCI(string path_TBD, IEnumerable<string> spaceNames)
        {
            if (string.IsNullOrWhiteSpace(path_TBD))
            {
                return false;
            }

            bool result = false;

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
            {
                result = UpdateACCI(sAMTBDDocument, spaceNames);
                if (result)
                {
                    sAMTBDDocument.Save();
                }
            }

            return result;
        }
    }
}