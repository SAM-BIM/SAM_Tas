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
        public SeriesResult(QuantityResult aggregate, IReadOnlyList<QuantityResult> elements)
        {
            Aggregate = aggregate;
            Elements = elements;
        }

        /// <summary>The decisive element result (the lowest rank; the first on a tie). When every element passes, a within-tolerance one if any.</summary>
        public QuantityResult Aggregate { get; }

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
                Interpretation = prototype.Interpretation
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

                return new SeriesResult(mismatch, new[] { mismatch });
            }

            List<QuantityResult> elements = new List<QuantityResult>(expected.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                elements.Add(Evaluate(With(prototype, expected[i], native[i], prototype.ObjectKey + "[" + i + "]", prototype.Listing), configuration));
            }

            QuantityResult decisive = elements[0];
            foreach (QuantityResult element in elements)
            {
                if ((int)element.Status < (int)decisive.Status)
                {
                    decisive = element;
                }
            }

            if (decisive.IsPass)
            {
                decisive = elements.FirstOrDefault(x => x.Outcome == EquivalenceOutcome.WithinTolerance) ?? decisive;
            }

            return new SeriesResult(decisive, elements);
        }
    }
}
