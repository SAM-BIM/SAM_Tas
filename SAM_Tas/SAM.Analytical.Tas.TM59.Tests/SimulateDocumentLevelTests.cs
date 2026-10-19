// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core.Tas;
using System;
using System.IO;
using System.Reflection;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// S01a. <c>Modify.Simulate(TBDDocument, path_TSD, ...)</c> (and the <c>SAMTBDDocument</c> overload that delegates to
    /// it) previously answered <c>code == -1 &amp;&amp; WaitToUnlock(path_TSD)</c>, so a leftover, empty or stub TSD, or a run
    /// TAS logged as failed, could still report success. It now decides through <see cref="SimulationEvidence"/>.
    /// <para>
    /// <b>Simulated evidence, not licensed.</b> A TAS COM response cannot be produced safely on demand, so these tests
    /// drive the private decision <c>Modify.SimulateToTSD(Func&lt;int&gt;, path_TSD, out evidence)</c> with a delegate that
    /// stands in for <c>TBDDocument.simulate</c>: it writes the files TAS would write and returns the code TAS would
    /// return. The real COM call is the one-line lambda in the public overload.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SimulateDocumentLevelTests
    {
        private string directory;
        private string path_TSD;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_S01a_DocumentLevel", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path_TSD = Path.Combine(directory, "beta.tsd");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // A leftover temp directory must never fail a test.
            }
        }

        /// <summary>Where TAS writes the error log for a TSD: beside it, with the TSD's basename (measured).</summary>
        private string Path_ErrorLog => Path.Combine(directory, "beta_error_log.txt");

        private bool Simulate(Func<int> simulate, out SimulationEvidence simulationEvidence)
        {
            MethodInfo methodInfo = typeof(SAM.Analytical.Tas.Modify).GetMethod("SimulateToTSD", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(methodInfo, Is.Not.Null, "the document-level decision the public TBDDocument overload calls");

            object[] arguments = new object[] { simulate, path_TSD, null };
            bool result = (bool)methodInfo.Invoke(null, arguments);
            simulationEvidence = (SimulationEvidence)arguments[2];
            return result;
        }

        /// <summary>A stand-in for TAS: optionally writes a TSD of <paramref name="length"/> bytes and an error log, then returns <paramref name="code"/>.</summary>
        private Func<int> Tas(int code, long length, string errorLog = null)
        {
            return () =>
            {
                if (length >= 0)
                {
                    File.WriteAllBytes(path_TSD, new byte[length]);

                    // A real run takes seconds; written microseconds after the start, the file system's coarser clock can
                    // stamp the file just BEFORE it. Stamp it with the precise clock, as TAS's later write would be.
                    File.SetLastWriteTimeUtc(path_TSD, DateTime.UtcNow);
                }

                if (errorLog != null)
                {
                    File.WriteAllText(Path_ErrorLog, errorLog);
                }

                return code;
            };
        }

        [Test]
        public void D1_ReturnCodeZero_IsFailure()
        {
            Assert.That(Simulate(Tas(0, 22, "Simulation Failed"), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.NativeReturnCode, Is.EqualTo(0));
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("returned 0"));
        }

        [Test]
        public void D1_ReturnCodeZero_WithANonTrivialTSD_IsStillFailure()
        {
            Assert.That(Simulate(Tas(0, 22 * 1024), out SimulationEvidence _), Is.False);
        }

        [Test]
        public void D2_MinusOne_WithAFreshValidTSD_Succeeds()
        {
            Assert.That(Simulate(Tas(-1, 1034856), out SimulationEvidence simulationEvidence), Is.True, string.Join(" | ", simulationEvidence.Refusals));
            Assert.That(simulationEvidence.Completed, Is.True);
        }

        [Test]
        public void D3_MinusOne_WithNoTSD_IsFailure()
        {
            Assert.That(Simulate(Tas(-1, -1), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("no output file"));
        }

        [Test]
        public void D3_MinusOne_WithAnEmptyTSD_IsFailure()
        {
            Assert.That(Simulate(Tas(-1, 0), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("is 0 bytes"));
        }

        [Test]
        public void D3_MinusOne_WithATrivialTSD_IsFailure()
        {
            Assert.That(Simulate(Tas(-1, 22), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("22 bytes"));
        }

        [Test]
        public void D3_MinusOne_WithALeftoverTSD_ThatTASDidNotRewrite_IsFailure()
        {
            // The old overload's false positive: a previous run's full TSD, unlocked, read as this run's.
            File.WriteAllBytes(path_TSD, new byte[1034856]);

            Assert.That(Simulate(Tas(-1, -1), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(File.Exists(path_TSD), Is.False, "the leftover was cleared before the run, so it cannot be inherited");
        }

        [Test]
        public void D3_MinusOne_WithAStaleTSD_IsFailure()
        {
            Func<int> stale = () =>
            {
                File.WriteAllBytes(path_TSD, new byte[1034856]);
                File.SetLastWriteTimeUtc(path_TSD, DateTime.UtcNow.AddHours(-1));
                return -1;
            };

            Assert.That(Simulate(stale, out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("predates this run"));
        }

        [Test]
        public void D4_MinusOne_WithAnAttributableErrorLog_IsFailure()
        {
            Assert.That(Simulate(Tas(-1, 1034856, "Simulation Failed"), out SimulationEvidence simulationEvidence), Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS wrote an error log for this run: " + Path_ErrorLog));
        }

        [Test]
        public void D4_AnEarlierRunsErrorLog_IsClearedAndNotChargedToThisRun()
        {
            File.WriteAllText(Path_ErrorLog, "Simulation Failed");

            Assert.That(Simulate(Tas(-1, 1034856), out SimulationEvidence simulationEvidence), Is.True, string.Join(" | ", simulationEvidence.Refusals));
            Assert.That(File.Exists(Path_ErrorLog), Is.False);
        }

        [Test]
        public void PublicDocumentOverloads_KeepTheirSignaturesAndRefuseNullInputs()
        {
            Assert.That(typeof(SAM.Analytical.Tas.Modify).GetMethod("Simulate", new[] { typeof(TBD.TBDDocument), typeof(string), typeof(int), typeof(int) }), Is.Not.Null);
            Assert.That(typeof(SAM.Analytical.Tas.Modify).GetMethod("Simulate", new[] { typeof(SAMTBDDocument), typeof(string), typeof(int), typeof(int) }), Is.Not.Null);
            Assert.That(SAM.Analytical.Tas.Modify.Simulate((TBD.TBDDocument)null, path_TSD, 1, 365), Is.False);
            Assert.That(SAM.Analytical.Tas.Modify.Simulate((SAMTBDDocument)null, path_TSD, 1, 365), Is.False);
        }
    }
}
