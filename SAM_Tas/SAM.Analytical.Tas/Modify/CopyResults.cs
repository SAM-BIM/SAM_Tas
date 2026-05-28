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

            // Build a list of (LinkedFace3D, SCSR, InternalPoint3D) candidates from the SolarModel.
            // Previous implementation matched panel ↔ SCSR by buildingElement.GUID (the TAS
            // *construction* GUID), but that's shared across every wall using the same construction,
            // so the dictionary key collapsed 51 SCSRs into ~20 keys and 83 panels into 6 keys,
            // overlapping by just 2 — leaving 48 SCSRs orphaned. Match by geometry instead:
            // each panel finds its closest LinkedFace3D by InternalPoint3D distance.
            List<LinkedFace3D> linkedFace3Ds = solarModel.GetLinkedFace3Ds();
            List<SolarCoverageSimulationResult> coverageResults = solarModel.SolarCoverageSimulationResults;
            if (linkedFace3Ds is null || linkedFace3Ds.Count == 0 || coverageResults is null || coverageResults.Count == 0)
            {
                return new AnalyticalModel(analyticalModel);
            }

            // Index SCSRs by the LinkedFace3D.Guid they're related to (SCSR.Reference is the
            // LinkedFace3D's fresh Guid.ToString() — see Create.SolarModel).
            Dictionary<Guid, SolarCoverageSimulationResult> dictionary_CoverageByLinkedFace3DGuid = new Dictionary<Guid, SolarCoverageSimulationResult>();
            foreach (SolarCoverageSimulationResult coverageResult in coverageResults)
            {
                if (coverageResult?.Reference == null)
                {
                    continue;
                }

                if (Guid.TryParse(coverageResult.Reference, out Guid linkedFace3DGuid))
                {
                    dictionary_CoverageByLinkedFace3DGuid[linkedFace3DGuid] = coverageResult;
                }
            }

            // Collect candidate (LinkedFace3D guid, internalPoint) pairs that have an SCSR.
            List<Tuple<Guid, Point3D>> candidates = new List<Tuple<Guid, Point3D>>();
            foreach (LinkedFace3D linkedFace3D in linkedFace3Ds)
            {
                if (linkedFace3D?.Face3D == null)
                {
                    continue;
                }

                if (!dictionary_CoverageByLinkedFace3DGuid.ContainsKey(linkedFace3D.Guid))
                {
                    continue;
                }

                Point3D internalPoint3D = linkedFace3D.Face3D.InternalPoint3D();
                if (internalPoint3D == null)
                {
                    continue;
                }

                candidates.Add(Tuple.Create(linkedFace3D.Guid, internalPoint3D));
            }

            // Greedy nearest-neighbour match: each LinkedFace3D is claimed by at most one panel.
            double tolerance = 0.5;
            HashSet<Guid> claimed = new HashSet<Guid>();
            foreach (Panel panel in panels)
            {
                Point3D panelPoint = panel?.GetInternalPoint3D();
                if (panelPoint == null)
                {
                    continue;
                }

                Guid bestGuid = Guid.Empty;
                double bestDistance = double.MaxValue;
                foreach (Tuple<Guid, Point3D> candidate in candidates)
                {
                    if (claimed.Contains(candidate.Item1))
                    {
                        continue;
                    }

                    double distance = panelPoint.Distance(candidate.Item2);
                    if (distance < bestDistance && distance <= tolerance)
                    {
                        bestDistance = distance;
                        bestGuid = candidate.Item1;
                    }
                }

                if (bestGuid == Guid.Empty)
                {
                    continue;
                }

                claimed.Add(bestGuid);
                SolarCoverageSimulationResult source = dictionary_CoverageByLinkedFace3DGuid[bestGuid];
                SolarCoverageSimulationResult newCoverageResult = new SolarCoverageSimulationResult(panel.Name, "TAS", panel.Guid.ToString(), source);

                adjacencyCluster.AddObject(newCoverageResult);
                adjacencyCluster.AddRelation(panel, newCoverageResult);
            }

            return new AnalyticalModel(analyticalModel, adjacencyCluster);
        }
    }
}