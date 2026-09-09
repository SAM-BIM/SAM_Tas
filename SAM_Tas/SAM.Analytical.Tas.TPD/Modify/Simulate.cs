// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.IO;
using TPD;

namespace SAM.Analytical.Tas.TPD
{
    public static partial class Modify
    {
        /// <summary>
        /// Simulates a TPD and answers whether the run is <b>evidenced</b>, not merely whether it was
        /// attempted.
        /// <para>
        /// This used to return a literal <c>true</c>. It did so even when <c>EnergyCentre</c> was null and the
        /// whole simulation had been silently skipped: measured on real TAS, a document holding zero plant
        /// rooms, zero systems and zero zones answered <c>true</c>, having produced no result of any kind.
        /// </para>
        /// <para>
        /// A TPD writes its results back into <b>the same document</b>, so the document must never be deleted
        /// beforehand, and a changed file proves nothing - <c>Save()</c> writes whether or not anything was
        /// produced. That makes this shape's evidence necessarily incomplete here: the decisive stage is
        /// reading the results back and reconciling them against the expected room set, which only the caller
        /// holding that set can do. Use the <c>out SimulationEvidence</c> overload and call
        /// <c>RecordResultsReconciled</c> once that check passes; until then
        /// <c>SimulationEvidence.Completed</c> stays false.
        /// </para>
        /// </summary>
        public static bool Simulate(string path_TPD, int startHour, int endHour)
        {
            return Simulate(path_TPD, startHour, endHour, out SimulationEvidence simulationEvidence)
                && simulationEvidence != null;
        }

        /// <summary>
        /// Simulates a TPD in place and reports every stage of what it left behind.
        /// </summary>
        /// <param name="path_TPD">The document to simulate. It is written back into and is never deleted.</param>
        /// <param name="startHour">0-based first hour. TAS is 1-based, so <c>+1</c> is applied on the way in.</param>
        /// <param name="endHour">0-based last hour, inclusive.</param>
        /// <param name="simulationEvidence">
        /// What the run left behind. Never null. <c>Completed</c> stays false until the caller has reconciled
        /// the results, because for this shape a saved file is not evidence.
        /// </param>
        /// <returns>
        /// True when every stage this method can judge passed. That is <b>not</b> the same as the simulation
        /// having produced usable results - see the remarks on the other overload.
        /// </returns>
        public static bool Simulate(string path_TPD, int startHour, int endHour, out SimulationEvidence simulationEvidence)
        {
            simulationEvidence = new SimulationEvidence(SimulationOutputShape.InPlaceDocument, path_TPD, null);

            if (string.IsNullOrWhiteSpace(path_TPD))
            {
                simulationEvidence.Refuse("No TPD path was given.");
                return false;
            }

            if (!File.Exists(path_TPD))
            {
                simulationEvidence.Refuse(string.Concat("The TPD does not exist: ", path_TPD));
                return false;
            }

            if (endHour < startHour)
            {
                simulationEvidence.Refuse(
                    string.Format("The requested period ends before it starts: {0}..{1}.", startHour, endHour));
                return false;
            }

            simulationEvidence.Prepare(DateTime.UtcNow);

            if (simulationEvidence.Refusals.Count != 0)
            {
                // Preparation refused - a stale error log that could be neither cleared nor dated, say.
                return false;
            }

            using (SAMTPDDocument sAMTPDDocument = new SAMTPDDocument(path_TPD))
            {
                TPDDoc tPDDoc = sAMTPDDocument.TPDDocument;

                if (tPDDoc == null)
                {
                    simulationEvidence.RecordCallFailed(string.Concat("the document could not be opened: ", path_TPD));
                    return false;
                }

                if (tPDDoc.EnergyCentre == null)
                {
                    // The silent-skip path. It used to fall through to `return true`.
                    simulationEvidence.RecordCallFailed(
                        string.Concat("the document carries no energy centre, so there is nothing to simulate: ", path_TPD));
                    return false;
                }

                try
                {
                    // ITPD.Simulate is declared as returning a STRING, and this call used to discard it.
                    // Measured on licensed TAS, that string diagnoses why nothing ran - "Plant room has no
                    // components", "Plant Room Has Errors", "Failed to open the TSD file", "Sizing Flow
                    // Failed". It is preserved verbatim as evidence.
                    //
                    // It is NOT treated as "non-empty means failure": no successful run has been observed
                    // through this route, so TAS may return a status on success too. Only a measured
                    // failure refuses here; anything else is recorded and left to the results
                    // reconciliation, which is the decisive gate.
                    string returned = tPDDoc.Simulate(startHour + 1, endHour + 1, 0);

                    tPDDoc.Save();

                    if (!simulationEvidence.RecordCallReturned(returned))
                    {
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    simulationEvidence.RecordCallFailed(
                        string.Format("{0}: {1}", exception.GetType().Name, exception.Message));
                    return false;
                }
            }

            simulationEvidence.Conclude();

            return simulationEvidence.Refusals.Count == 0;
        }
    }
}
