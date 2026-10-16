// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using Diagnostic = SAM.Core.Diagnostic;
using DiagnosticDetail = SAM.Core.DiagnosticDetail;
using Diagnostics = SAM.Core.Diagnostics;
using DiagnosticSeverity = SAM.Core.DiagnosticSeverity;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        /// <summary>
        /// The <see cref="Diagnostic"/> form of one <see cref="OptimisationDiagnostic"/>, the finding the optimisation definition checks, the Tas script
        /// check (<see cref="TasScriptDiagnostics"/>) and the Tas model runner raise. Read only.
        /// <para>
        /// <b>Mapping.</b> The producer states its severity, so it is mapped one to one: Info to Information (at <see cref="DiagnosticDetail.Standard"/>; the
        /// producer has no detail dimension), Warning to Warning, Error to Error. A severity value outside the enum is an Error, never Information. The stable
        /// code (<c>OPT101</c> and so on) is kept. <see cref="Diagnostic.Subject"/> is the place in the definition (<see cref="OptimisationDiagnostic.Path"/>, with the line and
        /// column when the finding came from text). <see cref="Diagnostic.Message"/> is <see cref="OptimisationDiagnostic.Message"/> followed by the hint, when there is one;
        /// the legacy text of the finding stays <see cref="OptimisationDiagnostic.ToString"/>.
        /// </para>
        /// </summary>
        /// <param name="optimisationDiagnostic">The finding; null gives null.</param>
        /// <param name="sourceIndex">Position in the list the finding came from; optional.</param>
        public static Diagnostic ToDiagnostic(this OptimisationDiagnostic optimisationDiagnostic, int? sourceIndex = null)
        {
            if (optimisationDiagnostic == null)
            {
                return null;
            }

            DiagnosticSeverity severity;
            DiagnosticDetail detail = DiagnosticDetail.Summary;
            switch (optimisationDiagnostic.Severity)
            {
                case SAM.Core.Optimisation.DiagnosticSeverity.Info:
                    severity = DiagnosticSeverity.Info;
                    detail = DiagnosticDetail.Standard;
                    break;

                case SAM.Core.Optimisation.DiagnosticSeverity.Warning:
                    severity = DiagnosticSeverity.Warning;
                    break;

                default:
                    severity = DiagnosticSeverity.Error;
                    break;
            }

            string subject = optimisationDiagnostic.Path;
            if (optimisationDiagnostic.Line != null)
            {
                subject = string.Format(CultureInfo.InvariantCulture, "{0} (line {1}, column {2})", subject, optimisationDiagnostic.Line, optimisationDiagnostic.Column ?? 1);
            }

            string message = optimisationDiagnostic.Message ?? string.Empty;
            if (!string.IsNullOrEmpty(optimisationDiagnostic.Hint))
            {
                message = message + " " + optimisationDiagnostic.Hint;
            }

            return new Diagnostic(severity, detail, subject, message, optimisationDiagnostic.Code, sourceIndex);
        }

        /// <summary>
        /// The <see cref="Diagnostics"/> form of a list of <see cref="OptimisationDiagnostic"/> (see <see cref="ToDiagnostic(OptimisationDiagnostic, int?)"/> for the mapping).
        /// Order is preserved; <see cref="Diagnostic.SourceIndex"/> is the position in <paramref name="optimisationDiagnostics"/>; a null entry is skipped but still counted.
        /// Warnings and Errors are kept at every <paramref name="diagnosticDetail"/>; only Information is narrowed. Read only.
        /// </summary>
        /// <param name="optimisationDiagnostics">The findings; null gives an empty result.</param>
        /// <param name="diagnosticDetail">The level the result is collected at. The default, <see cref="DiagnosticDetail.Detailed"/>, drops nothing.</param>
        public static Diagnostics ToDiagnostics(this IEnumerable<OptimisationDiagnostic> optimisationDiagnostics, DiagnosticDetail diagnosticDetail = DiagnosticDetail.Detailed)
        {
            Diagnostics result = new Diagnostics(diagnosticDetail);
            if (optimisationDiagnostics == null)
            {
                return result;
            }

            int index = 0;
            foreach (OptimisationDiagnostic optimisationDiagnostic in optimisationDiagnostics)
            {
                result.Add(optimisationDiagnostic.ToDiagnostic(index++));
            }

            return result;
        }
    }
}
