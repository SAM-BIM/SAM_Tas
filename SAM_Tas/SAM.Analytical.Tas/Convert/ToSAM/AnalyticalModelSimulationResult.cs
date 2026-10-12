// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TSD;

namespace SAM.Analytical.Tas
{
    public static partial class Convert
    {
        public static AnalyticalModelSimulationResult ToSAM_AnalyticalModelSimulationResult(string path_TSD, AnalyticalModel analyticalModel)
        {
            if (string.IsNullOrWhiteSpace(path_TSD) || !System.IO.File.Exists(path_TSD))
            {
                return null;
            }

            AnalyticalModelSimulationResult result = null;

            using (SAMTSDDocument sAMTSDDocument = new SAMTSDDocument(path_TSD, true))
            {
                BuildingData buildingData = sAMTSDDocument.TSDDocument?.SimulationData?.GetBuildingData();
                if (buildingData == null)
                {
                    return null;
                }

                List<double> heatingProfile = Weather.Tas.Query.AnnualBuildingResult<double>(buildingData, tsdBuildingArray.heatingProfile);
                List<double> coolingProfile = Weather.Tas.Query.AnnualBuildingResult<double>(buildingData, tsdBuildingArray.coolingProfile);

                result = ToSAM_AnalyticalModelSimulationResult(analyticalModel.Name, path_TSD, heatingProfile, coolingProfile, Core.Tas.Query.FloorArea(buildingData), Core.Tas.Query.Volume(buildingData));
            }

            return result;
        }

        /// <summary>
        /// The building result from the TSD's hourly building heating and cooling profiles, which Tas holds as loads in W.
        /// Their sums are Wh and their maxima W, so the result stores consumption / 1000 in kWh and peak / 1000 in kW, as
        /// SAM's parameters are labelled, and is marked with
        /// <see cref="Analytical.AnalyticalModelSimulationResultParameter.ResultUnitsVersion"/>. A result written before this
        /// correction has no marker and holds Wh and W: read either through
        /// <see cref="Analytical.Query.TryGetConsumptionOrPeakLoad"/>.
        /// </summary>
        /// <param name="name">Result name: the analytical model's name.</param>
        /// <param name="reference">Result reference: the TSD path.</param>
        /// <param name="heatingProfile">Hourly building heating load [W].</param>
        /// <param name="coolingProfile">Hourly building cooling load [W].</param>
        /// <param name="floorArea">Building floor area [m2]; not stored when NaN.</param>
        /// <param name="volume">Building volume [m3]; not stored when NaN.</param>
        public static AnalyticalModelSimulationResult ToSAM_AnalyticalModelSimulationResult(string name, string reference, IEnumerable<double> heatingProfile, IEnumerable<double> coolingProfile, double floorArea, double volume)
        {
            if (heatingProfile == null || coolingProfile == null)
            {
                return null;
            }

            List<double> values;

            values = heatingProfile.ToList();

            double consumptionHeating = values.Sum();
            double peakHeatingLoad = values.Max();
            int peakHeatingHour = values.IndexOf(peakHeatingLoad);

            values = coolingProfile.ToList();

            double consumptionCooling = values.Sum();
            double peakCoolingLoad = values.Max();
            int peakCoolingHour = values.IndexOf(peakCoolingLoad);

            // Wh -> kWh and W -> kW, once, here; the hours are unchanged.
            AnalyticalModelSimulationResult result = new AnalyticalModelSimulationResult(name, Assembly.GetExecutingAssembly().GetName()?.Name, reference);
            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating, consumptionHeating / 1000);
            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad, peakHeatingLoad / 1000);
            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingHour, peakHeatingHour);

            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling, consumptionCooling / 1000);
            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad, peakCoolingLoad / 1000);
            result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingHour, peakCoolingHour);

            Analytical.Modify.SetResultUnitsVersion(result);

            if (!double.IsNaN(volume))
            {
                result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.Volume, volume);
            }

            if (!double.IsNaN(floorArea))
            {
                result.SetValue(Analytical.AnalyticalModelSimulationResultParameter.FloorArea, floorArea);
            }

            return result;
        }
    }
}
