// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Core.Tas
{
    /// <summary>What a TAS <c>Simulate</c> return string is known to mean.</summary>
    public enum SimulationDiagnosticKind
    {
        /// <summary>TAS returned nothing. Not proof of success on its own - see <see cref="SimulationDiagnostic"/>.</summary>
        Silent,

        /// <summary>TAS returned text matching a diagnostic measured to accompany a failed run.</summary>
        KnownFailure,

        /// <summary>
        /// TAS returned text that has not been observed on the licensed machine. It is preserved as
        /// evidence and is deliberately <b>not</b> read as either success or failure.
        /// </summary>
        Unrecognised,
    }

    /// <summary>
    /// Classifies the string <c>ITPD.Simulate</c> returns.
    /// <para>
    /// <b>Why this is a vocabulary and not <c>!string.IsNullOrEmpty(x)</c>.</b> <c>ITPD.Simulate</c> is
    /// declared as returning a <c>String</c>, and production discarded it. Four distinct returns have been
    /// measured on the licensed machine, and every one of them accompanied a run that produced nothing:
    /// </para>
    /// <code>
    /// "Plant room has no components"
    /// "Plant Room Has Errors"
    /// "Failed to open the TSD file"
    /// "Sizing Flow Failed"
    /// </code>
    /// <para>
    /// But <b>no successful run has yet been observed through this route</b>, so what TAS returns on
    /// success - null, empty, or some status text - is <b>not established</b>. Treating every non-empty
    /// answer as an error would therefore be an assumption, and a status string on success would turn
    /// every good run into a refusal.
    /// </para>
    /// <para>
    /// So: a measured failure refuses; anything else is preserved verbatim as evidence and decides
    /// nothing. That is safe because it is <b>not</b> the success gate - the decisive gate is the
    /// complete <c>ZoneTemperature</c> reconciliation, which refuses a run that produced no results
    /// whatever TAS said about it.
    /// </para>
    /// <para>
    /// When a successful run is finally observed, add its return here: if success is silent, nothing
    /// changes; if success carries text, this is the one place that needs to learn it.
    /// </para>
    /// </summary>
    public static class SimulationDiagnostic
    {
        /// <summary>
        /// Fragments measured on licensed TAS, each seen accompanying a run that produced no results.
        /// Matched case-insensitively as substrings, because TAS's casing varies between them
        /// ("Plant room has no components" against "Plant Room Has Errors").
        /// </summary>
        private static readonly string[] knownFailureFragments = new string[]
        {
            "has no components",
            "has errors",
            "failed to open",
            "failed",
            "error",
            "cannot",
            "unable",
            "invalid",
            "missing",
        };

        /// <summary>The fragments this classifier recognises, for reporting and for tests.</summary>
        public static IEnumerable<string> KnownFailureFragments
        {
            get { return (string[])knownFailureFragments.Clone(); }
        }

        /// <summary>Classifies a <c>Simulate</c> return.</summary>
        public static SimulationDiagnosticKind Classify(string returned)
        {
            if (string.IsNullOrWhiteSpace(returned))
            {
                return SimulationDiagnosticKind.Silent;
            }

            string trimmed = returned.Trim();

            foreach (string fragment in knownFailureFragments)
            {
                if (trimmed.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return SimulationDiagnosticKind.KnownFailure;
                }
            }

            return SimulationDiagnosticKind.Unrecognised;
        }

        /// <summary>
        /// Whether this return, on its own, is grounds to refuse the run. Only a measured failure is.
        /// </summary>
        public static bool IsFailure(string returned)
        {
            return Classify(returned) == SimulationDiagnosticKind.KnownFailure;
        }
    }
}
