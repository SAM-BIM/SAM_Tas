﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static Dictionary<tsdZoneArray, Dictionary<string, double[]>> YearlyValues(this BuildingData buildingData, IEnumerable<tsdZoneArray> tSDZoneArrays)
        {
            if (buildingData == null || tSDZoneArrays == null)
                return null;

            Dictionary<string, ZoneData> dictionary = ZoneDataDictionary(buildingData);

            Dictionary<tsdZoneArray, Dictionary<string, double[]>> result = new Dictionary<tsdZoneArray, Dictionary<string, double[]>>();
            foreach (tsdZoneArray tSDZoneArray in tSDZoneArrays)
            {
                Dictionary<string, double[]> dictionary_ZoneArray = new Dictionary<string, double[]>();
                foreach (KeyValuePair<string, ZoneData> keyValuePair in dictionary)
                    dictionary_ZoneArray.Add(keyValuePair.Key, new double[8760]);

                for (int i = 1; i <= 365; i++)
                {
                    foreach (KeyValuePair<string, double[]> keyValuePair_Values in dictionary_ZoneArray)
                    {
                        float[] values_Day = (dictionary[keyValuePair_Values.Key].GetDailyZoneResult(i, (short)tSDZoneArray) as IEnumerable).Cast<float>().ToArray();
                        int startHour = (i * 24) - 24;
                        int counter = 0;
                        for (int n = startHour; n <= startHour + 23; n++)
                        {
                            keyValuePair_Values.Value[n] = values_Day[counter];
                            counter++;
                        }
                    }

                }
                result.Add(tSDZoneArray, dictionary_ZoneArray);
            }

            return result;
        }

        public static Dictionary<string, double[]> YearlyValues(this BuildingData buildingData, tsdZoneArray tSDZoneArray)
        {
            return YearlyValues(buildingData, new tsdZoneArray[] { tSDZoneArray })?[tSDZoneArray];
        }
    }
}