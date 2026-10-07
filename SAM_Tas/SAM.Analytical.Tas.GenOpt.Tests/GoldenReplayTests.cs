// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.GenOpt.Tests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// The merged SAM GenOpt 3.1.1 golden traces (real Java GenOpt), replayed end to end through the SAM_Tas adapter.
    /// The path is the GenOpt objects, then the Java-text mapping, then the SAM.Math kernel, then
    /// TasGenExecuteObjectiveEvaluator, then the stub TasGenExecute in a real child process. The result must match the
    /// Java trace bit for bit: coordinates, outputs, simulation numbers, main/sub counters, outcome, retries and the
    /// golden-section overview. Cases the SAM_Tas objects cannot express are checked to be refused for the right reason.
    /// </summary>
    [TestFixture]
    public class GoldenReplayTests
    {
        public static IEnumerable<string> Expressible() => GoldenTrace.ExpressibleNames();

        [Test]
        public void GoldenTraces_ArePresent_AndMostAreExpressible()
        {
            Assert.That(GoldenTrace.Names().Count(), Is.EqualTo(31));
            Assert.That(Expressible().Count(), Is.EqualTo(25));
        }

        [TestCaseSource(nameof(Expressible))]
        public void NativeAdapter_ReplaysJavaGenOptTrace_BitForBit(string name)
        {
            GoldenTrace trace = GoldenTrace.Load(name);
            using (TestFolder folder = new TestFolder())
            {
                NativeGenOptRun run = trace.CreateDocument(folder.Folder("ws")).RunNative(folder.Folder("runs"), TestFolder.StubExecutable);

                List<string> differences = trace.Compare(run.Result);
                Assert.That(differences, Is.Empty, name + ":\n" + string.Join("\n", differences));
            }
        }

        /// <summary>
        /// Every case that is not expressible, with the reason. The ones the SAM_Tas objects can still write are refused
        /// by the adapter: GPSCoordinateSearch (D3) and an exponent-notation AbsDiffFunction (D2).
        /// </summary>
        [Test]
        public void NotExpressibleCases_AreTheExpectedOnes()
        {
            Dictionary<string, string> reasons = GoldenTrace.Names().Select(GoldenTrace.Load).Where(t => t.NotExpressible != null).ToDictionary(t => t.Name, t => t.NotExpressible);

            Assert.That(reasons.Keys, Is.EquivalentTo(new[] { "e10-gs-intervalreduction", "e10-gs-no-keyword", "e10-gs-nullspace", "e11-unbounded", "e13-coordinate-search", "ec-gs-collapse" }), string.Join("\n", reasons.Select(r => r.Key + ": " + r.Value)));
            Assert.That(reasons["e13-coordinate-search"], Does.Contain("D3"));
            Assert.That(reasons["ec-gs-collapse"], Does.Contain("D2"));
        }

        [Test]
        public void ExponentAbsDiffFunction_GoldenCase_IsRefusedByTheAdapter()
        {
            Assert.Throws<GenOptCompatibilityException>(() => new GoldenSectionAlgorithm { AbsDiffFunction = 1e-31 }.ToSAM_Optimiser(new OptimizationSettings { MaxIterations = 90 }, 1));
        }

        [Test]
        public void SamGoldenFolder_IsTheSiblingCheckout()
        {
            Assert.That(GoldenTrace.Directory_Golden, Does.EndWith(System.IO.Path.Combine("SAM", "SAM", "SAM.Tests", "Golden", "GenOpt")));
            Assert.That(System.IO.Directory.Exists(GoldenTrace.Directory_Golden), Is.True, "SAM must be checked out next to SAM_Tas (as for the HintPath references)");
            Assert.That(GoldenTrace.Directory_Golden, Does.Not.Contain(AppContext.BaseDirectory));
        }
    }
}
