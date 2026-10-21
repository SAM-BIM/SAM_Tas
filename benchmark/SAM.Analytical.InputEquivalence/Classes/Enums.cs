// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// The per-quantity outcome categories of S07 section 6. <see cref="UnlistedDefault"/> exists only through OD-G1
    /// (proposed under the pre-decision rule sets, approved under <see cref="GateRuleSet.Approved"/>).
    /// </summary>
    public enum EquivalenceOutcome
    {
        /// <summary>Native equals SAM exactly (d == 0), or both are zero.</summary>
        Match,

        /// <summary>Within the S07 tolerance.</summary>
        WithinTolerance,

        /// <summary>Equal after a contract-listed transformation (T-*). Counts toward PASS only when the transformation is verified.</summary>
        AcceptedTransformation,

        /// <summary>SAM has no value (S-UNSPEC) and the native object is absent: proven absence on both sides.</summary>
        NotApplicable,

        /// <summary>SAM has no value and the native holds the contract-listed default.</summary>
        DocumentedDefault,

        /// <summary>OD-G1: SAM has no value and the native holds 0 although 0 is not contract-listed. Handled as a DOCUMENTED-DEFAULT (unaccepted unless the owner accepts it).</summary>
        UnlistedDefault,

        /// <summary>A contract-listed converter limitation: the native object is missing or differs by design.</summary>
        UnsupportedMapping,

        /// <summary>A known converter behaviour that conflicts with the stated SAM intent.</summary>
        MappingDifference,

        /// <summary>A model interpretation (for example a volume convention) that is required but not recorded.</summary>
        ModelInterpretationRequired,

        /// <summary>The SAM input is out of range or a dependent value is NaN. Not compared.</summary>
        InputInvalid,

        /// <summary>The native field could not be read (licence, executable, COM, field unavailable). Never a NaN.</summary>
        UnverifiedUnreadable,

        /// <summary>The value is readable but a prerequisite is unresolved (identity, hour alignment, an unverified transformation).</summary>
        UnverifiedUnresolved,

        /// <summary>Both values are finite and outside the S07 tolerance without a contract explanation.</summary>
        OutOfTolerance,

        /// <summary>The native value is missing, wrong, NaN or mis-identified without a contract explanation.</summary>
        ExportDefect
    }

    /// <summary>
    /// The roadmap status of the S07 section 6 rank table. The numeric value is the rank: the lowest rank present
    /// decides the status, and only rank 6 is a pass.
    /// </summary>
    public enum GateStatus
    {
        Fail = 1,
        BlockedTechnical = 2,
        BlockedOwnerDecision = 3,
        NotRun = 4,
        NotPassAcceptedLimitation = 5,
        Pass = 6
    }

    /// <summary>
    /// How well the native object is tied to the SAM object it is compared with. It defaults to
    /// <see cref="NotEstablished"/>, so nothing is ever compared on an assumed identity.
    /// </summary>
    public enum IdentityStatus
    {
        /// <summary>No identity evidence was supplied.</summary>
        NotEstablished,

        /// <summary>Only writer-generated names, array order or GUIDs agree. Writer-minted evidence cannot verify what the writer did.</summary>
        WriterOnly,

        /// <summary>Independent evidence (geometry) ties the native object to the SAM object, one to one.</summary>
        Verified,

        /// <summary>The evidence contradicts the pairing, or the match is unmatched or many to one.</summary>
        Mismatch
    }

    /// <summary>Whether the hourly index and day-type alignment has been verified by native evidence (S07 section 7).</summary>
    public enum AlignmentStatus
    {
        /// <summary>The writer convention is assumed, not verified. An EFLH agreement alone never changes this.</summary>
        Unverified,

        Verified
    }

    /// <summary>A limitation listed for the quantity in the S07 contract (section 5 "Limitation" column).</summary>
    public enum ContractListing
    {
        None,
        UnsupportedMapping,
        MappingDifference
    }

    /// <summary>Whether the quantity depends on a model interpretation (S07 D-F9) and whether it is recorded.</summary>
    public enum ModelInterpretation
    {
        NotRequired,
        RequiredNotRecorded,
        Recorded
    }

    /// <summary>
    /// Which gate-completeness rules apply to the three cases S07 section 6.1 left undecided (G1, G1-NaN/G3, G2).
    /// <see cref="Interim"/> and <see cref="ProvisionalProposed"/> are the pre-decision rule sets, kept so earlier evidence
    /// replays unchanged: they can never produce a pass, and every case they touch is at least BLOCKED with the owner
    /// decision named. <see cref="Approved"/> applies OD-G1 to OD-G3 as the owner approved them.
    /// </summary>
    public enum GateRuleSet
    {
        /// <summary>Default. The S07 interim treatment: a G case reports the proposed outcome, labelled proposed, and its status is BLOCKED (owner decision named).</summary>
        Interim,

        /// <summary>
        /// Explicitly provisional. Applies the proposed outcomes: where the proposal is more severe than BLOCKED
        /// (G1-NaN and G3 are EXPORT-DEFECT, rank 1) that rank is used. The floor stays BLOCKED and the result
        /// stays labelled proposed until the owner approves OD-G1 to OD-G3.
        /// </summary>
        ProvisionalProposed,

        /// <summary>
        /// OD-G1, OD-G2 and OD-G3 approved by the owner on 2026-10-21 as drafted in S07 section 6.1 (S08 PR-C): G1 is
        /// DOCUMENTED-DEFAULT or UNLISTED-DEFAULT handled as a DOCUMENTED-DEFAULT (rank 3 until accepted per quantity and
        /// fixture, then rank 5; G2 when not gated); G1-NaN and G3 are EXPORT-DEFECT (rank 1); G2 is rank 3 until accepted.
        /// Nothing is labelled proposed. The one owner-approved refinement of G1 is the verified inactive-profile
        /// transformation (<see cref="QuantityInput.InactiveNativeTransformation"/>); no other zero is read as absence.
        /// </summary>
        Approved
    }

    /// <summary>The limitation an owner may accept per quantity and per fixture (S07 rank 5). UNLISTED-DEFAULT is not acceptable until OD-G1 is approved.</summary>
    public enum AcceptableLimitation
    {
        UnsupportedMapping,
        MappingDifference,
        DocumentedDefault
    }
}
