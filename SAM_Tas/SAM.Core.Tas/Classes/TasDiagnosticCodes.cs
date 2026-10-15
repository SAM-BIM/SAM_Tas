// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Core.Tas
{
    /// <summary>
    /// Stable machine-readable codes carried by the <see cref="Diagnostic"/> records the TAS adapters produce
    /// (<c>Query.ToDiagnostics</c> and <c>WorkflowCalculator.GetDiagnostics</c>). A code names the <b>kind of
    /// producer record</b> it came from, not a parsed cause: no code is ever derived from message text.
    /// <para>
    /// Released values never change. A value is added for a new kind of record; an existing value is never reused
    /// for a different meaning.
    /// </para>
    /// </summary>
    public static class TasDiagnosticCodes
    {
        /// <summary>Error. A refusal recorded by <see cref="SimulationEvidence"/>: the run is not evidence of a successful simulation.</summary>
        public const string SimulationRefusal = "TAS-SIM-001";

        /// <summary>Information. A stage <see cref="SimulationEvidence"/> recorded as passed or observed.</summary>
        public const string SimulationNote = "TAS-SIM-002";

        /// <summary>Warning. TAS returned text that is not in the measured vocabulary (<see cref="SimulationDiagnosticKind.Unrecognised"/>).</summary>
        public const string SimulationUnrecognisedAnswer = "TAS-SIM-003";

        /// <summary>Warning. The run is not (yet) evidenced and no stage has refused it.</summary>
        public const string SimulationNotEvidenced = "TAS-SIM-004";

        /// <summary>Information. The run is evidenced as completed.</summary>
        public const string SimulationCompleted = "TAS-SIM-005";

        /// <summary>Information. A <see cref="Log"/> record of type <see cref="LogRecordType.Message"/>.</summary>
        public const string LogMessage = "TAS-LOG-001";

        /// <summary>Warning. A <see cref="Log"/> record of type <see cref="LogRecordType.Warning"/>.</summary>
        public const string LogWarning = "TAS-LOG-002";

        /// <summary>Error. A <see cref="Log"/> record of type <see cref="LogRecordType.Error"/>.</summary>
        public const string LogError = "TAS-LOG-003";

        /// <summary>Warning. A <see cref="Log"/> record whose type was never stated (<see cref="LogRecordType.Undefined"/>).</summary>
        public const string LogUnclassified = "TAS-LOG-004";

        /// <summary>Error. A direct SAM to T3D conversion that did not complete (<c>T3DImportReport.Success</c> is false).</summary>
        public const string T3DConversionFailed = "TAS-T3D-001";

        /// <summary>Warning. Something the direct SAM to T3D conversion did not import (<c>T3DImportReport.Skipped</c>).</summary>
        public const string T3DSkipped = "TAS-T3D-002";

        /// <summary>Information. Something imported with a caveat (<c>T3DImportReport.Notes</c>).</summary>
        public const string T3DNote = "TAS-T3D-003";

        /// <summary>Information. The one-line totals of a direct SAM to T3D conversion.</summary>
        public const string T3DTotals = "TAS-T3D-004";

        /// <summary>Information. A TAS zone created for a SAM space (<c>T3DImportReport.ZoneNames</c>).</summary>
        public const string T3DZoneCreated = "TAS-T3D-005";

        /// <summary>Error. The workflow refused to run (contradictory instructions, a canonical TBD that cannot be used, a surviving IZAM or a conversion TAS refused); nothing further was run.</summary>
        public const string WorkflowRefused = "TAS-WF-001";

        /// <summary>Information. A workflow note that records a decision, assumption or totals of a step.</summary>
        public const string WorkflowNote = "TAS-WF-002";

        /// <summary>Warning. A workflow note a step marked as an issue (<c>Modify.NotePrefix_Issue</c>).</summary>
        public const string WorkflowIssue = "TAS-WF-003";
    }
}
