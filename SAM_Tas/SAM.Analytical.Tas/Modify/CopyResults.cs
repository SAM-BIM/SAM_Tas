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

            // Optional diagnostic log written to %TEMP%\SAM_CopyResults.log (overwritten each run)
            // so we can see which apertures/panels matched which SolarModel surface and why a part
            // was skipped. OFF by default: set environment variable SAM_DEBUG (to any non-empty
            // value) to enable it. When disabled, `log` is null and Log(...) is a no-op — no IO.
            bool logEnabled = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAM_DEBUG"));
            System.Text.StringBuilder log = logEnabled ? new System.Text.StringBuilder() : null;
            void Log(string message) { if (log != null) { log.Append(message); log.Append(Environment.NewLine); } }

            Log("=== CopyResults " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");

            // Collect candidate (LinkedFace3D guid, internalPoint, reference, area) tuples that have
            // an SCSR. The reference is the TAS buildingElement.GUID stamped by Create.SolarModel.
            // NOTE: that GUID is actually the *construction* GUID (shared across all surfaces using
            // the same construction), so it only separates pane-construction from frame-construction
            // — it does NOT uniquely identify a single window. Area is carried so the geometry
            // fallback can tell a pane sub-face from the concentric frame sub-face.
            List<Tuple<Guid, Point3D, string, double>> candidates = new List<Tuple<Guid, Point3D, string, double>>();
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

                candidates.Add(Tuple.Create(linkedFace3D.Guid, internalPoint3D, linkedFace3D.Reference, linkedFace3D.Face3D.GetArea()));
            }

            Log("Panels: " + panels.Count + "   Candidates (LinkedFace3D w/ SCSR): " + candidates.Count);
            Log("-- Candidates --");
            foreach (Tuple<Guid, Point3D, string, double> candidate in candidates)
            {
                Log("  [" + candidate.Item1.ToString().Substring(0, 8) + "] area=" + candidate.Item4.ToString("0.###") + " ref=" + (candidate.Item3 ?? "<null>") + " centroid=" + candidate.Item2);
            }

            double tolerance = 0.5;
            HashSet<Guid> claimed = new HashSet<Guid>();
            int paneMatched = 0;
            int frameMatched = 0;
            int panelMatched = 0;

            // Apertures (window/door) are exposed zoneSurfaces too, so Create.SolarModel emits a
            // LinkedFace3D + SCSR for each "… -pane" and "… -frame" surface — but a single SAM
            // Aperture carries BOTH. We match by GEOMETRY, NOT the stored building-element GUIDs:
            // logged testing showed the import sometimes swaps/duplicates the pane & frame GUIDs
            // between mirrored windows, so a GUID match drops some frames and places others on the
            // wrong window. Geometry is the reliable signal:
            //   * a window's pane and frame surfaces share the same horizontal (X,Y) position;
            //   * the TAS frame surface is the OUTER opening (larger area), the pane the inner
            //     glazing (smaller area);
            //   * the SAM aperture face sits at a different height than the TAS surface (a known,
            //     constant vertical offset), so we gate on HORIZONTAL (X,Y) distance, then anchor on
            //     the nearest candidate's Z to keep only the same floor (see stacked-window note below).
            //
            // Runs BEFORE the panel loop so each aperture claims its own two (small) window
            // surfaces first; the larger wall surfaces are left for the panels.
            List<Aperture> apertures = adjacencyCluster.GetApertures();
            Log("-- Apertures: " + (apertures == null ? 0 : apertures.Count) + " --");
            if (apertures != null && apertures.Count != 0)
            {
                foreach (Aperture aperture in apertures)
                {
                    if (aperture == null)
                    {
                        continue;
                    }

                    Point3D aperturePoint = aperture.GetFace3D()?.InternalPoint3D();
                    Log("Aperture \"" + aperture.Name + "\" guid=" + aperture.Guid.ToString().Substring(0, 8) + " center=" + aperturePoint);
                    if (aperturePoint == null)
                    {
                        Log("  SKIP pane+frame — aperture has no internal point");
                        continue;
                    }

                    // Unclaimed candidates co-located with this aperture (horizontal distance).
                    List<Tuple<Guid, Point3D, string, double>> nearby = new List<Tuple<Guid, Point3D, string, double>>();
                    foreach (Tuple<Guid, Point3D, string, double> candidate in candidates)
                    {
                        if (claimed.Contains(candidate.Item1))
                        {
                            continue;
                        }

                        double dX = aperturePoint.X - candidate.Item2.X;
                        double dY = aperturePoint.Y - candidate.Item2.Y;
                        double horizontalDistance = Math.Sqrt((dX * dX) + (dY * dY));
                        if (horizontalDistance <= tolerance)
                        {
                            nearby.Add(candidate);
                        }
                    }

                    // Disambiguate vertically-stacked windows (same plan location, different floors):
                    // anchor on the height (Z) of the NEAREST-in-3D co-located surface — that is THIS
                    // window's surface, since other floors are a storey height away — and keep only
                    // candidates within tolerance of that height. A window's own pane and frame share
                    // this Z, so they stay; other floors are dropped and can't steal coverage. The
                    // constant SAM<->TAS vertical offset cancels out because we anchor on the nearest
                    // actual candidate rather than on an absolute height.
                    List<Tuple<Guid, Point3D, string, double>> sameFloor = new List<Tuple<Guid, Point3D, string, double>>();
                    if (nearby.Count != 0)
                    {
                        Tuple<Guid, Point3D, string, double> nearest = null;
                        double nearestDistance = double.MaxValue;
                        foreach (Tuple<Guid, Point3D, string, double> candidate in nearby)
                        {
                            double distance = aperturePoint.Distance(candidate.Item2);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearest = candidate;
                            }
                        }

                        double anchorZ = nearest.Item2.Z;
                        sameFloor = nearby.FindAll(x => Math.Abs(x.Item2.Z - anchorZ) <= tolerance);
                        sameFloor.Sort((x, y) => x.Item4.CompareTo(y.Item4));
                    }

                    Log("  co-located=" + nearby.Count + " sameFloor=" + sameFloor.Count + (sameFloor.Count == 0 ? "" : " areas=[" + string.Join(", ", sameFloor.Select(x => x.Item4.ToString("0.###"))) + "]"));

                    // pane = smallest same-floor surface; frame = next smallest, but only if it is
                    // still window-sized (<= 3x the pane area) so a co-located WALL surface — far
                    // larger — is never mistaken for a frame.
                    Tuple<Guid, Point3D, string, double> paneCandidate = sameFloor.Count >= 1 ? sameFloor[0] : null;
                    Tuple<Guid, Point3D, string, double> frameCandidate = (sameFloor.Count >= 2 && sameFloor[1].Item4 <= sameFloor[0].Item4 * 3.0) ? sameFloor[1] : null;

                    Tuple<AperturePart, Tuple<Guid, Point3D, string, double>>[] assignments = new Tuple<AperturePart, Tuple<Guid, Point3D, string, double>>[]
                    {
                        Tuple.Create(AperturePart.Pane, paneCandidate),
                        Tuple.Create(AperturePart.Frame, frameCandidate),
                    };

                    foreach (Tuple<AperturePart, Tuple<Guid, Point3D, string, double>> assignment in assignments)
                    {
                        AperturePart part = assignment.Item1;
                        Tuple<Guid, Point3D, string, double> chosen = assignment.Item2;

                        if (chosen == null)
                        {
                            Log("  " + part + " -> SKIP (no suitable co-located candidate)");
                            continue;
                        }

                        claimed.Add(chosen.Item1);
                        SolarCoverageSimulationResult source = dictionary_CoverageByLinkedFace3DGuid[chosen.Item1];
                        SolarCoverageSimulationResult newCoverageResult = new SolarCoverageSimulationResult(string.Format("{0} {1}", aperture.Name, part.Sufix()), "TAS", aperture.Guid.ToString(), source);

                        adjacencyCluster.AddObject(aperture);
                        adjacencyCluster.AddObject(newCoverageResult);
                        adjacencyCluster.AddRelation(aperture, newCoverageResult);

                        if (part == AperturePart.Pane) { paneMatched++; } else if (part == AperturePart.Frame) { frameMatched++; }

                        Log("  " + part + " -> matched [" + chosen.Item1.ToString().Substring(0, 8) + "] area=" + chosen.Item4.ToString("0.###") + " ref=" + (chosen.Item3 ?? "<null>"));
                    }
                }
            }

            // Greedy nearest-neighbour match: each remaining (wall) LinkedFace3D is claimed by at
            // most one panel.
            foreach (Panel panel in panels)
            {
                Point3D panelPoint = panel?.GetInternalPoint3D();
                if (panelPoint == null)
                {
                    continue;
                }

                Guid bestGuid = Guid.Empty;
                double bestDistance = double.MaxValue;
                foreach (Tuple<Guid, Point3D, string, double> candidate in candidates)
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
                panelMatched++;
            }

            Log("-- Summary --");
            Log("paneMatched=" + paneMatched + "  frameMatched=" + frameMatched + "  panelMatched=" + panelMatched + "  claimed=" + claimed.Count + "/" + candidates.Count);

            if (logEnabled)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_CopyResults.log");
                    System.IO.File.WriteAllText(logPath, log.ToString());
                }
                catch
                {
                    // Diagnostics must never break the import.
                }
            }

            return new AnalyticalModel(analyticalModel, adjacencyCluster);
        }
    }
}