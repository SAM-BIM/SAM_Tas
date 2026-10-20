// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>The evaluated outcome of one quantity on one object, with its working.</summary>
    public sealed class QuantityResult
    {
        public QuantityResult(QuantityInput input, EquivalenceOutcome outcome, GateStatus status, string reason, NumericComparison comparison, string proposed, string ownerDecisionNamed, string technicalBlockerNamed, string acceptedBy, bool isInformational = false)
        {
            Input = input;
            Outcome = outcome;
            Status = status;
            Reason = reason;
            Comparison = comparison;
            Proposed = proposed;
            OwnerDecisionNamed = ownerDecisionNamed;
            TechnicalBlockerNamed = technicalBlockerNamed;
            AcceptedBy = acceptedBy;
            IsInformational = isInformational;
        }

        public QuantityInput Input { get; }
        public QuantityDefinition Definition => Input?.Definition;
        public string FixtureId => Input?.FixtureId;
        public EquivalenceOutcome Outcome { get; }

        /// <summary>The rank this quantity contributes to the gate.</summary>
        public GateStatus Status { get; }

        public string Reason { get; }

        /// <summary>The numeric working when a numeric comparison was made, otherwise null.</summary>
        public NumericComparison Comparison { get; }

        /// <summary>Non-null when the outcome rests on an unapproved S07 section 6.1 proposal (OD-G1 to OD-G3). Such a result is labelled "proposed".</summary>
        public string Proposed { get; }

        /// <summary>The owner decision that is needed (an acceptance, or an OD-G case). Null when none.</summary>
        public string OwnerDecisionNamed { get; }

        /// <summary>The technical blocker (unreadable field, invalid input). Null when none.</summary>
        public string TechnicalBlockerNamed { get; }

        /// <summary>The recorded decision that moved this result to rank 5. Null otherwise.</summary>
        public string AcceptedBy { get; }

        /// <summary>
        /// True for a numeric mismatch on a quantity that is not gated (S07: the F-OPQ ground and adiabatic U, the derived g,
        /// the ground temperature). The comparison and its own verdict (<see cref="Outcome"/>, <see cref="Status"/>) stay visible
        /// in the report, but it does not decide the fixture status. Only a numeric mismatch is informational: an absent,
        /// NaN, unreadable or invalid value, a proposed OD-G case, a G2 default and every prerequisite still gate.
        /// </summary>
        public bool IsInformational { get; }

        /// <summary>False for an informational result.</summary>
        public bool GatesFixture => !IsInformational;

        public bool IsPass => Status == GateStatus.Pass;

        public override string ToString()
        {
            return (Definition?.Id ?? "?") + " " + (Input?.ObjectKey ?? string.Empty) + ": " + Outcome + " -> " + Status + (Proposed != null ? " (proposed)" : string.Empty) + (IsInformational ? " (informational, does not gate)" : string.Empty);
        }
    }

    /// <summary>
    /// One comparison the fixture verdict needs: a quantity on a named object (a space, a panel side, an aperture, a
    /// profile). Quantity presence alone is not coverage: "floor area" for one zone says nothing about the other zones.
    /// </summary>
    public sealed class RequiredComparison
    {
        public RequiredComparison(string quantityId, string objectKey)
        {
            if (string.IsNullOrWhiteSpace(quantityId) || string.IsNullOrWhiteSpace(objectKey))
            {
                throw new ArgumentException("A required comparison needs a quantity identifier and an object key.");
            }

            QuantityId = quantityId;
            ObjectKey = objectKey;
        }

        public string QuantityId { get; }
        public string ObjectKey { get; }

        /// <summary>
        /// Every quantity on every object: the all-object coverage for quantities that apply to each listed object. A
        /// quantity that does not apply to an object must be left out by building the list from explicit pairs instead.
        /// </summary>
        public static IReadOnlyCollection<RequiredComparison> Cross(IEnumerable<string> quantityIds, IEnumerable<string> objectKeys)
        {
            List<string> objects = (objectKeys ?? Enumerable.Empty<string>()).ToList();
            List<RequiredComparison> result = new List<RequiredComparison>();
            foreach (string quantityId in quantityIds ?? Enumerable.Empty<string>())
            {
                foreach (string objectKey in objects)
                {
                    result.Add(new RequiredComparison(quantityId, objectKey));
                }
            }

            return result;
        }

        public override string ToString()
        {
            return QuantityId + " @ " + ObjectKey;
        }
    }

    /// <summary>Whether the fixture verdict could rest on an explicit, complete list of the comparisons it needs.</summary>
    public enum CoverageStatus
    {
        /// <summary>The caller gave no list. Coverage is unknown, so the fixture cannot pass.</summary>
        NotSupplied,

        /// <summary>The caller gave an empty list. Rejected: an empty list is not a statement of completeness.</summary>
        Empty,

        /// <summary>A required comparison has no result.</summary>
        Incomplete,

        Complete
    }

    /// <summary>
    /// The gate verdict for one fixture: the highest-ranked trigger decides (S07 section 6), the count per outcome
    /// and every non-pass quantity stay visible so coexisting items are not hidden.
    /// </summary>
    public sealed class GateReport
    {
        public GateReport(string fixtureId, GateStatus status, IReadOnlyList<QuantityResult> results, CoverageStatus coverage, IReadOnlyList<RequiredComparison> missingComparisons, IReadOnlyList<QuantityResult> unrequested, GateRuleSet ruleSet)
        {
            FixtureId = fixtureId;
            Status = status;
            Results = results;
            Coverage = coverage;
            MissingComparisons = missingComparisons;
            Unrequested = unrequested;
            RuleSet = ruleSet;
        }

        public string FixtureId { get; }
        public GateStatus Status { get; }
        public IReadOnlyList<QuantityResult> Results { get; }

        /// <summary>
        /// Whether an explicit, non-empty and complete list of the required comparisons backed the verdict. Anything other
        /// than <see cref="CoverageStatus.Complete"/> caps the status at NOT RUN: the gate never infers its own coverage
        /// from the results it happens to have been given.
        /// </summary>
        public CoverageStatus Coverage { get; }

        /// <summary>Required quantity-on-object comparisons with no result. Non-empty only when <see cref="Coverage"/> is Incomplete.</summary>
        public IReadOnlyList<RequiredComparison> MissingComparisons { get; }

        /// <summary>Results that no required comparison asked for (reported, and still counted in the verdict).</summary>
        public IReadOnlyList<QuantityResult> Unrequested { get; }

        public GateRuleSet RuleSet { get; }

        public bool IsPass => Status == GateStatus.Pass;

        /// <summary>True when any result rests on an unapproved S07 section 6.1 proposal.</summary>
        public bool Provisional => Results.Any(x => x.Proposed != null);

        public IReadOnlyDictionary<EquivalenceOutcome, int> CountByOutcome
        {
            get
            {
                Dictionary<EquivalenceOutcome, int> result = new Dictionary<EquivalenceOutcome, int>();
                foreach (QuantityResult quantityResult in Results)
                {
                    result.TryGetValue(quantityResult.Outcome, out int count);
                    result[quantityResult.Outcome] = count + 1;
                }

                return result;
            }
        }

        /// <summary>Every gating result that is not a pass, in input order. Informational mismatches are listed separately.</summary>
        public IReadOnlyList<QuantityResult> NonPass => Results.Where(x => x.GatesFixture && !x.IsPass).ToList();

        /// <summary>Numeric mismatches on quantities that are not gated: visible, with their own verdict, never deciding the fixture status.</summary>
        public IReadOnlyList<QuantityResult> InformationalMismatches => Results.Where(x => x.IsInformational).ToList();

        public IReadOnlyList<string> OwnerDecisionsNamed => Results.Select(x => x.OwnerDecisionNamed).Where(x => x != null).Distinct().ToList();

        public IReadOnlyList<string> TechnicalBlockersNamed => Results.Select(x => x.TechnicalBlockerNamed).Where(x => x != null).Distinct().ToList();

        public override string ToString()
        {
            return FixtureId + ": " + Status + (Provisional ? " (provisional: unapproved OD-G rules applied)" : string.Empty) + ", " + Results.Count + " result(s), " + NonPass.Count + " not pass, " + InformationalMismatches.Count + " informational, coverage " + Coverage +
                   (MissingComparisons.Count > 0 ? ", missing: " + string.Join(", ", MissingComparisons) : string.Empty);
        }
    }
}
