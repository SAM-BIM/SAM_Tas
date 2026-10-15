// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Core.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The structured form of what a <see cref="SimulationEvidence"/> recorded. Read only: the evidence is not changed, and
        /// <see cref="SimulationEvidence.Notes"/>, <see cref="SimulationEvidence.Refusals"/> and <see cref="SimulationEvidence.ToString"/>
        /// remain the legacy text.
        /// <para>
        /// <b>Mapping (by what the producer recorded, never by message text).</b>
        /// Every refusal is an Error (<see cref="TasDiagnosticCodes.SimulationRefusal"/>): a refusal is by definition a reason the run is not evidence of
        /// a successful simulation. Every note is Information at <see cref="DiagnosticDetail.Standard"/> (<see cref="TasDiagnosticCodes.SimulationNote"/>): the notes are
        /// per-stage outcomes and carry no further structure. An answer TAS returned that is not in the measured vocabulary
        /// (<see cref="SimulationDiagnosticKind.Unrecognised"/>) is a Warning, so it is kept at every detail level and is never reported as Information.
        /// A completed run adds one Information record at <see cref="DiagnosticDetail.Summary"/>; a run that is not completed and has no refusal
        /// adds a Warning (for example an in-place run whose results have not yet been reconciled).
        /// </para>
        /// <para>
        /// Order: notes in the order recorded, then the unrecognised-answer warning, then refusals in the order recorded, then the outcome.
        /// <see cref="Diagnostic.SourceIndex"/> is the position in <see cref="SimulationEvidence.Notes"/> or <see cref="SimulationEvidence.Refusals"/>
        /// (null for the outcome and the unrecognised-answer record). <see cref="Diagnostic.Subject"/> is <see cref="SimulationEvidence.Path_Document"/>,
        /// the TBD or TPD that was simulated.
        /// </para>
        /// <para>
        /// This is <b>not</b> the legacy format: <c>Diagnostics.Format()</c> writes <c>Severity: Subject: Message</c>, whereas the legacy text is the
        /// bare notes and refusals above. Warnings and Errors are kept at every <paramref name="diagnosticDetail"/>; only Information is narrowed.
        /// </para>
        /// </summary>
        /// <param name="simulationEvidence">The evidence; null gives an empty result.</param>
        /// <param name="diagnosticDetail">The level the result is collected at. The default, <see cref="DiagnosticDetail.Detailed"/>, drops nothing.</param>
        public static Diagnostics ToDiagnostics(this SimulationEvidence simulationEvidence, DiagnosticDetail diagnosticDetail = DiagnosticDetail.Detailed)
        {
            Diagnostics result = new Diagnostics(diagnosticDetail);
            if (simulationEvidence == null)
            {
                return result;
            }

            string subject = string.IsNullOrWhiteSpace(simulationEvidence.Path_Document) ? "TAS simulation" : simulationEvidence.Path_Document;

            List<string> notes = simulationEvidence.Notes;
            for (int i = 0; i < notes.Count; i++)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Standard, subject, notes[i], TasDiagnosticCodes.SimulationNote, i));
            }

            if (simulationEvidence.NativeDiagnosticKind == SimulationDiagnosticKind.Unrecognised)
            {
                result.Add(new Diagnostic(
                    DiagnosticSeverity.Warning,
                    DiagnosticDetail.Summary,
                    subject,
                    string.Format("TAS returned \"{0}\", which is not a diagnostic measured on the licensed machine; it is classified neither as success nor as failure.", (simulationEvidence.NativeDiagnostic ?? string.Empty).Trim()),
                    TasDiagnosticCodes.SimulationUnrecognisedAnswer));
            }

            List<string> refusals = simulationEvidence.Refusals;
            for (int i = 0; i < refusals.Count; i++)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Error, DiagnosticDetail.Summary, subject, refusals[i], TasDiagnosticCodes.SimulationRefusal, i));
            }

            if (simulationEvidence.Completed)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Summary, subject, simulationEvidence.ToString(), TasDiagnosticCodes.SimulationCompleted));
            }
            else if (refusals.Count == 0)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Warning, DiagnosticDetail.Summary, subject, simulationEvidence.ToString(), TasDiagnosticCodes.SimulationNotEvidenced));
            }

            return result;
        }

        /// <summary>
        /// The structured form of a <see cref="Log"/>. Read only: the log is not changed.
        /// <para>
        /// <b>Mapping (one to one on <see cref="LogRecord.LogRecordType"/>).</b>
        /// <see cref="LogRecordType.Error"/> is an Error, <see cref="LogRecordType.Warning"/> a Warning and <see cref="LogRecordType.Message"/> Information at
        /// <see cref="DiagnosticDetail.Standard"/>. A record of <see cref="LogRecordType.Undefined"/> (what a <c>LogRecord</c> built from text alone is) has no
        /// stated severity: it is mapped to a Warning with <see cref="TasDiagnosticCodes.LogUnclassified"/>, so it is neither dropped at any detail level
        /// nor reported as Information, and it does not claim a failure the producer never stated. The legacy text of a record is
        /// <see cref="LogRecord.Text"/>; it is used unchanged as the message. A null record is skipped but still counted in <see cref="Diagnostic.SourceIndex"/>.
        /// </para>
        /// </summary>
        /// <param name="log">The log; null gives an empty result.</param>
        /// <param name="subject">What the log is about (for example the construction a thermal transmittance calculation was for); optional.</param>
        /// <param name="diagnosticDetail">The level the result is collected at. The default, <see cref="DiagnosticDetail.Detailed"/>, drops nothing.</param>
        public static Diagnostics ToDiagnostics(this Log log, string subject = null, DiagnosticDetail diagnosticDetail = DiagnosticDetail.Detailed)
        {
            Diagnostics result = new Diagnostics(diagnosticDetail);
            if (log == null)
            {
                return result;
            }

            int index = 0;
            foreach (LogRecord logRecord in log)
            {
                int sourceIndex = index++;
                if (logRecord == null)
                {
                    continue;
                }

                DiagnosticSeverity severity;
                DiagnosticDetail detail;
                string code;
                switch (logRecord.LogRecordType)
                {
                    case LogRecordType.Error:
                        severity = DiagnosticSeverity.Error;
                        detail = DiagnosticDetail.Summary;
                        code = TasDiagnosticCodes.LogError;
                        break;

                    case LogRecordType.Warning:
                        severity = DiagnosticSeverity.Warning;
                        detail = DiagnosticDetail.Summary;
                        code = TasDiagnosticCodes.LogWarning;
                        break;

                    case LogRecordType.Message:
                        severity = DiagnosticSeverity.Info;
                        detail = DiagnosticDetail.Standard;
                        code = TasDiagnosticCodes.LogMessage;
                        break;

                    default:
                        severity = DiagnosticSeverity.Warning;
                        detail = DiagnosticDetail.Summary;
                        code = TasDiagnosticCodes.LogUnclassified;
                        break;
                }

                result.Add(new Diagnostic(severity, detail, subject, logRecord.Text, code, sourceIndex));
            }

            return result;
        }
    }
}
