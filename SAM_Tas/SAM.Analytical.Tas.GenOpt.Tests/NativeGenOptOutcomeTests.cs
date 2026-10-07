// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Math;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// PR6: the result rules the Grasshopper component (PR4) and SAM_UI (PR5) used to hold each on its own, now in
    /// <see cref="NativeGenOptOutcome"/>. The results are real SAM.Math results from a delegate evaluator; no process.
    /// </summary>
    [TestFixture]
    public class NativeGenOptOutcomeTests
    {
        private static OptimisationResult Run(Optimiser optimiser, Func<double, double> objective, CancellationToken cancellationToken = default(CancellationToken))
        {
            OptimisationProblem problem = new OptimisationProblem(new[] { new OptimisationParameter("x", 10, -5, 35, 2) }, 2);
            return optimiser.Run(problem, new DelegateObjectiveEvaluator((request, token) =>
            {
                double x = request.Coordinates[0];
                double f = objective(x);
                return double.IsInfinity(f) ? ObjectiveEvaluation.Failure("injected failure") : ObjectiveEvaluation.Success(f, 2 * x);
            }), null, cancellationToken);
        }

        private static GoldenSection GoldenSection() => new GoldenSection { StoppingCriterion = GoldenSectionStoppingCriterion.AbsoluteDifference, AbsoluteDifference = 0.1 };

        // ------------------------------------------------------------------ normal ends

        [Test]
        public void Success_IsSuccessfulWithTheKernelMinimum()
        {
            OptimisationResult result = Run(new HookeJeeves(), x => (x - 5) * (x - 5));
            Assert.That(result.Outcome, Is.EqualTo(OptimisationOutcome.Success));

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, false);

            Assert.That(outcome.Completed, Is.True);
            Assert.That(outcome.Withheld, Is.False);
            Assert.That(outcome.Successful, Is.True);
            Assert.That(outcome.BestEntry, Is.SameAs(result.Minimum));
            Assert.That(outcome.Interval, Is.Null, "Pattern search has no interval.");
        }

        [Test]
        public void MaximumSimulationsReached_IsSuccessfulWithTheCurrentLowestPoint()
        {
            OptimisationResult result = Run(new HookeJeeves { MaximumSimulations = 3 }, x => (x - 5) * (x - 5));
            Assert.That(result.Outcome, Is.EqualTo(OptimisationOutcome.MaximumSimulationsReached));

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, false);

            Assert.That(outcome.Successful, Is.True);
            Assert.That(outcome.BestEntry, Is.SameAs(result.Minimum));
        }

        [Test]
        public void GoldenSection_BestPointIsTheFirstLowestEntry_AndTheIntervalIsReported()
        {
            // A flat objective: every entry ties, so the first one is reported (the PR3 acceptance definition).
            OptimisationResult result = Run(GoldenSection(), x => 7.0);
            Assert.That(result.Minimum, Is.Null, "Golden section reports no minimum entry.");
            Assert.That(result.Entries.Count, Is.GreaterThan(1));

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, false);

            Assert.That(outcome.Successful, Is.True);
            Assert.That(outcome.BestEntry, Is.SameAs(result.Entries[0]));
            Assert.That(outcome.Interval, Is.SameAs(result.Interval));
            Assert.That(outcome.Interval, Is.Not.Null);
        }

        [Test]
        public void GoldenSection_BestPointIsTheLowestObjective()
        {
            OptimisationResult result = Run(GoldenSection(), x => (x - 4.9) * (x - 4.9));

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, false);

            double lowest = result.Entries.Min(x => x.Objective);
            Assert.That(outcome.BestEntry.Objective, Is.EqualTo(lowest));
            Assert.That(outcome.BestEntry, Is.SameAs(result.Entries.First(x => x.Objective == lowest)));
        }

        [Test]
        public void NotANumberObjectives_AreNeverTheBestPoint()
        {
            // The first golden-section points are about 10.3 and 19.7 on [-5, 35].
            OptimisationResult result = Run(GoldenSection(), x => x > 15 ? double.NaN : x);
            Assert.That(result.Entries.Any(x => double.IsNaN(x.Objective)), Is.True);

            OptimisationTraceEntry best = NativeGenOptOutcome.Best(result);

            Assert.That(best, Is.Not.Null);
            Assert.That(double.IsNaN(best.Objective), Is.False);
        }

        // ------------------------------------------------------------------ withheld and failed

        [Test]
        public void CancelObservedAfterTheRun_WithholdsTheResult()
        {
            OptimisationResult result = Run(GoldenSection(), x => (x - 5) * (x - 5));
            Assert.That(NativeGenOptOutcome.IsCompleted(result.Outcome), Is.True);

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, true);

            Assert.That(outcome.Completed, Is.True);
            Assert.That(outcome.Withheld, Is.True);
            Assert.That(outcome.Successful, Is.False);
            Assert.That(outcome.Outcome, Is.EqualTo(result.Outcome), "The kernel's own outcome is still reported.");
            Assert.That(outcome.BestEntry, Is.Null);
            Assert.That(outcome.Interval, Is.Null);
        }

        [Test]
        public void Cancelled_IsNotACompletedRunAndNothingIsWithheld()
        {
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();
                OptimisationResult result = Run(new HookeJeeves(), x => x, cancellationTokenSource.Token);
                Assert.That(result.Outcome, Is.EqualTo(OptimisationOutcome.Cancelled));

                NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, true);

                Assert.That(outcome.Completed, Is.False);
                Assert.That(outcome.Withheld, Is.False);
                Assert.That(outcome.Successful, Is.False);
                Assert.That(outcome.BestEntry, Is.Null);
            }
        }

        [Test]
        public void EvaluationFailed_HasNoBestPoint()
        {
            OptimisationResult result = Run(new HookeJeeves(), x => double.PositiveInfinity);
            Assert.That(result.Outcome, Is.EqualTo(OptimisationOutcome.EvaluationFailed));

            NativeGenOptOutcome outcome = new NativeGenOptOutcome(result, false);

            Assert.That(outcome.Successful, Is.False);
            Assert.That(outcome.BestEntry, Is.Null);
        }

        [Test]
        public void InitialPointInfeasible_HasNoBestPoint()
        {
            OptimisationProblem problem = new OptimisationProblem(new[] { new OptimisationParameter("x", 50, -5, 35, 2) }, 1);
            OptimisationResult result = new HookeJeeves().Run(problem, new DelegateObjectiveEvaluator((request, token) => ObjectiveEvaluation.Success(request.Coordinates[0])));
            Assume.That(result.Outcome, Is.EqualTo(OptimisationOutcome.InitialPointInfeasible));

            Assert.That(new NativeGenOptOutcome(result, false).Successful, Is.False);
        }

        [Test]
        public void OnlySuccessTheLimitAndNullspaceAreNormalEnds()
        {
            OptimisationOutcome[] completed = Enum.GetValues(typeof(OptimisationOutcome)).Cast<OptimisationOutcome>().Where(NativeGenOptOutcome.IsCompleted).ToArray();

            Assert.That(completed, Is.EquivalentTo(new[] { OptimisationOutcome.Success, OptimisationOutcome.MaximumSimulationsReached, OptimisationOutcome.Nullspace }));
        }

        [Test]
        public void ARunIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new NativeGenOptOutcome((OptimisationResult)null, false));
            Assert.Throws<ArgumentNullException>(() => new NativeGenOptOutcome((NativeGenOptRun)null, false));
            Assert.That(NativeGenOptOutcome.Best(null), Is.Null);
        }

        // ------------------------------------------------------------------ the running lowest

        [Test]
        public void IsLower_FirstWinsATie_AndNaNNeverWins()
        {
            OptimisationResult result = Run(GoldenSection(), x => x > 15 ? double.NaN : 7.0);
            OptimisationTraceEntry first = result.Entries.First(x => !double.IsNaN(x.Objective));
            OptimisationTraceEntry tie = result.Entries.Skip(result.Entries.ToList().IndexOf(first) + 1).First(x => !double.IsNaN(x.Objective));
            OptimisationTraceEntry nan = result.Entries.First(x => double.IsNaN(x.Objective));

            Assert.That(NativeGenOptOutcome.IsLower(first, null), Is.True);
            Assert.That(NativeGenOptOutcome.IsLower(tie, first), Is.False);
            Assert.That(NativeGenOptOutcome.IsLower(nan, null), Is.False);
            Assert.That(NativeGenOptOutcome.IsLower(nan, first), Is.False);
            Assert.That(NativeGenOptOutcome.IsLower(null, first), Is.False);
        }

        [Test]
        public void IsLower_FoldedOverTheTrace_GivesTheBestPointOfAGoldenSectionRun()
        {
            OptimisationResult result = Run(GoldenSection(), x => (x - 4.9) * (x - 4.9));
            OptimisationTraceEntry lowest = null;
            foreach (OptimisationTraceEntry entry in result.Entries)
            {
                if (NativeGenOptOutcome.IsLower(entry, lowest))
                {
                    lowest = entry;
                }
            }

            Assert.That(lowest, Is.SameAs(NativeGenOptOutcome.Best(result)));
        }

        // ------------------------------------------------------------------ refusals

        [Test]
        public void RefusalMessage_NamesTheCause()
        {
            Assert.That(NativeGenOptOutcome.RefusalMessage(new GenOptCompatibilityException("AbsDiffFunction ...")), Is.EqualTo("Invalid GenOpt settings for the native route: AbsDiffFunction ..."));
            Assert.That(NativeGenOptOutcome.RefusalMessage(new NotSupportedException("Parametric ...")), Is.EqualTo("Not supported by the native route: Parametric ..."));
            Assert.That(NativeGenOptOutcome.RefusalMessage(new FileNotFoundException("TasGenExecute was not found.", @"C:\x\TasGenExecute.exe")), Is.EqualTo(@"TasGenExecute was not found. Path: 'C:\x\TasGenExecute.exe'."));
            Assert.That(NativeGenOptOutcome.RefusalMessage(new DirectoryNotFoundException("The optimisation workspace does not exist: 'C:\\w'.")), Is.EqualTo("The optimisation workspace does not exist: 'C:\\w'."));
            Assert.That(NativeGenOptOutcome.RefusalMessage(new IOException("disk full")), Is.EqualTo("Native optimisation failed (IOException): disk full"));
            Assert.That(NativeGenOptOutcome.RefusalMessage(null), Is.EqualTo("Native optimisation failed (): "));
        }

        [Test]
        public void RefusalMessage_ForAnUnsupportedCoordinateSearch_SaysWhy()
        {
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => new GPSCoordinateSearchAlgorithm { MeshSizeDivider = 2, MeshSizeExponentIncrement = 1, NumberOfStepReduction = 4 }.ToSAM_Optimiser(new OptimizationSettings(), 1));

            Assert.That(NativeGenOptOutcome.RefusalMessage(exception), Does.StartWith("Not supported by the native route: GenOpt algorithm 'GPSCoordinateSearch'"));
        }
    }
}
