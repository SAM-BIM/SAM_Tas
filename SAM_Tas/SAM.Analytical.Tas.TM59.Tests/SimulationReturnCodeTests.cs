// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Core;
using SAM.Core.Tas;
using System;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// S01a. <c>TBDDocument.simulate</c> returns <c>-1</c> when the simulation ran and <c>0</c> when TAS refused it,
    /// and a refusal still writes a stub TSD (measured: 22 bytes and <c>&lt;basename&gt;_error_log.txt</c> =
    /// "Simulation Failed", for a zone with an unbalanced inter-zone air movement). The return code is what decides;
    /// anything other than <c>-1</c> or <c>0</c> fails closed; <c>-1</c> is still judged on the output and error log.
    /// <para>
    /// No TAS: <see cref="SimulationEvidence"/> is COM-free, so the COM call is stood in for by the code it would
    /// return and real files on disk.
    /// </para>
    /// </summary>
    [TestFixture]
    public class SimulationReturnCodeTests
    {
        private string directory;
        private string path_TBD;
        private string path_TSD;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_S01a_ReturnCode", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path_TBD = Path.Combine(directory, "m.tbd");
            File.WriteAllBytes(path_TBD, new byte[4096]);
            path_TSD = Path.Combine(directory, "m.tsd");
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

        /// <summary>Prepare, let "TAS" write a TSD of <paramref name="length"/> bytes (none when negative), record <paramref name="nativeReturnCode"/>, conclude.</summary>
        private SimulationEvidence Run(object nativeReturnCode, long length, string errorLog = null)
        {
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));

            if (length >= 0)
            {
                File.WriteAllBytes(path_TSD, new byte[length]);
            }

            if (errorLog != null)
            {
                File.WriteAllText(SimulationEvidence.ErrorLogPath(path_TBD), errorLog);
            }

            simulationEvidence.RecordCallReturned(nativeReturnCode);
            simulationEvidence.Conclude();
            return simulationEvidence;
        }

        private string Path_ErrorLog => SimulationEvidence.ErrorLogPath(path_TBD);

        // ------------------------------------------------------------------ A1: 0 is a refusal

        [Test]
        public void A1_ReturnCodeZero_IsARefusal_NamingTheCodeAndTheErrorLog()
        {
            SimulationEvidence simulationEvidence = Run(0, 64 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False, "0 is TAS refusing the run, whatever file it left");
                Assert.That(simulationEvidence.CallReturned, Is.False);
                Assert.That(simulationEvidence.NativeReturnCode, Is.EqualTo(0));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS refused the simulation: TBDDocument.simulate returned 0."));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS return code: 0; TAS error log: " + Path_ErrorLog));
            });
        }

        [Test]
        public void A1_RecordCallReturned_AnswersTheVerdict()
        {
            SimulationEvidence ran = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            SimulationEvidence refused = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);

            Assert.Multiple(() =>
            {
                Assert.That(ran.RecordCallReturned((object)-1), Is.True);
                Assert.That(refused.RecordCallReturned((object)0), Is.False);
            });
        }

        // ------------------------------------------------------------------ A2: -1 with a fresh TSD completes

        [Test]
        public void A2_ReturnCodeMinusOne_WithAFreshTSD_Completes()
        {
            SimulationEvidence simulationEvidence = Run(-1, 1034856);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.True, string.Join(" | ", simulationEvidence.Refusals));
                Assert.That(simulationEvidence.NativeReturnCode, Is.EqualTo(-1));
                Assert.That(simulationEvidence.Refusals, Is.Empty);
                Assert.That(simulationEvidence.Notes, Has.Some.Contains("TAS returned -1 from TBDDocument.simulate"));
            });
        }

        // ------------------------------------------------------------------ A3: anything else fails closed

        [TestCase(1)]
        [TestCase(-2)]
        [TestCase(2)]
        [TestCase(int.MinValue)]
        public void A3_UnexpectedIntegerCode_FailsClosed_AndKeepsTheRawValue(int code)
        {
            SimulationEvidence simulationEvidence = Run(code, 64 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False, "an unknown code is not success, even with a good-looking TSD");
                Assert.That(simulationEvidence.NativeReturnCode, Is.EqualTo(code));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains(string.Format("unexpected code {0} from TBDDocument.simulate", code)));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS error log: " + Path_ErrorLog));
            });
        }

        [Test]
        public void A3_NullCode_FailsClosed()
        {
            SimulationEvidence simulationEvidence = Run(null, 64 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False);
                Assert.That(simulationEvidence.NativeReturnCode, Is.Null);
                Assert.That(simulationEvidence.NativeReturnCodeRecorded, Is.True);
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("no return code (null)"));
            });
        }

        [Test]
        public void A3_NonIntegerCode_FailsClosed_AndKeepsTheRawValue()
        {
            SimulationEvidence asDouble = Run(-1.0, 64 * 1024);
            SimulationEvidence asString = Run("-1", 64 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(asDouble.Completed, Is.False, "-1.0 is not the integer TAS returns");
                Assert.That(asDouble.NativeReturnCode, Is.Null);
                Assert.That(asDouble.Refusals, Has.Some.Contains("(\"-1\", System.Double)"));

                Assert.That(asString.Completed, Is.False, "a string boxed as object is not an integer return code");
                Assert.That(asString.Refusals, Has.Some.Contains("(\"-1\", System.String)"));
            });
        }

        [Test]
        public void A3_OtherIntegralTypes_AreReadAsTheirValue()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Run((short)-1, 64 * 1024).Completed, Is.True);
                Assert.That(Run(-1L, 64 * 1024).Completed, Is.True);
                Assert.That(Run(0L, 64 * 1024).Completed, Is.False);
                Assert.That(Run(long.MaxValue, 64 * 1024).Completed, Is.False, "an integer outside Int32 is not a TAS code");
            });
        }

        // ------------------------------------------------------------------ A4: refusal stubs

        [Test]
        public void A4_TwentyTwoByteRefusalStub_IsRefused()
        {
            // As measured on the licensed box model with one unbalanced IZAM.
            SimulationEvidence simulationEvidence = Run(0, 22, "Simulation Failed");

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False);
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("returned 0"));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("Simulation Failed"));
                Assert.That(simulationEvidence.Refusals, Has.Some.Contains("22 bytes"));
            });
        }

        [Test]
        public void A4_TwentyTwoKilobyteRefusalStub_IsRefusedByTheCode_NotTheSizeThreshold()
        {
            // ~22 KB is above the 1024-byte threshold, so only the return code can catch it: no error log here.
            SimulationEvidence simulationEvidence = Run(0, 22 * 1024);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.ResultLengthAfter, Is.GreaterThan(SimulationEvidence.TrivialOutputLengthThreshold), "fixture: the stub passes the size check");
                Assert.That(simulationEvidence.Completed, Is.False);
                Assert.That(simulationEvidence.Refusals, Has.Count.EqualTo(1));
                Assert.That(simulationEvidence.Refusals[0], Does.Contain("returned 0"));
            });
        }

        // ------------------------------------------------------------------ A5: an error log beats -1

        [Test]
        public void A5_AttributableErrorLog_RefusesEvenWhenTASReturnedMinusOne()
        {
            SimulationEvidence simulationEvidence = Run(-1, 64 * 1024, "Zone 1: Max Pressure Exceeded");

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.Completed, Is.False);
                Assert.That(simulationEvidence.CallReturned, Is.True, "the call itself said it ran");
                string refusal = simulationEvidence.Refusals.Single();
                Assert.That(refusal, Does.StartWith("TAS wrote an error log for this run: " + Path_ErrorLog));
                Assert.That(refusal, Does.Contain("TAS return code: -1"));
                Assert.That(refusal, Does.Contain("Max Pressure Exceeded"));
            });
        }

        // ------------------------------------------------------------------ A6: empty and stale TSD

        [Test]
        public void A6_EmptyTSD_WithMinusOne_IsRefused()
        {
            SimulationEvidence simulationEvidence = Run(-1, 0);

            Assert.That(simulationEvidence.Completed, Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("is 0 bytes"));
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS return code: -1"));
        }

        [Test]
        public void A6_MissingTSD_WithMinusOne_IsRefused()
        {
            SimulationEvidence simulationEvidence = Run(-1, -1);

            Assert.That(simulationEvidence.Completed, Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("no output file"));
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS return code: -1"));
        }

        [Test]
        public void A6_StaleTSD_WithMinusOne_IsRefused()
        {
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow);

            // A file that could not be deleted beforehand and was not rewritten: it predates the run.
            File.WriteAllBytes(path_TSD, new byte[64 * 1024]);
            File.SetLastWriteTimeUtc(path_TSD, DateTime.UtcNow.AddHours(-1));

            simulationEvidence.RecordCallReturned((object)-1);
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Completed, Is.False);
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("predates this run"));
            Assert.That(simulationEvidence.Refusals, Has.Some.Contains("TAS return code: -1"));
        }

        [Test]
        public void A6_LeftoverTSD_IsDeletedBeforeTheRun_SoARefusalCannotInheritIt()
        {
            File.WriteAllBytes(path_TSD, new byte[1034856]);

            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow);
            Assert.That(File.Exists(path_TSD), Is.False, "the previous run's TSD is gone before TAS is called");

            File.WriteAllBytes(path_TSD, new byte[22]);
            simulationEvidence.RecordCallReturned((object)0);
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Completed, Is.False);
        }

        // ------------------------------------------------------------------ A8: diagnostics carry code and log path

        [Test]
        public void A8_Refusal_ReachesTheDiagnostics_AsTASSIM001_WithCodeAndLogPath()
        {
            SimulationEvidence simulationEvidence = Run(0, 22, "Simulation Failed");

            Diagnostics diagnostics = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary);
            Diagnostic[] errors = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(errors, Is.Not.Empty);
                Assert.That(errors.Select(x => x.Code), Is.All.EqualTo(TasDiagnosticCodes.SimulationRefusal));
                Assert.That(errors.Select(x => x.Message), Has.Some.Contains("returned 0"));
                Assert.That(errors.Select(x => x.Message), Has.Some.Contains(Path_ErrorLog));
                Assert.That(diagnostics.Any(x => x.Code == TasDiagnosticCodes.SimulationCompleted), Is.False);
            });
        }

        // ------------------------------------------------------------------ A10: the other recording paths are unchanged

        [Test]
        public void A10_RunsThatRecordNoCode_KeepTheirRefusalTextExactly()
        {
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow);
            simulationEvidence.RecordCallReturned();
            simulationEvidence.Conclude();

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.NativeReturnCodeRecorded, Is.False);
                Assert.That(simulationEvidence.NativeReturnCode, Is.Null);
                Assert.That(simulationEvidence.Refusals, Is.EqualTo(new[] { "The run produced no output file: " + path_TSD }));
            });
        }

        [Test]
        public void A10_TheDiagnosticStringOverload_IsStillChosenForAString()
        {
            // TPD's ITPD.Simulate answers with text; a string argument must keep binding to that overload.
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.InPlaceDocument, path_TBD, null);

            Assert.Multiple(() =>
            {
                Assert.That(simulationEvidence.RecordCallReturned("Done"), Is.True);
                Assert.That(simulationEvidence.NativeDiagnostic, Is.EqualTo("Done"));
                Assert.That(simulationEvidence.NativeReturnCodeRecorded, Is.False);
            });
        }
    }
}
