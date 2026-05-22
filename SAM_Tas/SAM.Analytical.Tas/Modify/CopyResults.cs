// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.SolarCalculator;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.SolarCalculator;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using TBD;
using TCD;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static AnalyticalModel CopyResults(this AnalyticalModel analyticalModel, SolarModel solarModel)
        {
            if(analyticalModel is null)
            {
                return null;
            }


            if(solarModel is null)
            {
                return new AnalyticalModel(analyticalModel);
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

            List<Panel> panels = adjacencyCluster.GetPanels();
            if(panels is null || panels.Count == 0)
            {
                return new AnalyticalModel(analyticalModel);
            }

            Dictionary<string, Panel> dictionary_Panels = [];
            foreach(Panel panel in panels)
            {
                string reference = panel?.GetValue<string>(PanelParameter.BuildingElementGuid);
                if(string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                dictionary_Panels[reference] = panel;
            }

            List<ISolarSimulationResult> solarSimulationResults = solarModel.GetSolarSimulationResults<ISolarSimulationResult>();
            if (solarSimulationResults is null || solarSimulationResults.Count == 0)
            {
                return new AnalyticalModel(analyticalModel);
            }

            Dictionary<string, ISolarSimulationResult> dictionary_SolarSimulationResults = [];
            foreach(ISolarSimulationResult solarSimulationResult in solarSimulationResults)
            {
                string reference = solarSimulationResult?.Reference;
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                dictionary_SolarSimulationResults[reference] = solarSimulationResult;
            }


            foreach(KeyValuePair<string, Panel> keyValuePair in dictionary_Panels)
            {
                if(!dictionary_SolarSimulationResults.TryGetValue(keyValuePair.Key, out ISolarSimulationResult solarSimulationResult) || solarSimulationResult is null)
                {
                    continue;
                }

                Panel panel = keyValuePair.Value;

                SolarCoverageSimulationResult solarCoverageSimulationResult = new SolarCoverageSimulationResult(panel.Name, "TAS", panel.Guid.ToString(), solarSimulationResult as SolarCoverageSimulationResult);

                adjacencyCluster.AddObject(solarCoverageSimulationResult);

                adjacencyCluster.AddRelation(keyValuePair.Value, solarCoverageSimulationResult);
            }

            return new AnalyticalModel(analyticalModel, adjacencyCluster);
        }
    }
}