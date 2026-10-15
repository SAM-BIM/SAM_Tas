// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Globalization;
using Diagnostic = SAM.Core.Diagnostic;
using DiagnosticDetail = SAM.Core.DiagnosticDetail;
using Diagnostics = SAM.Core.Diagnostics;
using DiagnosticSeverity = SAM.Core.DiagnosticSeverity;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The structured form of a <see cref="T3DImportReport"/>. Read only: the report is not changed, and
        /// <see cref="T3DImportReport.ToString"/>, <see cref="T3DImportReport.Skipped"/> and <see cref="T3DImportReport.Notes"/> remain the legacy text.
        /// <para>
        /// <b>Mapping (by the list or flag the producer used, never by message text).</b>
        /// A report whose <see cref="T3DImportReport.Success"/> is false is an Error (<see cref="TasDiagnosticCodes.T3DConversionFailed"/>): the conversion did not
        /// run or TAS refused the model. (<see cref="T3DImportReport.Success"/> is only set by <c>Convert.ToT3D</c> once TAS has built the model, so the report of
        /// a <see cref="T3DImportPlan"/> that has not been converted yet is, correctly, not a success.) Every entry of <see cref="T3DImportReport.Skipped"/> is a Warning (<see cref="TasDiagnosticCodes.T3DSkipped"/>): the report
        /// defines it as something not imported, "a hole in the thermal model", and the workflow already reports each one as an issue. Every entry of
        /// <see cref="T3DImportReport.Notes"/> is Information at <see cref="DiagnosticDetail.Standard"/> (<see cref="TasDiagnosticCodes.T3DNote"/>): the report defines
        /// it as something imported with a caveat that does not lose geometry. The totals line (<see cref="T3DImportReport.ToString"/>) is Information at
        /// <see cref="DiagnosticDetail.Summary"/>, and each zone created for a SAM space (<see cref="T3DImportReport.ZoneNames"/>) is Information at
        /// <see cref="DiagnosticDetail.Detailed"/>, ordered by zone name then space GUID, with the SAM space GUID as its subject.
        /// </para>
        /// <para>
        /// Skipped and Notes entries are free text: the report carries the panel, aperture and space identities inside the sentence only, so
        /// <see cref="Diagnostic.Subject"/> is the conversion ("Direct T3D conversion") and <see cref="Diagnostic.SourceIndex"/> is the position in the list.
        /// Giving each entry a structured identity would need a change in the producers and is not part of this adapter.
        /// </para>
        /// <para>
        /// Order: totals, the failure, <see cref="T3DImportReport.Skipped"/> in order, <see cref="T3DImportReport.Notes"/> in order, then zones.
        /// Warnings and Errors are kept at every <paramref name="diagnosticDetail"/>; only Information is narrowed.
        /// </para>
        /// </summary>
        /// <param name="t3DImportReport">The report; null gives an empty result.</param>
        /// <param name="diagnosticDetail">The level the result is collected at. The default, <see cref="DiagnosticDetail.Detailed"/>, drops nothing.</param>
        public static Diagnostics ToDiagnostics(this T3DImportReport t3DImportReport, DiagnosticDetail diagnosticDetail = DiagnosticDetail.Detailed)
        {
            Diagnostics result = new Diagnostics(diagnosticDetail);
            if (t3DImportReport == null)
            {
                return result;
            }

            const string subject = "Direct T3D conversion";

            result.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Summary, subject, t3DImportReport.ToString(), TasDiagnosticCodes.T3DTotals));

            if (!t3DImportReport.Success)
            {
                result.Add(new Diagnostic(
                    DiagnosticSeverity.Error,
                    DiagnosticDetail.Summary,
                    subject,
                    "The conversion did not complete: it could not run, or TAS refused the model. Nothing it imported is a complete model.",
                    TasDiagnosticCodes.T3DConversionFailed));
            }

            IList<string> skipped = t3DImportReport.Skipped;
            for (int i = 0; i < skipped.Count; i++)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Warning, DiagnosticDetail.Summary, subject, skipped[i], TasDiagnosticCodes.T3DSkipped, i));
            }

            IList<string> notes = t3DImportReport.Notes;
            for (int i = 0; i < notes.Count; i++)
            {
                result.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Standard, subject, notes[i], TasDiagnosticCodes.T3DNote, i));
            }

            List<KeyValuePair<Guid, string>> zones = new List<KeyValuePair<Guid, string>>(t3DImportReport.ZoneNames);
            zones.Sort((x, y) =>
            {
                int compare = string.CompareOrdinal(x.Value, y.Value);
                return compare != 0 ? compare : x.Key.CompareTo(y.Key);
            });

            foreach (KeyValuePair<Guid, string> zone in zones)
            {
                result.Add(new Diagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticDetail.Detailed,
                    string.Format(CultureInfo.InvariantCulture, "Space {0}", zone.Key),
                    string.Format(CultureInfo.InvariantCulture, "TAS zone '{0}' was created for this space.", zone.Value),
                    TasDiagnosticCodes.T3DZoneCreated));
            }

            return result;
        }

        /// <summary>
        /// True when <paramref name="note"/> is a note a workflow step marked as an issue: it starts with <see cref="Modify.NotePrefix_Issue"/>. This is the
        /// producers' own documented marker (a constant they write and the steps themselves test for), not a reading of the text.
        /// </summary>
        public static bool IsIssueNote(string note)
        {
            return note != null && note.StartsWith(Modify.NotePrefix_Issue, StringComparison.Ordinal);
        }

        /// <summary>
        /// The structured form of the notes a workflow step returns: <c>Modify.UpdateBuildingElements</c>, <c>Modify.UpdateApertureDefinitions</c> and
        /// <c>Modify.SetApertureTypes</c> (their <c>out List&lt;string&gt; notes</c>). Read only.
        /// <para>
        /// <b>Mapping, by those producers' documented contract</b> ("problems carry <see cref="Modify.NotePrefix_Issue"/>"): a note that starts with the marker
        /// is an issue, a Warning (<see cref="TasDiagnosticCodes.WorkflowIssue"/>) whose message has the marker removed, because the severity now carries it; every other line
        /// those steps write is a one-line outcome, Information at <see cref="DiagnosticDetail.Summary"/> (<see cref="TasDiagnosticCodes.WorkflowNote"/>). Nothing is classified by what the
        /// text says, and nothing is mapped to Error: these steps report problems as issues and carry on.
        /// </para>
        /// <para>
        /// <b>Do not use this for <see cref="WorkflowCalculator.Notes"/> as a whole.</b> That list also holds the calculator's own refusals, which carry no marker; use
        /// <see cref="WorkflowCalculator.GetDiagnostics"/>, where each of those is an Error chosen by the site that wrote it.
        /// </para>
        /// </summary>
        /// <param name="notes">The notes of one step; null gives an empty result. A null entry is skipped but still counted in <see cref="Diagnostic.SourceIndex"/>.</param>
        /// <param name="subject">The step the notes came from (for example "Updating Building Elements"); optional.</param>
        /// <param name="diagnosticDetail">The level the result is collected at. The default, <see cref="DiagnosticDetail.Detailed"/>, drops nothing.</param>
        public static Diagnostics StepNotesDiagnostics(IEnumerable<string> notes, string subject = null, DiagnosticDetail diagnosticDetail = DiagnosticDetail.Detailed)
        {
            Diagnostics result = new Diagnostics(diagnosticDetail);
            if (notes == null)
            {
                return result;
            }

            int index = 0;
            foreach (string note in notes)
            {
                int sourceIndex = index++;
                if (note == null)
                {
                    continue;
                }

                if (IsIssueNote(note))
                {
                    result.Add(new Diagnostic(DiagnosticSeverity.Warning, DiagnosticDetail.Summary, subject, note.Substring(Modify.NotePrefix_Issue.Length), TasDiagnosticCodes.WorkflowIssue, sourceIndex));
                }
                else
                {
                    result.Add(new Diagnostic(DiagnosticSeverity.Info, DiagnosticDetail.Summary, subject, note, TasDiagnosticCodes.WorkflowNote, sourceIndex));
                }
            }

            return result;
        }
    }
}
