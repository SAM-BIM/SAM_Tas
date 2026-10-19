// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using SAM.Core.Tas;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last)
        {
            return Simulate(path_TBD, path_TSD, day_First, day_Last, SizingType.Undefined, null);
        }

        /// <summary>
        /// Simulates a TBD to a separate TSD. Decided by <see cref="SimulationEvidence"/>: true only when TAS returned
        /// -1 and the run left a fresh, non-trivial TSD and no error log. A refusal (TAS returns 0 and still writes a
        /// stub TSD) is false. Use the overload with the evidence <c>out</c> parameter to learn why.
        /// </summary>
        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last, SizingType sizingType, IEnumerable<SurfaceOutputSpec> surfaceOutputSpecs)
        {
            Simulate(path_TBD, path_TSD, day_First, day_Last, sizingType, surfaceOutputSpecs, out SimulationEvidence simulationEvidence);

            return simulationEvidence.Completed;
        }

        public static bool Simulate(SAMTBDDocument sAMTBDDocument, string path_TSD, int day_First, int day_Last)
        {
            return Simulate(sAMTBDDocument?.TBDDocument, path_TSD, day_First, day_Last);
        }

        public static bool Simulate(TBD.TBDDocument tBDDocument, string path_TSD, int day_First, int day_Last)
        {
            if (tBDDocument == null || string.IsNullOrWhiteSpace(path_TSD))
                return false;

            return SimulateToTSD(() => tBDDocument.simulate(day_First, day_Last, 0, 1, 0, 0, path_TSD, 1, 0), path_TSD, out SimulationEvidence _);
        }

        /// <summary>
        /// The document-level decision, held to the same <see cref="SimulationEvidence"/> rules as the path overloads:
        /// a leftover TSD and error log are cleared first, then success needs TAS to return -1, the TSD to be fresh and
        /// non-trivial, and no error log for this run. A <c>TBDDocument</c> carries no path, so the record is keyed on
        /// the TSD: TAS writes <c>&lt;tsd basename&gt;_error_log.txt</c> beside the TSD (measured 2026-10-19 with a TBD
        /// and TSD of different names and folders). <paramref name="simulate"/> performs the COM call; a throw from it
        /// propagates, as before.
        /// </summary>
        private static bool SimulateToTSD(System.Func<int> simulate, string path_TSD, out SimulationEvidence simulationEvidence)
        {
            simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TSD, path_TSD);
            simulationEvidence.Prepare(System.DateTime.UtcNow);

            if (simulationEvidence.Refusals.Count != 0)
            {
                return false;
            }

            int nativeReturnCode = simulate();

            bool unlocked = Core.Query.WaitToUnlock(path_TSD);

            simulationEvidence.RecordCallReturned((object)nativeReturnCode);
            simulationEvidence.Conclude();

            return unlocked && simulationEvidence.Completed;
        }

        /// <summary>
        /// Simulates a TBD to a <b>separate</b> TSD and reports what the run actually left behind.
        /// <para>
        /// The overloads above answer <c>Core.Query.WaitToUnlock(path_TSD)</c>, which returns true as soon as
        /// an <i>already existing</i> file is unlocked. A leftover TSD from an earlier run therefore made a
        /// failed simulation report success - and TAS reports a rejected model by writing
        /// <c>&lt;basename&gt;_error_log.txt</c> and returning normally from the COM call, which nothing read.
        /// </para>
        /// <para>
        /// Here <c>WaitToUnlock</c> is demoted to what it is - a wait - and success is decided by evidence:
        /// the expected TSD was deleted first, no error log is attributable to this run, and the file that
        /// now exists post-dates the run and is not the ~22-byte stub TAS writes when no weather data is
        /// installed.
        /// </para>
        /// </summary>
        /// <param name="simulationEvidence">What the run left behind. Never null.</param>
        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last, out SimulationEvidence simulationEvidence)
        {
            return Simulate(path_TBD, path_TSD, day_First, day_Last, SizingType.Undefined, null, out simulationEvidence);
        }

        /// <summary>
        /// As <see cref="Simulate(string, string, int, int, out SimulationEvidence)"/>, first applying
        /// <paramref name="sizingType"/> and <paramref name="surfaceOutputSpecs"/> to the TBD (saved before the run when
        /// either is given). The integer <c>TBDDocument.simulate</c> returns is recorded: -1 (ran) lets the run be judged
        /// on its output and error log, 0 (refused) or any other value refuses. The TBD is saved after the call only
        /// when TAS returned -1.
        /// </summary>
        /// <param name="sizingType"><see cref="SizingType.Undefined"/> leaves the zones' sizing as it is.</param>
        /// <param name="surfaceOutputSpecs">Null or empty leaves the surface outputs as they are.</param>
        /// <param name="simulationEvidence">What the run left behind. Never null.</param>
        public static bool Simulate(string path_TBD, string path_TSD, int day_First, int day_Last, SizingType sizingType, IEnumerable<SurfaceOutputSpec> surfaceOutputSpecs, out SimulationEvidence simulationEvidence)
        {
            simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);

            if (string.IsNullOrWhiteSpace(path_TBD) || string.IsNullOrWhiteSpace(path_TSD))
            {
                simulationEvidence.Refuse("Both a TBD and a TSD path are required.");
                return false;
            }

            if (!System.IO.File.Exists(path_TBD))
            {
                simulationEvidence.Refuse(string.Concat("The TBD does not exist: ", path_TBD));
                return false;
            }

            simulationEvidence.Prepare(System.DateTime.UtcNow);

            if (simulationEvidence.Refusals.Count != 0)
            {
                return false;
            }

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
            {
                TBD.TBDDocument tBDDocument = sAMTBDDocument?.TBDDocument;

                if (tBDDocument == null)
                {
                    simulationEvidence.RecordCallFailed(string.Concat("the document could not be opened: ", path_TBD));
                    return false;
                }

                if (tBDDocument.Building == null)
                {
                    simulationEvidence.RecordCallFailed(string.Concat("the document carries no building: ", path_TBD));
                    return false;
                }

                try
                {
                    bool dirty = false;

                    if (sizingType != SizingType.Undefined)
                    {
                        SetSizingTypes(tBDDocument.Building, sizingType);
                        dirty = true;
                    }

                    SurfaceOutputSpec firstSpec = null;
                    if (surfaceOutputSpecs != null)
                    {
                        foreach (SurfaceOutputSpec spec in surfaceOutputSpecs)
                        {
                            if (spec == null)
                                continue;
                            if (firstSpec == null)
                                firstSpec = spec;
                        }
                    }

                    if (firstSpec != null)
                    {
                        Core.Tas.Modify.UpdateSurfaceOutputSpecs(tBDDocument, surfaceOutputSpecs);
                        Core.Tas.Modify.AssignSurfaceOutputSpecs(tBDDocument, firstSpec.Name);
                        dirty = true;
                    }

                    if (dirty)
                    {
                        sAMTBDDocument.Save();
                    }

                    int nativeReturnCode = tBDDocument.simulate(day_First, day_Last, 0, 1, 0, 0, path_TSD, 1, 0);

                    // A wait, not a verdict: the file may still be being written when the call returns.
                    Core.Query.WaitToUnlock(path_TSD);

                    if (simulationEvidence.RecordCallReturned((object)nativeReturnCode))
                    {
                        sAMTBDDocument.Save();
                    }
                }
                catch (System.Exception exception)
                {
                    simulationEvidence.RecordCallFailed(
                        string.Format("{0}: {1}", exception.GetType().Name, exception.Message));
                    return false;
                }
            }

            simulationEvidence.Conclude();

            return simulationEvidence.Completed;
        }
    }
}