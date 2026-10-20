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
        public QuantityResult(QuantityInput input, EquivalenceOutcome outcome, GateStatus status, string reason, NumericComparison comparison, string proposed, string ownerDecisionNamed, string technicalBlockerNamed, string acceptedBy)
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

        public bool IsPass => Status == GateStatus.Pass;

        public override string ToString()
        {
            return (Definition?.Id ?? "?") + " " + (Input?.ObjectKey ?? string.Empty) + ": " + Outcome + " -> " + Status + (Proposed != null ? " (proposed)" : string.Empty);
        }
    }

    /// <summary>
    /// The gate verdict for one fixture: the highest-ranked trigger decides (S07 section 6), the count per outcome
    /// and every non-pass quantity stay visible so coexisting items are not hidden.
    /// </summary>
    public sealed class GateReport
    {
        public GateReport(string fixtureId, GateStatus status, IReadOnlyList<QuantityResult> results, IReadOnlyList<string> missingRequiredQuantityIds, bool requiredListSupplied, GateRuleSet ruleSet)
        {
            FixtureId = fixtureId;
            Status = status;
            Results = results;
            MissingRequiredQuantityIds = missingRequiredQuantityIds;
            RequiredListSupplied = requiredListSupplied;
            RuleSet = ruleSet;
        }

        public string FixtureId { get; }
        public GateStatus Status { get; }
        public IReadOnlyList<QuantityResult> Results { get; }

        /// <summary>Required quantities with no result. A non-empty list makes the status NOT RUN at best.</summary>
        public IReadOnlyList<string> MissingRequiredQuantityIds { get; }

        /// <summary>False when the caller gave no required list, so a never-evaluated quantity cannot be detected.</summary>
        public bool RequiredListSupplied { get; }

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

        /// <summary>Every result that is not a pass, in input order.</summary>
        public IReadOnlyList<QuantityResult> NonPass => Results.Where(x => !x.IsPass).ToList();

        public IReadOnlyList<string> OwnerDecisionsNamed => Results.Select(x => x.OwnerDecisionNamed).Where(x => x != null).Distinct().ToList();

        public IReadOnlyList<string> TechnicalBlockersNamed => Results.Select(x => x.TechnicalBlockerNamed).Where(x => x != null).Distinct().ToList();

        public override string ToString()
        {
            return FixtureId + ": " + Status + (Provisional ? " (provisional: unapproved OD-G rules applied)" : string.Empty) + ", " + Results.Count + " result(s), " + NonPass.Count + " not pass" +
                   (MissingRequiredQuantityIds.Count > 0 ? ", missing: " + string.Join(",", MissingRequiredQuantityIds) : string.Empty);
        }
    }
}
