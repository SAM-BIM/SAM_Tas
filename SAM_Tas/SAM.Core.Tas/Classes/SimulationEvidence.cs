// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;

namespace SAM.Core.Tas
{
    /// <summary>
    /// What a TAS run actually left behind, so that "it simulated" is a finding rather than an assumption.
    /// <para>
    /// <b>Why this exists.</b> Three separate false positives sat on the Part O route. The TPD entry point
    /// returned a literal <c>true</c> even when the energy centre was null and nothing ran at all. The
    /// conversion returned a literal <c>true</c> even when it had lost every system. And the TBD entry point
    /// returned <c>Core.Query.WaitToUnlock(path_TSD)</c>, which answers <c>true</c> as soon as an
    /// <i>already existing</i> file is unlocked - so a leftover TSD from an earlier run made a failed
    /// simulation report success. TAS reports a rejected model by writing
    /// <c>&lt;basename&gt;_error_log.txt</c> and returning normally from the COM call, and nothing in this
    /// repository read that log.
    /// </para>
    /// <para>
    /// <b>None of these count as evidence:</b> the output file existing, the file being unlocked,
    /// <c>Save()</c> having been called, or a literal <c>true</c>. What counts depends on the
    /// <see cref="SimulationOutputShape"/> - see <see cref="Completed"/>.
    /// </para>
    /// <para>
    /// Deliberately free of TAS COM types, so the whole decision can be exercised without a TAS licence,
    /// an installed TAS or a COM server. The caller performs the COM call and reports what it observed;
    /// this type decides what that adds up to.
    /// </para>
    /// </summary>
    public class SimulationEvidence
    {
        private readonly List<string> refusals = new List<string>();
        private readonly List<string> notes = new List<string>();

        /// <summary>The ~22-byte TSD TAS writes when no weather data is installed. Anything at or below this is not a result set.</summary>
        public const long TrivialOutputLengthThreshold = 1024;

        /// <summary><c>TBDDocument.simulate</c> returned this: the simulation ran. Still judged on its output and error log.</summary>
        public const int NativeReturnCode_Ran = -1;

        /// <summary><c>TBDDocument.simulate</c> returned this: TAS refused the simulation (it still writes a stub TSD and usually an error log).</summary>
        public const int NativeReturnCode_Refused = 0;

        /// <summary>
        /// Opens an evidence record for a run about to be made.
        /// </summary>
        /// <param name="simulationOutputShape">Where this run puts its results. <c>Undefined</c> refuses.</param>
        /// <param name="path_Document">The TBD or TPD being simulated.</param>
        /// <param name="path_Output">The TSD, for <c>SeparateOutputFile</c>. Must be null for <c>InPlaceDocument</c>: nothing about a separate file could be asserted honestly there.</param>
        public SimulationEvidence(SimulationOutputShape simulationOutputShape, string path_Document, string path_Output)
        {
            OutputShape = simulationOutputShape;
            Path_Document = path_Document;
            Path_Output = path_Output;

            if (simulationOutputShape == SimulationOutputShape.Undefined)
            {
                refusals.Add("The simulation output shape was not stated, so no evidence rule applies.");
            }

            if (string.IsNullOrWhiteSpace(path_Document))
            {
                refusals.Add("No document path was given, so there is nothing to attribute a run to.");
            }

            if (simulationOutputShape == SimulationOutputShape.SeparateOutputFile && string.IsNullOrWhiteSpace(path_Output))
            {
                refusals.Add("A separate-output run must name the output file it is expected to write.");
            }

            if (simulationOutputShape == SimulationOutputShape.InPlaceDocument && !string.IsNullOrWhiteSpace(path_Output))
            {
                refusals.Add(
                    "An in-place run must not name a separate output file: its results go back into the "
                    + "document it simulated, and claiming a distinct output would be false.");
            }

            // TAS writes the log beside the file it writes, under that file's basename: for a separate-output run that is
            // the TSD (measured 2026-10-19: alpha.tbd simulated to beta.tsd logged beta_error_log.txt beside the TSD).
            Path_ErrorLog = ErrorLogPath(
                simulationOutputShape == SimulationOutputShape.SeparateOutputFile && !string.IsNullOrWhiteSpace(path_Output)
                    ? path_Output
                    : path_Document);
        }

        /// <summary>Where this run writes its results.</summary>
        public SimulationOutputShape OutputShape { get; }

        /// <summary>The TBD or TPD that was simulated.</summary>
        public string Path_Document { get; }

        /// <summary>The TSD for a separate-output run; <c>null</c> for an in-place run.</summary>
        public string Path_Output { get; }

        /// <summary>
        /// The file that carries the result for this shape - the TSD for a separate-output run, the document
        /// itself for an in-place run. This is what the before/after timestamps below describe.
        /// </summary>
        public string Path_Result
        {
            get
            {
                return OutputShape == SimulationOutputShape.InPlaceDocument ? Path_Document : Path_Output;
            }
        }

        /// <summary>Captured immediately before the COM call, and the line every staleness test is drawn against.</summary>
        public DateTime UtcStarted { get; private set; }

        /// <summary>Pre-call write time of <see cref="Path_Result"/>, or null when it did not exist.</summary>
        public DateTime? UtcResultBefore { get; private set; }

        /// <summary>Pre-call length of <see cref="Path_Result"/>; 0 when it did not exist.</summary>
        public long ResultLengthBefore { get; private set; }

        /// <summary>Post-call write time of <see cref="Path_Result"/>, or null when it does not exist.</summary>
        public DateTime? UtcResultAfter { get; private set; }

        /// <summary>Post-call length of <see cref="Path_Result"/>; 0 when it does not exist.</summary>
        public long ResultLengthAfter { get; private set; }

        /// <summary>
        /// Where TAS states a rejected model: <c>&lt;TSD basename&gt;_error_log.txt</c> beside the TSD for a separate-output
        /// run, <c>&lt;document basename&gt;_error_log.txt</c> beside the document for an in-place run.
        /// </summary>
        public string Path_ErrorLog { get; }

        /// <summary>Whether an error log was present before the run, and therefore could be mistaken for this run's.</summary>
        public bool ErrorLogPresentBefore { get; private set; }

        /// <summary>Pre-call write time of an error log that could not be deleted, so it can be excluded by timestamp instead.</summary>
        public DateTime? UtcErrorLogBefore { get; private set; }

        /// <summary>The error log text attributable to <i>this</i> run, or null when there is none.</summary>
        public string ErrorLogText { get; private set; }

        /// <summary>Whether the COM call returned rather than throwing.</summary>
        public bool CallReturned { get; private set; }

        /// <summary>
        /// The raw string <c>ITPD.Simulate</c> returned, preserved verbatim as evidence whatever it said.
        /// <c>null</c> for a shape whose call returns nothing, or when the call threw.
        /// </summary>
        public string NativeDiagnostic { get; private set; }

        /// <summary>
        /// The integer <c>TBDDocument.simulate</c> returned (<see cref="NativeReturnCode_Ran"/> or
        /// <see cref="NativeReturnCode_Refused"/>, or an unexpected value, which refuses). <c>null</c> when no return code
        /// was recorded, or when what came back was not an integer - the raw value is then in the refusal text.
        /// </summary>
        public int? NativeReturnCode { get; private set; }

        /// <summary>Whether <see cref="RecordCallReturned(object)"/> was called, whatever it was given.</summary>
        public bool NativeReturnCodeRecorded { get; private set; }

        /// <summary>What <see cref="NativeDiagnostic"/> was classified as. See <see cref="SimulationDiagnostic"/>.</summary>
        public SimulationDiagnosticKind NativeDiagnosticKind { get; private set; } = SimulationDiagnosticKind.Silent;

        /// <summary>Whether the caller has confirmed the results read back and reconciled. In-place runs turn on this.</summary>
        public bool ResultsReconciled { get; private set; }

        /// <summary>Stages passed, in order, for the audit trail.</summary>
        public List<string> Notes
        {
            get { return new List<string>(notes); }
        }

        /// <summary>Every reason this run is not evidence of a successful simulation.</summary>
        public List<string> Refusals
        {
            get { return new List<string>(refusals); }
        }

        /// <summary>
        /// True only when every stage required for <see cref="OutputShape"/> passed. For an in-place run that
        /// includes <see cref="ResultsReconciled"/>: a saved file is explicitly not enough.
        /// </summary>
        public bool Completed
        {
            get { return refusals.Count == 0 && CallReturned && (OutputShape != SimulationOutputShape.InPlaceDocument || ResultsReconciled); }
        }

        /// <summary>Records a refusal. A run that has refused cannot later be talked into success.</summary>
        public void Refuse(string refusal)
        {
            if (!string.IsNullOrWhiteSpace(refusal))
            {
                refusals.Add(refusal);
            }
        }

        /// <summary>Records a stage that passed.</summary>
        public void Note(string note)
        {
            if (!string.IsNullOrWhiteSpace(note))
            {
                notes.Add(note);
            }
        }

        /// <summary>
        /// The path TAS writes a failure to for a given file: <c>&lt;basename&gt;_error_log.txt</c> beside it. For a
        /// separate-output run pass the TSD (see <see cref="Path_ErrorLog"/>).
        /// </summary>
        public static string ErrorLogPath(string path_Document)
        {
            if (string.IsNullOrWhiteSpace(path_Document))
            {
                return null;
            }

            string directory = Path.GetDirectoryName(path_Document);
            string fileName = Path.GetFileNameWithoutExtension(path_Document);

            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            return Path.Combine(directory ?? string.Empty, string.Concat(fileName, "_error_log.txt"));
        }

        /// <summary>
        /// Stage 1. Clears the ground so that whatever is found afterwards can only be this run's.
        /// <para>
        /// For a separate-output run the expected output is deleted; a file that cannot be deleted refuses
        /// rather than being simulated over, because a leftover would otherwise be read as this run's result.
        /// For an in-place run the document is <b>never</b> deleted - it is the input - so its pre-call
        /// timestamp and length are recorded instead.
        /// </para>
        /// <para>
        /// In both shapes a pre-existing error log is deleted, or, if the filesystem refuses, its write time
        /// is recorded so a stale log can be excluded by timestamp. A stale log is never read as this run's
        /// evidence, and never silently ignored either.
        /// </para>
        /// </summary>
        public void Prepare(DateTime utcStarted)
        {
            UtcStarted = utcStarted;

            string path_Result = Path_Result;

            if (OutputShape == SimulationOutputShape.SeparateOutputFile && !string.IsNullOrWhiteSpace(path_Result))
            {
                if (File.Exists(path_Result))
                {
                    try
                    {
                        File.Delete(path_Result);
                        Note(string.Concat("Pre-existing output deleted so it cannot be read as this run's: ", path_Result));
                    }
                    catch (Exception exception)
                    {
                        Refuse(
                            string.Format(
                                "A pre-existing output could not be deleted before the run, so a leftover could be "
                                + "mistaken for this run's result: {0} ({1}).",
                                path_Result,
                                exception.Message));
                    }
                }
            }
            else if (OutputShape == SimulationOutputShape.InPlaceDocument && !string.IsNullOrWhiteSpace(path_Result))
            {
                if (!File.Exists(path_Result))
                {
                    Refuse(string.Concat("The document to be simulated in place does not exist: ", path_Result));
                }
                else
                {
                    FileInfo fileInfo = new FileInfo(path_Result);
                    UtcResultBefore = fileInfo.LastWriteTimeUtc;
                    ResultLengthBefore = fileInfo.Length;
                    Note(
                        string.Format(
                            "In-place document state captured before the run: {0} bytes, written {1:o}.",
                            ResultLengthBefore,
                            UtcResultBefore));
                }
            }

            PrepareErrorLog();
        }

        private void PrepareErrorLog()
        {
            if (string.IsNullOrWhiteSpace(Path_ErrorLog) || !File.Exists(Path_ErrorLog))
            {
                ErrorLogPresentBefore = false;
                return;
            }

            ErrorLogPresentBefore = true;

            try
            {
                File.Delete(Path_ErrorLog);
                ErrorLogPresentBefore = false;
                Note("A stale TAS error log was deleted before the run.");
            }
            catch
            {
                try
                {
                    UtcErrorLogBefore = new FileInfo(Path_ErrorLog).LastWriteTimeUtc;
                    Note(
                        string.Format(
                            "A stale TAS error log could not be deleted; its write time {0:o} is recorded so it can "
                            + "be excluded by timestamp instead.",
                            UtcErrorLogBefore));
                }
                catch (Exception exception)
                {
                    Refuse(
                        string.Format(
                            "A pre-existing TAS error log could neither be deleted nor timestamped, so this run's "
                            + "failures could not be told from an earlier run's: {0} ({1}).",
                            Path_ErrorLog,
                            exception.Message));
                }
            }
        }

        /// <summary>Stage 2. The COM call returned rather than throwing.</summary>
        public void RecordCallReturned()
        {
            CallReturned = true;
            Note("The TAS call returned without throwing.");
        }

        /// <summary>
        /// Stage 2 for a call that answers with a diagnostic string. The string is preserved verbatim
        /// whatever it says; only a <b>measured</b> failure refuses.
        /// <para>
        /// An unrecognised answer is recorded and decides nothing, because no successful run has yet been
        /// observed and TAS may well return a status on success. The decisive gate is the results
        /// reconciliation, not this string.
        /// </para>
        /// </summary>
        /// <returns>True when the run may proceed to be judged on its results.</returns>
        public bool RecordCallReturned(string nativeDiagnostic)
        {
            NativeDiagnostic = nativeDiagnostic;
            NativeDiagnosticKind = SimulationDiagnostic.Classify(nativeDiagnostic);

            switch (NativeDiagnosticKind)
            {
                case SimulationDiagnosticKind.KnownFailure:
                    CallReturned = false;
                    Refuse(string.Format("TAS reported a failure: \"{0}\".", (nativeDiagnostic ?? string.Empty).Trim()));
                    return false;

                case SimulationDiagnosticKind.KnownSuccess:
                    CallReturned = true;
                    Note(string.Format(
                        "TAS reported success: \"{0}\". Positive evidence, but not the gate - the results "
                        + "reconciliation still decides.",
                        (nativeDiagnostic ?? string.Empty).Trim()));
                    return true;

                case SimulationDiagnosticKind.Unrecognised:
                    CallReturned = true;
                    Note(string.Format(
                        "TAS returned \"{0}\", which is not a diagnostic measured on the licensed machine. It is "
                        + "recorded as evidence and treated as neither success nor failure; the results "
                        + "reconciliation decides.",
                        (nativeDiagnostic ?? string.Empty).Trim()));
                    return true;

                default:
                    CallReturned = true;
                    Note("The TAS call returned without throwing and said nothing.");
                    return true;
            }
        }

        /// <summary>
        /// Stage 2 for <c>TBDDocument.simulate</c>, which answers with an integer: <see cref="NativeReturnCode_Ran"/>
        /// (<c>-1</c>) when the simulation ran and <see cref="NativeReturnCode_Refused"/> (<c>0</c>) when TAS refused it.
        /// <para>
        /// A refusal still writes a stub TSD - measured at 22 bytes, and larger stubs are possible - so the output file
        /// cannot be what decides; this code does. Anything else fails closed: an integer other than <c>-1</c> or
        /// <c>0</c>, <c>null</c>, or a value that is not an integer at all. The raw value is preserved in the refusal.
        /// <c>-1</c> is necessary but not sufficient: <see cref="Conclude"/> still requires a fresh, non-trivial output
        /// and no error log attributable to this run.
        /// </para>
        /// </summary>
        /// <returns>True only for <see cref="NativeReturnCode_Ran"/>.</returns>
        public bool RecordCallReturned(object nativeReturnCode)
        {
            NativeReturnCodeRecorded = true;
            NativeReturnCode = null;

            int code;
            if (nativeReturnCode == null)
            {
                CallReturned = false;
                Refuse(string.Concat("TAS returned no return code (null) from TBDDocument.simulate, so the run fails closed.", NativeContext()));
                return false;
            }

            if (!TryGetInteger(nativeReturnCode, out code))
            {
                CallReturned = false;
                Refuse(
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "TAS returned a value that is not an integer return code from TBDDocument.simulate (\"{0}\", {1}), so the run fails closed.{2}",
                        nativeReturnCode,
                        nativeReturnCode.GetType().FullName,
                        NativeContext()));
                return false;
            }

            NativeReturnCode = code;

            if (code == NativeReturnCode_Ran)
            {
                CallReturned = true;
                Note(string.Format(
                    "TAS returned {0} from TBDDocument.simulate (the simulation ran). Positive evidence, but not the gate - "
                    + "the output and the error log still decide.",
                    code));
                return true;
            }

            CallReturned = false;

            if (code == NativeReturnCode_Refused)
            {
                Refuse(string.Concat("TAS refused the simulation: TBDDocument.simulate returned 0.", NativeContext()));
                return false;
            }

            Refuse(
                string.Format(
                    "TAS returned an unexpected code {0} from TBDDocument.simulate; only -1 (ran) and 0 (refused) are known, "
                    + "so the run fails closed.{1}",
                    code,
                    NativeContext()));
            return false;
        }

        private static bool TryGetInteger(object value, out int code)
        {
            code = 0;
            switch (value)
            {
                case int @int:
                    code = @int;
                    return true;
                case short @short:
                    code = @short;
                    return true;
                case sbyte @sbyte:
                    code = @sbyte;
                    return true;
                case byte @byte:
                    code = @byte;
                    return true;
                case ushort @ushort:
                    code = @ushort;
                    return true;
                case long @long when @long >= int.MinValue && @long <= int.MaxValue:
                    code = (int)@long;
                    return true;
                case uint @uint when @uint <= int.MaxValue:
                    code = (int)@uint;
                    return true;
                case ulong @ulong when @ulong <= int.MaxValue:
                    code = (int)@ulong;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// " TAS return code: …; TAS error log: …" for a refusal of a run whose native return code was recorded, so
        /// whoever reads the refusal can find what TAS said without the rest of the record. Empty otherwise, which keeps
        /// every refusal of a run that records no code exactly as it was.
        /// </summary>
        private string NativeContext()
        {
            if (!NativeReturnCodeRecorded)
            {
                return string.Empty;
            }

            return string.Format(
                " TAS return code: {0}; TAS error log: {1}.",
                NativeReturnCode.HasValue ? NativeReturnCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none",
                string.IsNullOrWhiteSpace(Path_ErrorLog) ? "(no document path)" : Path_ErrorLog);
        }

        /// <summary>Stage 2, failed. The COM call threw, or the document could not be opened.</summary>
        public void RecordCallFailed(string reason)
        {
            CallReturned = false;
            Refuse(string.Concat("The TAS call did not complete: ", reason));
        }

        /// <summary>
        /// Stages 3 and 4. Reads back what the run left: an error log attributable to this run refuses, and
        /// for a separate-output run the output must now exist, post-date <see cref="UtcStarted"/> and be
        /// non-trivial. For an in-place run the document must differ from its pre-call state - which is
        /// recorded as necessary but not sufficient, because <c>Save()</c> writes regardless.
        /// </summary>
        public void Conclude()
        {
            ReadErrorLog();

            string path_Result = Path_Result;

            if (string.IsNullOrWhiteSpace(path_Result))
            {
                return;
            }

            if (File.Exists(path_Result))
            {
                FileInfo fileInfo = new FileInfo(path_Result);
                UtcResultAfter = fileInfo.LastWriteTimeUtc;
                ResultLengthAfter = fileInfo.Length;
            }

            if (OutputShape == SimulationOutputShape.SeparateOutputFile)
            {
                ConcludeSeparateOutputFile(path_Result);
            }
            else if (OutputShape == SimulationOutputShape.InPlaceDocument)
            {
                ConcludeInPlaceDocument(path_Result);
            }
        }

        private void ConcludeSeparateOutputFile(string path_Result)
        {
            if (UtcResultAfter == null)
            {
                Refuse(string.Concat("The run produced no output file: ", path_Result, NativeContext()));
                return;
            }

            // A whole-second filesystem timestamp granularity would make an equal-to comparison unsafe, so
            // the output must not PRE-date the start; equal is accepted.
            if (UtcResultAfter.Value < UtcStarted)
            {
                Refuse(
                    string.Format(
                        "The output file predates this run ({0:o} < {1:o}), so it is a leftover and not this "
                        + "run's result: {2}.{3}",
                        UtcResultAfter,
                        UtcStarted,
                        path_Result,
                        NativeContext()));
                return;
            }

            if (ResultLengthAfter <= TrivialOutputLengthThreshold)
            {
                Refuse(
                    string.Format(
                        "The output file is {0} bytes, at or below the {1}-byte threshold that marks the stub TAS "
                        + "writes when it produced no results: {2}.{3}",
                        ResultLengthAfter,
                        TrivialOutputLengthThreshold,
                        path_Result,
                        NativeContext()));
                return;
            }

            Note(
                string.Format(
                    "Output produced by this run: {0} bytes, written {1:o}.",
                    ResultLengthAfter,
                    UtcResultAfter));
        }

        private void ConcludeInPlaceDocument(string path_Result)
        {
            if (UtcResultAfter == null)
            {
                Refuse(string.Concat("The in-place document no longer exists after the run: ", path_Result));
                return;
            }

            bool changed = UtcResultAfter.Value > UtcResultBefore.GetValueOrDefault()
                || ResultLengthAfter != ResultLengthBefore;

            if (!changed)
            {
                Refuse(
                    string.Format(
                        "The in-place document is unchanged from its pre-call state ({0} bytes, {1:o}), so nothing "
                        + "was written: {2}.",
                        ResultLengthBefore,
                        UtcResultBefore,
                        path_Result));
                return;
            }

            Note(
                "The in-place document was written by this run. NECESSARY BUT NOT SUFFICIENT: Save() writes "
                + "whether or not results were produced, so success still depends on reading the results back "
                + "and reconciling them.");
        }

        /// <summary>
        /// Stage 5, the decisive one for an in-place run: the results were read back out of the document and
        /// reconciled against the expected room set and the requested period.
        /// </summary>
        public void RecordResultsReconciled(int count_Rooms, int startHour, int endHour)
        {
            ResultsReconciled = true;
            Note(
                string.Format(
                    "Results read back and reconciled: {0} rooms, hours {1}-{2} inclusive.",
                    count_Rooms,
                    startHour,
                    endHour));
        }

        /// <summary>Stage 5, failed.</summary>
        public void RecordResultsNotReconciled(string reason)
        {
            ResultsReconciled = false;
            Refuse(string.Concat("The results did not reconcile: ", reason));
        }

        private void ReadErrorLog()
        {
            if (string.IsNullOrWhiteSpace(Path_ErrorLog) || !File.Exists(Path_ErrorLog))
            {
                return;
            }

            DateTime utcWritten;
            try
            {
                utcWritten = new FileInfo(Path_ErrorLog).LastWriteTimeUtc;
            }
            catch (Exception exception)
            {
                Refuse(
                    string.Format(
                        "A TAS error log exists but could not be inspected, so this run cannot be cleared of it: "
                        + "{0} ({1}).",
                        Path_ErrorLog,
                        exception.Message));
                return;
            }

            // Attributable to this run only if it appeared after the start line. A log that could not be
            // deleted and has not been rewritten since is an earlier run's and is reported, not charged here.
            if (utcWritten < UtcStarted)
            {
                Note(
                    string.Format(
                        "A TAS error log is present but predates this run ({0:o} < {1:o}); it is an earlier run's "
                        + "and is not read as this run's failure.",
                        utcWritten,
                        UtcStarted));
                return;
            }

            try
            {
                ErrorLogText = File.ReadAllText(Path_ErrorLog);
            }
            catch (Exception exception)
            {
                Refuse(
                    string.Format(
                        "A TAS error log written by this run could not be read: {0} ({1}).",
                        Path_ErrorLog,
                        exception.Message));
                return;
            }

            Refuse(
                string.Format(
                    "TAS wrote an error log for this run: {0}{1}{2}{3}",
                    Path_ErrorLog,
                    NativeReturnCodeRecorded
                        ? string.Format(" (TAS return code: {0})", NativeReturnCode.HasValue ? NativeReturnCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")
                        : string.Empty,
                    Environment.NewLine,
                    (ErrorLogText ?? string.Empty).Trim()));
        }

        /// <summary>A single-line summary for a progress log or a refusal report.</summary>
        public override string ToString()
        {
            if (Completed)
            {
                return string.Format("Simulation completed ({0}): {1}", OutputShape, Path_Document);
            }

            return string.Format(
                "Simulation NOT evidenced ({0}): {1} - {2}",
                OutputShape,
                Path_Document,
                refusals.Count == 0 ? "no stage recorded" : string.Join("; ", refusals.ToArray()));
        }
    }
}
