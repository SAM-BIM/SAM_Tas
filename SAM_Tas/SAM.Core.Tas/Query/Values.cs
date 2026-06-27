// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Core.Tas
{
    public static partial class Query
    {
        public static List<double> Values(this TBD.profile profile)
        {
            if(profile == null)
            {
                return null;
            }

            List<double> result = new List<double>();
            switch (profile.type)
            {
                case TBD.ProfileTypes.ticValueProfile:
                    result.Add(profile.value);
                    break;

                case TBD.ProfileTypes.ticHourlyProfile:
                    // 24 iters — per-index COM getter is fine, no bulk hourly getter on TBD.profileClass.
                    for (int i = 0; i <= 23; i++)
                        result.Add(profile.hourlyValues[i + 1]);
                    break;

                case TBD.ProfileTypes.ticYearlyProfile:
                    // Mirror of the UpdateACCI bulk-write fix: the per-index `yearlyValues[i + 1]`
                    // getter on TBD.profileClass is a marshalled COM round-trip — 8760 of them per
                    // call adds up because this is invoked once per profile during TBD import
                    // (see SAM.Analytical.Tas/Convert/ToSAM/Profile.cs:20, 53). `GetYearlyValues()`
                    // returns the whole SAFEARRAY in a single COM call. Coerce to float[] via the
                    // same defensive bounds-aware path used in UpdateACCI, then widen to double.
                    float[] yearlyValues = ToFloatArray(profile.GetYearlyValues());
                    for (int i = 0; i < 8760; i++)
                        result.Add(yearlyValues[i]);
                    break;
            }

            return result;
        }

        // Coerce the boxed SAFEARRAY returned by TBD.profile.GetYearlyValues() into a float[].
        // Mirror of the helper in SAM.Analytical.Tas/Modify/UpdateACCI.cs (same project chain
        // can't reference the private one). Fast path: marshalled `float[]`. Defensive fallback:
        // any other System.Array — bounds-aware via GetLowerBound/GetUpperBound so it doesn't
        // throw on a non-zero-lower-bound SAFEARRAY.
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

            float[] result = new float[8760];
            if (yearlyValues is global::System.Array array)
            {
                int lowerBound = array.GetLowerBound(0);
                int upperBound = array.GetUpperBound(0);
                int sourceLength = upperBound - lowerBound + 1;
                int copyLength = global::System.Math.Min(sourceLength, 8760);
                for (int i = 0; i < copyLength; i++)
                {
                    result[i] = global::System.Convert.ToSingle(array.GetValue(lowerBound + i));
                }
            }
            return result;
        }
    }
}