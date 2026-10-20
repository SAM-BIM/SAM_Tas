// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    public static partial class Query
    {
        /// <summary>
        /// Evaluates one quantity on one object with the S07 section 6 state table, then applies the identity,
        /// model-interpretation and hour-alignment prerequisites. The highest-ranked trigger decides; every other
        /// non-pass trigger is appended to the reason so coexisting items stay visible.
        /// <para>
        /// Nothing here relaxes a tolerance or invents a rule. The three cases S07 leaves undecided (section 6.1:
        /// G1, G1-NaN/G3, G2) are handled by <see cref="GateConfiguration.RuleSet"/> and are always labelled
        /// "proposed" with the open owner decision named.
        /// </para>
        /// </summary>
        public static QuantityResult Evaluate(QuantityInput input, GateConfiguration configuration = null)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Definition == null)
            {
                throw new ArgumentException("The quantity input has no definition.", nameof(input));
            }

            if (string.IsNullOrWhiteSpace(input.FixtureId))
            {
                throw new ArgumentException("The quantity input has no fixture; acceptances and the gate are per fixture.", nameof(input));
            }

            configuration = configuration ?? new GateConfiguration();

            List<Trigger> triggers = new List<Trigger> { CoreTrigger(input, configuration) };

            // Prerequisites. They can only make a result less favourable.
            switch (input.Identity)
            {
                case IdentityStatus.Mismatch:
                    triggers.Add(new Trigger(EquivalenceOutcome.ExportDefect, GateStatus.Fail, "Identity mismatch: the native object is unmatched, many to one, or contradicts the SAM object."));
                    break;

                case IdentityStatus.NotEstablished:
                case IdentityStatus.WriterOnly:
                    triggers.Add(new Trigger(EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun, "Identity is not independently verified (" + input.Identity + "); writer-generated names, order and GUIDs are not evidence."));
                    break;
            }

            if (input.Interpretation == ModelInterpretation.RequiredNotRecorded)
            {
                triggers.Add(new Trigger(EquivalenceOutcome.ModelInterpretationRequired, GateStatus.BlockedOwnerDecision, "A model interpretation is required and not recorded.") { OwnerDecision = "record the model interpretation (S07 D-F9)" });
            }

            if (input.Definition.HourlyAligned && input.Alignment != AlignmentStatus.Verified)
            {
                triggers.Add(new Trigger(EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun, "Hour/day-type alignment is unverified (S07 section 7); an EFLH agreement is supporting evidence only."));
            }

            // An informational numeric mismatch (a non-gated quantity) never decides the status while a gating trigger exists.
            List<Trigger> gating = triggers.Where(x => !x.Informational).ToList();
            Trigger decisive = gating.Count > 0 ? gating[0] : triggers[0];
            foreach (Trigger trigger in gating)
            {
                if ((int)trigger.Status < (int)decisive.Status)
                {
                    decisive = trigger;
                }
            }

            string reason = decisive.Reason;
            List<Trigger> others = triggers.Where(x => !ReferenceEquals(x, decisive) && x.Status != GateStatus.Pass).ToList();
            if (others.Count > 0)
            {
                reason += " Also: " + string.Join(" ", others.Select(x => x.Reason));
            }

            return new QuantityResult(input, decisive.Outcome, decisive.Status, reason, decisive.Comparison, decisive.Proposed, decisive.OwnerDecision, decisive.Technical, decisive.AcceptedBy, decisive.Informational);
        }

        private sealed class Trigger
        {
            public Trigger(EquivalenceOutcome outcome, GateStatus status, string reason)
            {
                Outcome = outcome;
                Status = status;
                Reason = reason;
            }

            public EquivalenceOutcome Outcome { get; }
            public GateStatus Status { get; }
            public string Reason { get; }
            public NumericComparison Comparison { get; set; }
            public string Proposed { get; set; }
            public string OwnerDecision { get; set; }
            public string Technical { get; set; }
            public string AcceptedBy { get; set; }

            /// <summary>A numeric mismatch on a quantity that is not gated: reported, never gating.</summary>
            public bool Informational { get; set; }
        }

        private static Trigger NumericMismatch(QuantityInput input, NumericComparison comparison, string reason)
        {
            return new Trigger(EquivalenceOutcome.OutOfTolerance, GateStatus.Fail, reason + (input.Definition.Gated ? string.Empty : " The quantity is not gated (" + input.Definition.NotGatedReason + "), so this is informational and does not decide the fixture status."))
            {
                Comparison = comparison,
                Informational = !input.Definition.Gated
            };
        }

        private static Trigger CoreTrigger(QuantityInput input, GateConfiguration configuration)
        {
            ExpectedValue expected = input.Expected;
            ReaderResult native = input.Native ?? ReaderResult.Unreadable("no reader result was supplied");

            if (expected == null)
            {
                return new Trigger(EquivalenceOutcome.InputInvalid, GateStatus.BlockedTechnical, "No expected value was supplied.") { Technical = "no expected value" };
            }

            // S-INVALID: not compared, whatever the native holds.
            if (expected.State == SamInputState.Invalid)
            {
                return new Trigger(EquivalenceOutcome.InputInvalid, GateStatus.BlockedTechnical, "The SAM input is invalid (S-INVALID): " + expected.Detail + ". It is not compared.") { Technical = "SAM input invalid: " + expected.Detail };
            }

            // An unreadable field is UNVERIFIED, never a NaN or a zero.
            if (native.Kind == ReaderKind.Unreadable)
            {
                return new Trigger(EquivalenceOutcome.UnverifiedUnreadable, GateStatus.BlockedTechnical, "The native field could not be read: " + native.Detail + ".") { Technical = "native field unreadable: " + native.Detail };
            }

            switch (expected.State)
            {
                case SamInputState.Value:
                    return ValueState(input, expected, native, configuration);

                case SamInputState.Zero:
                    return ZeroState(input, expected, native);

                default:
                    return UnspecifiedState(input, native, configuration);
            }
        }

        // S-VALUE
        private static Trigger ValueState(QuantityInput input, ExpectedValue expected, ReaderResult native, GateConfiguration configuration)
        {
            switch (native.Kind)
            {
                case ReaderKind.Absent:
                    return ListedOrDefect(input, configuration, "The native object is absent for an S-VALUE input.");

                case ReaderKind.NaN:
                    // G3 (proposed, OD-G3): NaN is not a comparable value; a listed limitation does not cover it.
                    return GCase(configuration, EquivalenceOutcome.ExportDefect, GateStatus.Fail, "G3 (S-VALUE, native NaN)", "OD-G3", "The native value was read as NaN, which is not comparable; neither abs nor rel can pass it, and a listed limitation does not cover it.");

                case ReaderKind.Zero:
                    // S07 section 6: "abs never turns a missing or zeroed native object into a pass". A non-zero SAM value
                    // against a native object that holds exactly 0 is a zeroed object, however small the SAM value is. It is
                    // judged like a missing one: an export defect unless the contract lists an unsupported mapping or a
                    // mapping difference. The numeric working is kept for the report, but is not used for the verdict.
                    Trigger zeroed = ListedOrDefect(input, configuration, "SAM states a non-zero value (S-VALUE) but the native object holds exactly 0; abs never turns a zeroed native object into a pass.");
                    zeroed.Comparison = Compare(expected.Value, native.Value, input.Definition.Tolerance);
                    return zeroed;

                default:
                    return NumericTrigger(input, expected.Value, native, configuration);
            }
        }

        // S-ZERO
        private static Trigger ZeroState(QuantityInput input, ExpectedValue expected, ReaderResult native)
        {
            switch (native.Kind)
            {
                case ReaderKind.Zero:
                    return new Trigger(EquivalenceOutcome.Match, GateStatus.Pass, "SAM and native are both zero.");

                case ReaderKind.Finite:
                    NumericComparison comparison = Compare(0, native.Value, input.Definition.Tolerance);
                    if (comparison.IsWithin)
                    {
                        return new Trigger(EquivalenceOutcome.WithinTolerance, GateStatus.Pass, "SAM is zero and the native value is within the tolerance of zero.") { Comparison = comparison };
                    }

                    return NumericMismatch(input, comparison, "SAM is zero and the native holds a non-zero value outside the tolerance.");

                case ReaderKind.Absent:
                    if (!string.IsNullOrWhiteSpace(input.TransformationId))
                    {
                        return input.TransformationVerified
                            ? new Trigger(EquivalenceOutcome.AcceptedTransformation, GateStatus.Pass, "SAM is zero and the native object is omitted under " + input.TransformationId + " (verified).")
                            : new Trigger(EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun, "SAM is zero and the native object is omitted under " + input.TransformationId + ", but the transformation is not verified.");
                    }

                    return new Trigger(EquivalenceOutcome.ExportDefect, GateStatus.Fail, "SAM is zero (S-ZERO) and the native object is omitted without a listed transformation.");

                default:
                    // S07: S-ZERO with a native NaN is already EXPORT-DEFECT (approved rule, not provisional).
                    return new Trigger(EquivalenceOutcome.ExportDefect, GateStatus.Fail, "SAM is zero (S-ZERO) and the native value was read as NaN.");
            }
        }

        // S-UNSPEC
        private static Trigger UnspecifiedState(QuantityInput input, ReaderResult native, GateConfiguration configuration)
        {
            switch (native.Kind)
            {
                case ReaderKind.Absent:
                    return new Trigger(EquivalenceOutcome.NotApplicable, GateStatus.Pass, "SAM states no value and the native object is absent.");

                case ReaderKind.Finite:
                    if (input.ListedDefault.HasValue && Compare(input.ListedDefault.Value, native.Value, input.Definition.Tolerance).IsWithin)
                    {
                        return Acceptable(input, configuration, EquivalenceOutcome.DocumentedDefault, AcceptableLimitation.DocumentedDefault, "SAM states no value and the native holds the contract-listed default.");
                    }

                    return new Trigger(EquivalenceOutcome.ExportDefect, GateStatus.Fail, "SAM states no value (S-UNSPEC) and the native holds a non-zero value that is not the contract-listed default.");

                case ReaderKind.Zero:
                    // G1 (proposed, OD-G1): zero is neither absence nor a comparable value.
                    bool listedZero = input.ListedDefault.HasValue && input.ListedDefault.Value == 0;
                    return GCase(configuration, listedZero ? EquivalenceOutcome.DocumentedDefault : EquivalenceOutcome.UnlistedDefault, GateStatus.BlockedOwnerDecision,
                        listedZero ? "G1 (S-UNSPEC, native zero, zero is the listed default)" : "G1 (S-UNSPEC, native zero, UNLISTED-DEFAULT)", "OD-G1",
                        "SAM states no value and the native object is present with the value 0; zero does not mean absence, and it is " + (listedZero ? "" : "not ") + "the contract-listed default.");

                default:
                    // G1-NaN (proposed, OD-G3).
                    return GCase(configuration, EquivalenceOutcome.ExportDefect, GateStatus.Fail, "G1-NaN (S-UNSPEC, native NaN)", "OD-G3", "SAM states no value and the native object is present with the value NaN.");
            }
        }

        private static Trigger NumericTrigger(QuantityInput input, double expectedValue, ReaderResult native, GateConfiguration configuration)
        {
            NumericComparison comparison = Compare(expectedValue, native.Value, input.Definition.Tolerance);

            if (comparison.IsWithin)
            {
                EquivalenceOutcome outcome;
                string reason;
                if (!string.IsNullOrWhiteSpace(input.TransformationId))
                {
                    if (!input.TransformationVerified)
                    {
                        return new Trigger(EquivalenceOutcome.UnverifiedUnresolved, GateStatus.NotRun, "The values agree after " + input.TransformationId + ", but the transformation is not verified.") { Comparison = comparison };
                    }

                    outcome = EquivalenceOutcome.AcceptedTransformation;
                    reason = "Equal within the tolerance after " + input.TransformationId + " (verified).";
                }
                else
                {
                    outcome = comparison.Verdict == NumericVerdict.Identical ? EquivalenceOutcome.Match : EquivalenceOutcome.WithinTolerance;
                    reason = comparison.Verdict == NumericVerdict.Identical ? "Native equals SAM." : "Native is within the tolerance of SAM.";
                }

                return new Trigger(outcome, GateStatus.Pass, reason) { Comparison = comparison };
            }

            if (input.Listing != ContractListing.None)
            {
                Trigger trigger = Acceptable(input, configuration, input.Listing == ContractListing.UnsupportedMapping ? EquivalenceOutcome.UnsupportedMapping : EquivalenceOutcome.MappingDifference,
                    input.Listing == ContractListing.UnsupportedMapping ? AcceptableLimitation.UnsupportedMapping : AcceptableLimitation.MappingDifference,
                    "The native value differs from SAM by a contract-listed " + input.Listing + ".");
                trigger.Comparison = comparison;
                return trigger;
            }

            return NumericMismatch(input, comparison, "The native value is outside the tolerance without a contract explanation.");
        }

        private static Trigger ListedOrDefect(QuantityInput input, GateConfiguration configuration, string detail)
        {
            if (input.Listing == ContractListing.UnsupportedMapping)
            {
                return Acceptable(input, configuration, EquivalenceOutcome.UnsupportedMapping, AcceptableLimitation.UnsupportedMapping, detail + " The contract lists an unsupported mapping.");
            }

            if (input.Listing == ContractListing.MappingDifference)
            {
                return Acceptable(input, configuration, EquivalenceOutcome.MappingDifference, AcceptableLimitation.MappingDifference, detail + " The contract lists a mapping difference.");
            }

            return new Trigger(EquivalenceOutcome.ExportDefect, GateStatus.Fail, detail + " No limitation is listed.");
        }

        /// <summary>
        /// UNSUPPORTED-MAPPING, MAPPING-DIFFERENCE and DOCUMENTED-DEFAULT: rank 3 until the owner accepts them for this
        /// quantity on this fixture, then rank 5. They are never a pass. An unaccepted DOCUMENTED-DEFAULT on a quantity
        /// that is not gated is the proposed G2 case.
        /// </summary>
        private static Trigger Acceptable(QuantityInput input, GateConfiguration configuration, EquivalenceOutcome outcome, AcceptableLimitation limitation, string detail)
        {
            if (configuration.IsAccepted(input.Definition.Id, input.FixtureId, limitation, out OwnerAcceptance acceptance))
            {
                return new Trigger(outcome, GateStatus.NotPassAcceptedLimitation, detail + " Accepted by the owner (" + acceptance.DecisionReference + "); this is not a pass.") { AcceptedBy = acceptance.DecisionReference };
            }

            string decision = "owner acceptance of " + limitation + " for " + input.Definition.Id + " on fixture " + input.FixtureId;
            Trigger trigger = new Trigger(outcome, GateStatus.BlockedOwnerDecision, detail + " Not accepted by the owner.") { OwnerDecision = decision };

            if (outcome == EquivalenceOutcome.DocumentedDefault && !input.Definition.Gated)
            {
                trigger.Proposed = "G2 (unaccepted DOCUMENTED-DEFAULT on a non-gated quantity: " + input.Definition.NotGatedReason + ")";
                trigger.OwnerDecision = decision + "; OD-G2 is open";
            }

            return trigger;
        }

        /// <summary>
        /// The undecided S07 section 6.1 cases. Both rule sets report the proposed outcome, labelled proposed, with
        /// the open decision named, and neither can rise above BLOCKED. <see cref="GateRuleSet.ProvisionalProposed"/>
        /// additionally uses the proposed rank where it is more severe (EXPORT-DEFECT is rank 1). An owner acceptance
        /// does not lift a proposed case.
        /// </summary>
        private static Trigger GCase(GateConfiguration configuration, EquivalenceOutcome proposedOutcome, GateStatus proposedStatus, string label, string decision, string detail)
        {
            GateStatus status = GateStatus.BlockedOwnerDecision;
            if (configuration.RuleSet == GateRuleSet.ProvisionalProposed && (int)proposedStatus < (int)status)
            {
                status = proposedStatus;
            }

            return new Trigger(proposedOutcome, status, detail + " Proposed rule " + label + "; " + decision + " is not approved, so the result is at best BLOCKED.")
            {
                Proposed = label + " -> " + proposedOutcome + " (proposed, " + decision + ")",
                OwnerDecision = decision
            };
        }

        /// <summary>
        /// The S07 section 6 gate for one fixture: the lowest rank present decides, and only rank 6 is a pass.
        /// <para>
        /// A pass needs an explicit, non-empty and complete list of the required quantity-on-object comparisons
        /// (<see cref="GateConfiguration.RequiredComparisons"/>), at least one gating result, and every gating result a
        /// pass. The gate never infers its own coverage from the results it was given: with no list, an empty list or a
        /// required comparison missing the status is at best NOT RUN. A higher-ranked FAIL or BLOCKED outcome is never
        /// hidden by that cap. Informational numeric mismatches (quantities that are not gated) are reported and do not
        /// decide the status; every other outcome of a non-gated quantity still gates.
        /// </para>
        /// </summary>
        public static GateReport Gate(IEnumerable<QuantityResult> results, GateConfiguration configuration = null)
        {
            configuration = configuration ?? new GateConfiguration();
            List<QuantityResult> list = (results ?? Enumerable.Empty<QuantityResult>()).Where(x => x != null).ToList();

            List<string> fixtures = list.Select(x => x.FixtureId).Distinct().ToList();
            if (fixtures.Count > 1)
            {
                throw new ArgumentException("The gate is per fixture; results of " + fixtures.Count + " fixtures were supplied.", nameof(results));
            }

            // Coverage: explicit, non-empty, complete. Key = quantity identifier and object key; quantity presence is not enough.
            CoverageStatus coverage;
            List<RequiredComparison> missing = new List<RequiredComparison>();
            List<QuantityResult> unrequested = new List<QuantityResult>();
            List<RequiredComparison> required = configuration.RequiredComparisons?.Where(x => x != null).ToList();

            if (configuration.RequiredComparisons == null)
            {
                coverage = CoverageStatus.NotSupplied;
            }
            else if (required.Count == 0)
            {
                coverage = CoverageStatus.Empty;
            }
            else
            {
                HashSet<string> present = new HashSet<string>(list.Where(x => !string.IsNullOrWhiteSpace(x.Input?.ObjectKey)).Select(x => CoverageKey(x.Definition?.Id, x.Input.ObjectKey)), StringComparer.Ordinal);
                HashSet<string> asked = new HashSet<string>(required.Select(x => CoverageKey(x.QuantityId, x.ObjectKey)), StringComparer.Ordinal);

                missing = required.Where(x => !present.Contains(CoverageKey(x.QuantityId, x.ObjectKey))).GroupBy(x => CoverageKey(x.QuantityId, x.ObjectKey)).Select(x => x.First()).ToList();
                unrequested = list.Where(x => !asked.Contains(CoverageKey(x.Definition?.Id, x.Input?.ObjectKey))).ToList();
                coverage = missing.Count == 0 ? CoverageStatus.Complete : CoverageStatus.Incomplete;
            }

            List<QuantityResult> gating = list.Where(x => x.GatesFixture).ToList();
            GateStatus status = GateStatus.Pass;
            foreach (QuantityResult result in gating)
            {
                if ((int)result.Status < (int)status)
                {
                    status = result.Status;
                }
            }

            // Unknown, empty or incomplete coverage, or nothing that can gate: not resolved, so at best NOT RUN.
            if ((coverage != CoverageStatus.Complete || gating.Count == 0) && (int)status > (int)GateStatus.NotRun)
            {
                status = GateStatus.NotRun;
            }

            return new GateReport(fixtures.Count == 1 ? fixtures[0] : null, status, list, coverage, missing, unrequested, configuration.RuleSet);
        }

        private static string CoverageKey(string quantityId, string objectKey)
        {
            return quantityId + "\u0001" + objectKey;
        }
    }
}
