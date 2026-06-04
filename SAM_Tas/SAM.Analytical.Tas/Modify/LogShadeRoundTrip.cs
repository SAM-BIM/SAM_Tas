// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using SAM.Geometry.Object.Spatial;
using SAM.Geometry.SolarCalculator;
using SAM.Geometry.Spatial;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        /// <summary>
        /// Diagnostic only (gated on the <c>SAM_DEBUG</c> env var, set by the SAMAnalytical.TBD <c>_debug_</c>
        /// toggle). Re-opens the just-saved TBD at <paramref name="path"/> read-only, reads its shading back
        /// through the SAME path SAMAnalytical.FromTBD uses (<see cref="Create.SolarModel(TBD.Building)"/>),
        /// and compares it — per surface and per hour — against the SAM source coverage attached to
        /// <paramref name="analyticalModel"/> (<see cref="AnalyticalModelParameter.SolarModel"/>).
        /// <para>
        /// This captures the full write→save→reopen→read round-trip that the option-2 workflow
        /// (SolarSimulation → ToTBD → FromTBD) goes through, so the coverage drift it introduces can be
        /// measured directly. The report is appended to <c>%TEMP%\SAM_ToTBD.log</c> and therefore shows up on
        /// the component's <c>debugLog</c> output. Never throws — diagnostics must not break the export.
        /// </para>
        /// </summary>
        public static void LogShadeRoundTrip(string path, AnalyticalModel analyticalModel)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SAM_DEBUG")))
            {
                return;
            }

            string logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SAM_ToTBD.log");
            StringBuilder log = new StringBuilder();
            void Log(string message) { log.AppendLine(message); }

            Log("");
            Log("=== Round-trip check (SAM source coverage vs TBD read-back) ===");

            try
            {
                if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path) || analyticalModel == null)
                {
                    Log("  SKIP — missing TBD path or AnalyticalModel");
                    Flush(logPath, log);
                    return;
                }

                SolarModel sourceSolarModel = analyticalModel.GetValue<SolarModel>(Analytical.AnalyticalModelParameter.SolarModel);
                if (sourceSolarModel == null)
                {
                    Log("  SKIP — no source SolarModel attached to the AnalyticalModel (nothing was written to compare against)");
                    Flush(logPath, log);
                    return;
                }

                List<ShadeRoundTripSurface> sourceSurfaces = ShadeRoundTripSurfaces(sourceSolarModel);

                // Re-open the saved TBD exactly as FromTBD does — read-WRITE (readOnly = false), NOT
                // read-only. This matters: TAS's read-write open processes manually-written SurfaceShade
                // (shifting it vs a read-only reopen), so only this mode reproduces the real FromTBD read.
                // Dispose only close()s (never save()s), so the file is not modified.
                List<ShadeRoundTripSurface> readBackSurfaces = null;
                using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path, false))
                {
                    TBD.Building building = sAMTBDDocument?.TBDDocument?.Building;
                    SolarModel readBackSolarModel = building == null ? null : Create.SolarModel(building);
                    readBackSurfaces = ShadeRoundTripSurfaces(readBackSolarModel);
                }

                Log(string.Format("  Source surfaces with coverage: {0}   Read-back surfaces with coverage: {1}", sourceSurfaces.Count, readBackSurfaces.Count));
                if (sourceSurfaces.Count == 0 || readBackSurfaces.Count == 0)
                {
                    Log("  SKIP — one side has no coverage surfaces to match");
                    Flush(logPath, log);
                    return;
                }

                const double tolerance = 0.5;
                HashSet<int> claimed = new HashSet<int>();
                int matched = 0;
                int unmatchedSource = 0;
                double sumAbsAll = 0.0;
                double sumSignedAll = 0.0;
                int overlapHoursAll = 0;
                int excellent = 0, good = 0, fair = 0, poor = 0;
                double worstPairMeanAbs = 0.0;

                Log("  --- Per matched surface (read-back minus source) ---");
                foreach (ShadeRoundTripSurface source in sourceSurfaces)
                {
                    int best = -1;
                    double bestDistance = double.MaxValue;
                    for (int i = 0; i < readBackSurfaces.Count; i++)
                    {
                        if (claimed.Contains(i)) continue;
                        double distance = source.InternalPoint3D.Distance(readBackSurfaces[i].InternalPoint3D);
                        if (distance <= tolerance && distance < bestDistance)
                        {
                            bestDistance = distance;
                            best = i;
                        }
                    }

                    if (best == -1)
                    {
                        unmatchedSource++;
                        continue;
                    }

                    claimed.Add(best);
                    matched++;

                    Dictionary<int, double> sourceByHour = ShadeRoundTripHourMap(source.Result);
                    Dictionary<int, double> readByHour = ShadeRoundTripHourMap(readBackSurfaces[best].Result);

                    double sumAbs = 0.0;
                    double sumSigned = 0.0;
                    int overlap = 0;
                    double maxAbs = 0.0;
                    int worstKey = -1;
                    double worstSource = 0.0, worstRead = 0.0;
                    foreach (KeyValuePair<int, double> keyValuePair in sourceByHour)
                    {
                        if (!readByHour.TryGetValue(keyValuePair.Key, out double readValue)) continue;
                        double delta = readValue - keyValuePair.Value;
                        sumAbs += Math.Abs(delta);
                        sumSigned += delta;
                        overlap++;
                        if (Math.Abs(delta) > maxAbs)
                        {
                            maxAbs = Math.Abs(delta);
                            worstKey = keyValuePair.Key;
                            worstSource = keyValuePair.Value;
                            worstRead = readValue;
                        }
                    }

                    double meanAbs = overlap == 0 ? double.NaN : sumAbs / overlap;
                    sumAbsAll += sumAbs;
                    sumSignedAll += sumSigned;
                    overlapHoursAll += overlap;

                    if (!double.IsNaN(meanAbs))
                    {
                        if (meanAbs < 0.01) excellent++;
                        else if (meanAbs < 0.05) good++;
                        else if (meanAbs < 0.10) fair++;
                        else poor++;
                        if (meanAbs > worstPairMeanAbs) worstPairMeanAbs = meanAbs;
                    }

                    string worst = worstKey < 0
                        ? ""
                        : string.Format("  worst@{0:00}/{1:00} {2:00}h: src={3:0.000} read={4:0.000}", (worstKey >> 16) & 0xFF, (worstKey >> 8) & 0xFF, worstKey & 0xFF, worstSource, worstRead);

                    Log(string.Format("  centroid={0} area={1:0.###}  srcHrs={2} readHrs={3} overlap={4}  meanAbs={5:0.0000} maxAbs={6:0.0000}{7}",
                        source.InternalPoint3D, source.Area, sourceByHour.Count, readByHour.Count, overlap, meanAbs, maxAbs, worst));
                }

                double overallMeanAbs = overlapHoursAll == 0 ? double.NaN : sumAbsAll / overlapHoursAll;
                double overallSigned = overlapHoursAll == 0 ? double.NaN : sumSignedAll / overlapHoursAll;

                Log("  --- Summary ---");
                Log(string.Format("  matched pairs: {0}   unmatched source (e.g. Shade occluders with no TBD surface): {1}   unmatched read-back: {2}", matched, unmatchedSource, readBackSurfaces.Count - claimed.Count));
                Log(string.Format("  delta distribution (meanAbs/pair): <0.01 {0} | 0.01-0.05 {1} | 0.05-0.10 {2} | >0.10 {3}", excellent, good, fair, poor));
                Log(string.Format("  overallMeanAbsDelta: {0:0.0000}   overallSignedDelta (read-src): {1:0.0000}   (across {2} pairs, {3} overlapping hours)", overallMeanAbs, overallSigned, matched, overlapHoursAll));
                Log(string.Format("  worst pair meanAbs: {0:0.0000}", worstPairMeanAbs));
                Log("  NOTE: read-back reopens the saved TBD and reads only TAS's representative shade-day calendar (GetShadeDays). A non-zero delta here means the value SAM wrote is not what TAS returns on those days (rep-day mismatch / interpolation), i.e. the option-2 drift is reproduced on the ToTBD side.");
            }
            catch (Exception exception)
            {
                Log("  ERROR during round-trip diagnostic: " + exception.Message);
            }

            Flush(logPath, log);
        }

        private static void Flush(string logPath, StringBuilder log)
        {
            try { System.IO.File.AppendAllText(logPath, log.ToString()); } catch { }
        }

        // (internalPoint, area, coverage result) for every SolarModel surface that carries a coverage result.
        private struct ShadeRoundTripSurface
        {
            public Point3D InternalPoint3D;
            public double Area;
            public SolarCoverageSimulationResult Result;
        }

        private static List<ShadeRoundTripSurface> ShadeRoundTripSurfaces(SolarModel solarModel)
        {
            List<ShadeRoundTripSurface> result = new List<ShadeRoundTripSurface>();
            if (solarModel == null)
            {
                return result;
            }

            Dictionary<string, SolarCoverageSimulationResult> byReference = new Dictionary<string, SolarCoverageSimulationResult>();
            foreach (SolarCoverageSimulationResult coverageResult in solarModel.SolarCoverageSimulationResults ?? new List<SolarCoverageSimulationResult>())
            {
                if (coverageResult?.Reference != null)
                {
                    byReference[coverageResult.Reference] = coverageResult;
                }
            }

            foreach (LinkedFace3D linkedFace3D in solarModel.GetLinkedFace3Ds() ?? new List<LinkedFace3D>())
            {
                if (linkedFace3D?.Face3D == null)
                {
                    continue;
                }

                if (!byReference.TryGetValue(linkedFace3D.Guid.ToString(), out SolarCoverageSimulationResult coverageResult))
                {
                    continue;
                }

                Point3D internalPoint3D = linkedFace3D.Face3D.InternalPoint3D();
                if (internalPoint3D == null)
                {
                    continue;
                }

                result.Add(new ShadeRoundTripSurface { InternalPoint3D = internalPoint3D, Area = linkedFace3D.Face3D.GetArea(), Result = coverageResult });
            }

            return result;
        }

        // (month<<16)|(day<<8)|hour -> coverage, ignoring the year (matches CompareSolarCoverage's alignment).
        private static Dictionary<int, double> ShadeRoundTripHourMap(SolarCoverageSimulationResult coverageResult)
        {
            Dictionary<int, double> result = new Dictionary<int, double>();
            if (coverageResult?.Coverage == null)
            {
                return result;
            }

            foreach (Tuple<DateTime, double> tuple in coverageResult.Coverage)
            {
                if (tuple == null || double.IsNaN(tuple.Item2))
                {
                    continue;
                }

                DateTime dateTime = tuple.Item1;
                int key = (dateTime.Month << 16) | (dateTime.Day << 8) | dateTime.Hour;
                if (!result.ContainsKey(key))
                {
                    result[key] = tuple.Item2;
                }
            }

            return result;
        }
    }
}
