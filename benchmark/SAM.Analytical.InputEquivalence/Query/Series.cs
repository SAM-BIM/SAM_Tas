// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>The evaluation of an hourly (or any ordered) series: one result per element and the aggregate.</summary>
    public sealed class SeriesResult
    {
        public SeriesResult(QuantityResult aggregate, IReadOnlyList<QuantityResult> elements, string seriesObjectKey)
        {
            Aggregate = aggregate;
            Elements = elements;

            // One result for the whole series, keyed by the series object (not by the decisive hour), so it can satisfy a
            // required comparison. It carries the decisive element's verdict and names the element in the reason.
            InformationalElements = elements.Where(x => x.IsInformational).ToList();

            QuantityInput input = Query.With(aggregate.Input, aggregate.Input.Expected, aggregate.Input.Native, seriesObjectKey, aggregate.Input.Listing);
            string element = aggregate.Input.ObjectKey != seriesObjectKey ? " [decisive element " + aggregate.Input.ObjectKey + " of " + elements.Count + "]" : string.Empty;
            string informational = InformationalElements.Count > 0 && !aggregate.IsInformational
                ? " [" + InformationalElements.Count + " informational mismatch element(s) do not gate, first " + InformationalElements[0].Input.ObjectKey + "]"
                : string.Empty;

            GateResult = new QuantityResult(input, aggregate.Outcome, aggregate.Status, aggregate.Reason + element + informational, aggregate.Comparison, aggregate.Proposed, aggregate.OwnerDecisionNamed, aggregate.TechnicalBlockerNamed, aggregate.AcceptedBy, aggregate.IsInformational, InformationalElements);
        }

        /// <summary>
        /// The elements that are informational numeric mismatches (non-gated quantity). They never decide the series result
        /// while a gating element exists, but they are never dropped: they are listed here and carried on <see cref="GateResult"/>.
        /// </summary>
        public IReadOnlyList<QuantityResult> InformationalElements { get; }

        /// <summary>The decisive element result (the lowest rank; the first on a tie). When every element passes, a within-tolerance one if any.</summary>
        public QuantityResult Aggregate { get; }

        /// <summary>The series verdict as one result keyed by the series object: pass this to <see cref="Query.Gate"/>, not an element.</summary>
        public QuantityResult GateResult { get; }

        public IReadOnlyList<QuantityResult> Elements { get; }

        public int NotPassCount => Elements.Count(x => !x.IsPass);
    }

    public static partial class Query
    {
        /// <summary>Copies a quantity input with new values. Everything else, in particular identity and alignment, is kept.</summary>
        internal static QuantityInput With(QuantityInput prototype, ExpectedValue expected, ReaderResult native, string objectKey, ContractListing listing)
        {
            return new QuantityInput
            {
                Definition = prototype.Definition,
                FixtureId = prototype.FixtureId,
                ObjectKey = objectKey,
                Expected = expected,
                Native = native,
                Identity = prototype.Identity,
                Alignment = prototype.Alignment,
                Listing = listing,
                ListedDefault = prototype.ListedDefault,
                TransformationId = prototype.TransformationId,
                TransformationVerified = prototype.TransformationVerified,
                Interpretation = prototype.Interpretation,
                InactiveNativeTransformation = prototype.InactiveNativeTransformation
            };
        }

        /// <summary>
        /// Evaluates every element of a series with <see cref="Evaluate"/> and aggregates by rank. A length difference
        /// between the expected and the native series is an EXPORT-DEFECT: a series is never silently truncated or padded.
        /// The prototype's identity and alignment apply to every element.
        /// </summary>
        public static SeriesResult EvaluateSeries(QuantityInput prototype, IReadOnlyList<ExpectedValue> expected, IReadOnlyList<ReaderResult> native, GateConfiguration configuration = null)
        {
            if (prototype == null)
            {
                throw new ArgumentNullException(nameof(prototype));
            }

            if (expected == null || native == null || expected.Count != native.Count || expected.Count == 0)
            {
                QuantityResult mismatch = new QuantityResult(prototype, EquivalenceOutcome.ExportDefect, GateStatus.Fail,
                    "The expected and native series are empty or differ in length (" + (expected?.Count ?? 0) + " vs " + (native?.Count ?? 0) + "); a series is never truncated or padded.", null, null, null, null, null);

                return new SeriesResult(mismatch, new[] { mismatch }, prototype.ObjectKey);
            }

            List<QuantityResult> elements = new List<QuantityResult>(expected.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                elements.Add(Evaluate(With(prototype, expected[i], native[i], prototype.ObjectKey + "[" + i + "]", prototype.Listing), configuration));
            }

            // The decisive element is chosen among the elements that gate the fixture, by the existing rank precedence (lowest
            // rank; the first on a tie). An informational mismatch (a non-gated quantity) can therefore never outrank, tie with
            // or hide a gating FAIL, BLOCKED, NOT RUN or accepted limitation. Only when no element gates is an informational
            // element the series result; every informational element stays in the evidence either way.
            List<QuantityResult> gating = elements.Where(x => x.GatesFixture).ToList();
            List<QuantityResult> candidates = gating.Count > 0 ? gating : elements;

            QuantityResult decisive = candidates[0];
            foreach (QuantityResult element in candidates)
            {
                if ((int)element.Status < (int)decisive.Status)
                {
                    decisive = element;
                }
            }

            if (decisive.IsPass)
            {
                decisive = candidates.FirstOrDefault(x => x.Outcome == EquivalenceOutcome.WithinTolerance) ?? decisive;
            }

            return new SeriesResult(decisive, elements, prototype.ObjectKey);
        }
    }
}
