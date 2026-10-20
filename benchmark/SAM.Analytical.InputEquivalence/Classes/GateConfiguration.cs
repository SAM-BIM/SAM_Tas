// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// An owner decision accepting one limitation for one quantity on one fixture (S07 rank 5: "accepted per
    /// quantity and per fixture"). It needs a non-blank reference to the recorded decision. An acceptance never
    /// makes a quantity pass: it moves it from rank 3 to rank 5.
    /// </summary>
    public sealed class OwnerAcceptance
    {
        public OwnerAcceptance(string quantityId, string fixtureId, AcceptableLimitation limitation, string decisionReference)
        {
            if (string.IsNullOrWhiteSpace(quantityId) || string.IsNullOrWhiteSpace(fixtureId))
            {
                throw new ArgumentException("An acceptance is per quantity and per fixture.");
            }

            if (string.IsNullOrWhiteSpace(decisionReference))
            {
                throw new ArgumentException("An acceptance must cite the recorded owner decision.", nameof(decisionReference));
            }

            QuantityId = quantityId;
            FixtureId = fixtureId;
            Limitation = limitation;
            DecisionReference = decisionReference;
        }

        public string QuantityId { get; }
        public string FixtureId { get; }
        public AcceptableLimitation Limitation { get; }
        public string DecisionReference { get; }
    }

    /// <summary>
    /// How a set of quantity results is evaluated and gated. The default is fail-closed: the
    /// <see cref="GateRuleSet.Interim"/> rules, no acceptance and no required quantity list.
    /// </summary>
    public sealed class GateConfiguration
    {
        public GateRuleSet RuleSet { get; init; } = GateRuleSet.Interim;

        /// <summary>Owner acceptances recorded for this run. Empty by default.</summary>
        public IReadOnlyCollection<OwnerAcceptance> Acceptances { get; init; } = Array.Empty<OwnerAcceptance>();

        /// <summary>
        /// The quantity-on-object comparisons the fixture verdict needs ("every applicable quantity must be resolved",
        /// S07 section 6). The caller states them explicitly, for every object (see <see cref="RequiredComparison.Cross"/>);
        /// quantity presence alone is not coverage. Null (not supplied) and empty both leave the coverage unknown, so the
        /// gate cannot reach PASS; a required comparison with no result makes the coverage incomplete, which caps the
        /// status at NOT RUN. Higher-ranked FAIL and BLOCKED outcomes are never hidden by a coverage problem.
        /// </summary>
        public IReadOnlyCollection<RequiredComparison> RequiredComparisons { get; init; }

        internal bool IsAccepted(string quantityId, string fixtureId, AcceptableLimitation limitation, out OwnerAcceptance acceptance)
        {
            acceptance = Acceptances?.FirstOrDefault(x => x != null && x.QuantityId == quantityId && x.FixtureId == fixtureId && x.Limitation == limitation);
            return acceptance != null;
        }
    }
}
