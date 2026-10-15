// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation;
using SAM.Core;
using SAM.Core.Tas;
using TasModify = SAM.Analytical.Tas.Modify;
using TasQuery = SAM.Analytical.Tas.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// Unified Diagnostics, phase 4B (TAS). Helpers shared by the fixtures in this file.
    /// <para>
    /// Everything here is COM-free and needs no TAS install or licence: real <see cref="SimulationEvidence"/> runs over real files in a temp directory,
    /// real <see cref="Log"/> records from <c>Create.Log</c>, real <see cref="T3DImportReport"/>s from the real <c>T3DImportPlan</c> over the synthetic
    /// models, and the real <c>WorkflowCalculator.Calculate</c> refusal gates (the part of the workflow that runs before any TAS type is touched).
    /// </para>
    /// </summary>
    internal static class DiagnosticsAssert
    {
        /// <summary>A comparable identity of a record: everything the adapters set. Used to compare detail levels and repeated runs.</summary>
        public static string Key(Diagnostic diagnostic)
        {
            return string.Join("|", diagnostic.Severity, diagnostic.Detail, diagnostic.Code, diagnostic.SourceIndex, diagnostic.Subject, diagnostic.Message);
        }

        public static List<string> Keys(IEnumerable<Diagnostic> diagnostics)
        {
            return diagnostics.Select(Key).ToList();
        }

        /// <summary>
        /// The two invariants of <see cref="DiagnosticDetail"/>: Information only narrows (Summary is contained in Standard, which is contained in Detailed,
        /// order preserved), and every Warning and Error survives every level, in the same order.
        /// </summary>
        public static void AssertDetailLevels(Func<DiagnosticDetail, Diagnostics> collect)
        {
            List<string> summary = Keys(collect(DiagnosticDetail.Summary));
            List<string> standard = Keys(collect(DiagnosticDetail.Standard));
            List<string> detailed = Keys(collect(DiagnosticDetail.Detailed));

            Assert.That(summary.Intersect(standard).Count(), Is.EqualTo(summary.Count), "Summary must be contained in Standard");
            Assert.That(standard.Intersect(detailed).Count(), Is.EqualTo(standard.Count), "Standard must be contained in Detailed");
            Assert.That(detailed.Where(summary.Contains).ToList(), Is.EqualTo(summary), "Summary records keep their order within Detailed");

            foreach (DiagnosticSeverity severity in new[] { DiagnosticSeverity.Warning, DiagnosticSeverity.Error })
            {
                List<string> at(DiagnosticDetail detail) => Keys(collect(detail).Where(x => x.Severity == severity));
                Assert.That(at(DiagnosticDetail.Summary), Is.EqualTo(at(DiagnosticDetail.Detailed)), severity + " records must be kept at Summary exactly as at Detailed");
                Assert.That(at(DiagnosticDetail.Standard), Is.EqualTo(at(DiagnosticDetail.Detailed)), severity + " records must be kept at Standard exactly as at Detailed");
            }
        }
    }

    /// <summary>
    /// <see cref="SimulationEvidenceExtensions"/>-free: <c>Query.ToDiagnostics(SimulationEvidence)</c>. The evidence is the one funnel every TAS simulation
    /// (TBD to TSD, the TPD in-place run, the no-IZAM source, the thermostat bridge and the system-ventilation route) records its stages in.
    /// </summary>
    [TestFixture]
    public class SimulationEvidenceDiagnosticsTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_Tas_Diagnostics_Evidence", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
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

        private string File_(string name, long length = 4096)
        {
            string result = Path.Combine(directory, name);
            File.WriteAllBytes(result, new byte[length]);
            return result;
        }

        private SimulationEvidence CompletedRun(out string path_TBD, out string path_TSD)
        {
            path_TBD = File_("m.tbd");
            path_TSD = Path.Combine(directory, "m.tsd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TSD, new byte[64 * 1024]);
            simulationEvidence.RecordCallReturned("Done");
            simulationEvidence.Conclude();
            return simulationEvidence;
        }

        private SimulationEvidence FailedRun_MissingOutput(out string path_TBD, out string path_TSD)
        {
            path_TBD = File_("m.tbd");
            path_TSD = Path.Combine(directory, "m.tsd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow);
            simulationEvidence.RecordCallReturned();
            simulationEvidence.Conclude();
            return simulationEvidence;
        }

        [Test]
        public void CompletedRun_IsInformationOnly_WithTheNotesAsTheirMessagesInOrder()
        {
            SimulationEvidence simulationEvidence = CompletedRun(out string path_TBD, out _);
            Assert.That(simulationEvidence.Completed, Is.True, string.Join(" | ", simulationEvidence.Refusals));

            Diagnostics diagnostics = simulationEvidence.ToDiagnostics();

            Assert.That(diagnostics.HasErrors, Is.False);
            Assert.That(diagnostics.Any(x => x.Severity != DiagnosticSeverity.Info), Is.False, "a completed run has no Warning and no Error");

            List<Diagnostic> notes = diagnostics.Where(x => x.Code == TasDiagnosticCodes.SimulationNote).ToList();
            Assert.That(notes.Select(x => x.Message).ToList(), Is.EqualTo(simulationEvidence.Notes), "message, content and order are the legacy notes");
            Assert.That(notes.Select(x => x.SourceIndex).ToList(), Is.EqualTo(Enumerable.Range(0, notes.Count).Select(x => (int?)x).ToList()));
            Assert.That(notes.All(x => x.Subject == path_TBD && x.Detail == DiagnosticDetail.Standard), Is.True, "the subject is the simulated document, per-stage notes are Standard");

            Diagnostic completed = diagnostics.Single(x => x.Code == TasDiagnosticCodes.SimulationCompleted);
            Assert.That(completed.Detail, Is.EqualTo(DiagnosticDetail.Summary));
            Assert.That(completed.Message, Is.EqualTo(simulationEvidence.ToString()));
            Assert.That(diagnostics.Last(), Is.SameAs(completed), "the outcome comes last");
        }

        [Test]
        public void Refusals_AreErrors_AtEveryDetailLevel_WithTheirLegacyText_AndNothingIsDropped()
        {
            SimulationEvidence simulationEvidence = FailedRun_MissingOutput(out string path_TBD, out string path_TSD);
            Assert.That(simulationEvidence.Refusals, Is.EqualTo(new[] { "The run produced no output file: " + path_TSD }), "fixture: the legacy refusal");

            foreach (DiagnosticDetail detail in Enum.GetValues(typeof(DiagnosticDetail)))
            {
                Diagnostics diagnostics = simulationEvidence.ToDiagnostics(detail);
                List<Diagnostic> errors = diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).ToList();

                Assert.That(errors.Select(x => x.Message).ToList(), Is.EqualTo(simulationEvidence.Refusals), detail + ": every refusal kept, text unchanged, in order");
                Assert.That(errors.All(x => x.Code == TasDiagnosticCodes.SimulationRefusal && x.Subject == path_TBD), Is.True);
                Assert.That(errors.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0 }.ToList()));
                Assert.That(diagnostics.HasErrors, Is.True, detail.ToString());
                Assert.That(diagnostics.Any(x => x.Code == TasDiagnosticCodes.SimulationCompleted), Is.False, "a refused run is never reported as completed");
            }
        }

        [Test]
        public void SeveralRefusals_KeepTheirOrderAndIndices()
        {
            string path_TBD = File_("m.tbd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.Undefined, path_TBD, null);
            simulationEvidence.Refuse("second");
            simulationEvidence.Refuse("third");

            List<Diagnostic> errors = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary).Where(x => x.Severity == DiagnosticSeverity.Error).ToList();

            Assert.That(errors.Select(x => x.Message).ToList(), Is.EqualTo(simulationEvidence.Refusals));
            Assert.That(errors, Has.Count.EqualTo(3));
            Assert.That(errors.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1, 2 }.ToList()));
        }

        [Test]
        public void ATasErrorLog_IsAnError_WithTheLogTextVerbatim()
        {
            string path_TBD = File_("m.tbd");
            string path_TSD = Path.Combine(directory, "m.tsd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TSD, new byte[64 * 1024]);
            File.WriteAllText(simulationEvidence.Path_ErrorLog, "Zone 3 has no surfaces." + Environment.NewLine + "Model rejected.");
            simulationEvidence.RecordCallReturned();
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Completed, Is.False, "fixture: TAS wrote an error log for this run");

            Diagnostic error = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary).Single(x => x.Severity == DiagnosticSeverity.Error);
            Assert.That(error.Message, Is.EqualTo(simulationEvidence.Refusals.Single()));
            Assert.That(error.Message, Does.Contain("Zone 3 has no surfaces.").And.Contain("Model rejected."));
        }

        [TestCase("Done", false, 0)]
        [TestCase("Sizing Flow Failed", true, 0)]
        [TestCase("Plant Room Has Errors", true, 0)]
        [TestCase("Failed to open the TSD file", true, 0)]
        [TestCase("Everything is rosy", false, 1)]
        [TestCase("   ", false, 0)]
        [TestCase(null, false, 0)]
        public void TheNativeAnswer_IsMappedByTheEvidenceKind_NotByItsWords(string answer, bool refused, int unrecognisedWarnings)
        {
            string path_TBD = File_("m.tbd");
            string path_TSD = Path.Combine(directory, "m.tsd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, path_TBD, path_TSD);
            simulationEvidence.Prepare(DateTime.UtcNow.AddSeconds(-1));
            File.WriteAllBytes(path_TSD, new byte[64 * 1024]);
            simulationEvidence.RecordCallReturned(answer);
            simulationEvidence.Conclude();

            Diagnostics diagnostics = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary);

            Assert.That(diagnostics.Count(x => x.Code == TasDiagnosticCodes.SimulationUnrecognisedAnswer), Is.EqualTo(unrecognisedWarnings));
            Assert.That(diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error), Is.EqualTo(refused), "only a measured failure refuses, and the refusal is the Error");

            if (unrecognisedWarnings == 1)
            {
                Diagnostic warning = diagnostics.Single(x => x.Code == TasDiagnosticCodes.SimulationUnrecognisedAnswer);
                Assert.That(warning.Severity, Is.EqualTo(DiagnosticSeverity.Warning), "an unclassified TAS answer is never Information and never invented as an Error");
                Assert.That(warning.Message, Does.Contain("\"" + answer + "\""));
                Assert.That(diagnostics.Any(x => x.Code == TasDiagnosticCodes.SimulationCompleted), Is.True, "it does not refuse the run: the legacy decision is unchanged");
            }
        }

        [Test]
        public void AnInPlaceRun_NotYetReconciled_IsAWarning_ThenCompletes()
        {
            string path_TPD = File_("m.tpd");
            SimulationEvidence simulationEvidence = new SimulationEvidence(SimulationOutputShape.InPlaceDocument, path_TPD, null);
            simulationEvidence.Prepare(DateTime.UtcNow);
            File.WriteAllBytes(path_TPD, new byte[8192]);
            simulationEvidence.RecordCallReturned("Done");
            simulationEvidence.Conclude();

            Assert.That(simulationEvidence.Refusals, Is.Empty, "fixture: nothing refused, but not reconciled either");
            Assert.That(simulationEvidence.Completed, Is.False);

            Diagnostic notEvidenced = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary).Single(x => x.Code == TasDiagnosticCodes.SimulationNotEvidenced);
            Assert.That(notEvidenced.Severity, Is.EqualTo(DiagnosticSeverity.Warning), "an unproven run is not reported as Information");

            simulationEvidence.RecordResultsReconciled(3, 0, 23);
            Diagnostics after = simulationEvidence.ToDiagnostics(DiagnosticDetail.Summary);
            Assert.That(after.Any(x => x.Code == TasDiagnosticCodes.SimulationNotEvidenced), Is.False);
            Assert.That(after.Any(x => x.Code == TasDiagnosticCodes.SimulationCompleted), Is.True);
        }

        [Test]
        public void DetailLevels_OnlyNarrowInformation_AndKeepEveryWarningAndError()
        {
            SimulationEvidence completed = CompletedRun(out _, out _);
            SimulationEvidence failed = FailedRun_MissingOutput(out _, out _);
            DiagnosticsAssert.AssertDetailLevels(detail => completed.ToDiagnostics(detail));
            DiagnosticsAssert.AssertDetailLevels(detail => failed.ToDiagnostics(detail));

            // Summary of a completed run is the one outcome line; Standard adds the stage notes.
            Diagnostics summary = completed.ToDiagnostics(DiagnosticDetail.Summary);
            Assert.That(summary.Select(x => x.Code).ToList(), Is.EqualTo(new[] { TasDiagnosticCodes.SimulationCompleted }));
            Assert.That(completed.ToDiagnostics(DiagnosticDetail.Standard).Count, Is.GreaterThan(summary.Count));
        }

        [Test]
        public void TheAdapter_DoesNotChangeTheEvidence_AndIsDeterministic()
        {
            SimulationEvidence simulationEvidence = FailedRun_MissingOutput(out _, out _);

            List<string> notes = simulationEvidence.Notes;
            List<string> refusals = simulationEvidence.Refusals;
            string text = simulationEvidence.ToString();
            bool completed = simulationEvidence.Completed;

            List<string> first = DiagnosticsAssert.Keys(simulationEvidence.ToDiagnostics());
            List<string> second = DiagnosticsAssert.Keys(simulationEvidence.ToDiagnostics());

            Assert.That(second, Is.EqualTo(first), "repeatable");
            Assert.That(simulationEvidence.Notes, Is.EqualTo(notes));
            Assert.That(simulationEvidence.Refusals, Is.EqualTo(refusals));
            Assert.That(simulationEvidence.ToString(), Is.EqualTo(text));
            Assert.That(simulationEvidence.Completed, Is.EqualTo(completed));
        }

        [Test]
        public void TheLegacyText_IsByteIdenticalToWhatTheProducerAlwaysWrote()
        {
            // Pinned literals, taken from the producer source: a change to the adapter or to the evidence that rewrote a legacy string fails here.
            SimulationEvidence simulationEvidence = FailedRun_MissingOutput(out string path_TBD, out string path_TSD);

            Assert.That(simulationEvidence.Notes, Is.EqualTo(new[] { "The TAS call returned without throwing." }));
            Assert.That(simulationEvidence.Refusals, Is.EqualTo(new[] { "The run produced no output file: " + path_TSD }));
            Assert.That(
                simulationEvidence.ToString(),
                Is.EqualTo("Simulation NOT evidenced (SeparateOutputFile): " + path_TBD + " - The run produced no output file: " + path_TSD));
        }

        [Test]
        public void NullAndMalformedEvidence_NeverThrow_AndNeverLookClean()
        {
            Assert.That(((SimulationEvidence)null).ToDiagnostics().Count, Is.EqualTo(0));

            SimulationEvidence malformed = new SimulationEvidence(SimulationOutputShape.Undefined, null, null);
            Diagnostics diagnostics = null;
            Assert.DoesNotThrow(() => diagnostics = malformed.ToDiagnostics(DiagnosticDetail.Summary));

            Assert.That(diagnostics.HasErrors, Is.True, "a run with no document and no shape is refused, not Information");
            Assert.That(diagnostics.All(x => x.Subject == "TAS simulation"), Is.True, "the subject has a stable fallback");
            Assert.That(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Error).Select(x => x.Message).ToList(), Is.EqualTo(malformed.Refusals));

            SimulationEvidence callFailed = new SimulationEvidence(SimulationOutputShape.SeparateOutputFile, File_("m.tbd"), Path.Combine(directory, "m.tsd"));
            callFailed.Prepare(DateTime.UtcNow);
            callFailed.RecordCallFailed("the building was null");
            Assert.That(callFailed.ToDiagnostics(DiagnosticDetail.Summary).Single(x => x.Severity == DiagnosticSeverity.Error).Message, Is.EqualTo("The TAS call did not complete: the building was null"));
        }
    }

    /// <summary>
    /// <c>Query.ToDiagnostics(Log)</c>, over the log <c>Create.Log</c> builds for the thermal transmittance calculation data.
    /// </summary>
    [TestFixture]
    public class LogDiagnosticsTests
    {
        [Test]
        public void EveryLogRecordType_IsMappedOneToOne_AndTheUnstatedOneIsNeverInformation()
        {
            Log log = new Log();
            log.Add("an error", LogRecordType.Error);
            log.Add("a warning", LogRecordType.Warning);
            log.Add("a message", LogRecordType.Message);
            log.Add(new LogRecord("no type stated"));

            List<Diagnostic> diagnostics = log.ToDiagnostics("Construction X", DiagnosticDetail.Summary).ToList();

            Assert.That(diagnostics.Select(x => x.Message).ToList(), Is.EqualTo(new[] { "an error", "a warning", "no type stated" }), "Summary keeps Warning/Error and the unstated record; the Message record is Standard");
            Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Error, DiagnosticSeverity.Warning, DiagnosticSeverity.Warning }));
            Assert.That(diagnostics.Select(x => x.Code).ToList(), Is.EqualTo(new[] { TasDiagnosticCodes.LogError, TasDiagnosticCodes.LogWarning, TasDiagnosticCodes.LogUnclassified }));
            Assert.That(diagnostics.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1, 3 }.ToList()), "the index is the position in the log");
            Assert.That(diagnostics.All(x => x.Subject == "Construction X"), Is.True);

            Diagnostic message = log.ToDiagnostics(null, DiagnosticDetail.Standard).Single(x => x.Code == TasDiagnosticCodes.LogMessage);
            Assert.That(message.Severity, Is.EqualTo(DiagnosticSeverity.Info));
            Assert.That(message.Detail, Is.EqualTo(DiagnosticDetail.Standard));
        }

        [Test]
        public void TheRealProducer_ItsErrorsStayErrors_AndItsLegacyTextIsUnchanged()
        {
            ConstructionCalculationData data = new ConstructionCalculationData() { ConstructionName = null, ConstructionNames = new HashSet<string>(), ThermalTransmittance = double.NaN };
            Log log = data.Log();

            string legacy = log.ToString();
            List<string> texts = log.Select(x => x.Text).ToList();
            Assert.That(texts, Is.EqualTo(new[]
            {
                "Source Construction name has not been provided",
                "Construction names have not been provided for construction ???",
                "Thermal transmittance has invalid value for construction ???",
            }), "fixture: the legacy text of the producer");

            Diagnostics diagnostics = log.ToDiagnostics("???");

            Assert.That(diagnostics.Select(x => x.Message).ToList(), Is.EqualTo(texts));
            Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Info, DiagnosticSeverity.Error, DiagnosticSeverity.Error }));
            Assert.That(diagnostics.HasErrors, Is.True);
            Assert.That(log.ToString(), Is.EqualTo(legacy), "the adapter does not change the log");
            Assert.That(log.Select(x => x.Text).ToList(), Is.EqualTo(texts));
        }

        [Test]
        public void DetailLevels_OnlyNarrowInformation_AndKeepEveryWarningAndError()
        {
            Log log = new Log();
            log.Add("e", LogRecordType.Error);
            log.Add("w", LogRecordType.Warning);
            log.Add("m", LogRecordType.Message);
            log.Add(new LogRecord("u"));

            DiagnosticsAssert.AssertDetailLevels(detail => log.ToDiagnostics("s", detail));

            Assert.That(log.ToDiagnostics("s", DiagnosticDetail.Summary).Count, Is.EqualTo(3));
            Assert.That(log.ToDiagnostics("s", DiagnosticDetail.Standard).Count, Is.EqualTo(4));
        }

        [Test]
        public void NullEmptyAndNullRecords_AreHandled()
        {
            Assert.That(((Log)null).ToDiagnostics().Count, Is.EqualTo(0));
            Assert.That(new Log().ToDiagnostics().Count, Is.EqualTo(0));

            // A log cannot hold a null record through its API; an empty-text record is kept as a record, not dropped.
            Log log = new Log();
            log.Add((string)null, LogRecordType.Error);
            Diagnostic diagnostic = log.ToDiagnostics(null, DiagnosticDetail.Summary).Single();
            Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error));
            Assert.That(diagnostic.Message, Is.EqualTo(string.Empty));
        }
    }

    /// <summary>
    /// <c>Query.ToDiagnostics(T3DImportReport)</c>: the direct SAM to T3D conversion. The reports come from the real <c>T3DImportPlan</c> over the same
    /// synthetic buildings the direct-route tests use.
    /// </summary>
    [TestFixture]
    public class T3DImportReportDiagnosticsTests
    {
        private static T3DImportReport SampleReport()
        {
            T3DImportReport report = new T3DImportReport() { Zones = 2, Success = true };
            report.Skipped.Add("Panel P1: no geometry.");
            report.Skipped.Add("Shade panel S1: not imported, ImportShades is off.");
            report.Notes.Add("Panel P2: its space's shell is not closed, so the outward side could not be verified; the panel's own normal was used.");
            report.ZoneNames[new Guid("00000000-0000-0000-0000-0000000000b2")] = "Zone B";
            report.ZoneNames[new Guid("00000000-0000-0000-0000-0000000000b1")] = "Zone A";
            return report;
        }

        [Test]
        public void Mapping_FollowsTheListTheProducerUsed()
        {
            T3DImportReport report = SampleReport();

            List<Diagnostic> all = report.ToDiagnostics().ToList();

            Assert.That(all.Select(x => x.Code).ToList(), Is.EqualTo(new[]
            {
                TasDiagnosticCodes.T3DTotals,
                TasDiagnosticCodes.T3DSkipped, TasDiagnosticCodes.T3DSkipped,
                TasDiagnosticCodes.T3DNote,
                TasDiagnosticCodes.T3DZoneCreated, TasDiagnosticCodes.T3DZoneCreated,
            }), "order: totals, skipped, notes, zones");

            Assert.That(all.Where(x => x.Code == TasDiagnosticCodes.T3DSkipped).Select(x => x.Severity).Distinct().ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Warning }));
            Assert.That(all.Where(x => x.Code == TasDiagnosticCodes.T3DSkipped).Select(x => x.Message).ToList(), Is.EqualTo(report.Skipped), "legacy text and order");
            Assert.That(all.Where(x => x.Code == TasDiagnosticCodes.T3DSkipped).Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1 }.ToList()));
            Assert.That(all.Single(x => x.Code == TasDiagnosticCodes.T3DNote).Message, Is.EqualTo(report.Notes.Single()));
            Assert.That(all.Single(x => x.Code == TasDiagnosticCodes.T3DNote).Severity, Is.EqualTo(DiagnosticSeverity.Info));
            Assert.That(all.Single(x => x.Code == TasDiagnosticCodes.T3DTotals).Message, Is.EqualTo(report.ToString()));
            Assert.That(all.Any(x => x.Severity == DiagnosticSeverity.Error), Is.False, "a successful conversion has no Error");

            // Zones: the SAM space GUID is the subject; ordered by zone name.
            List<Diagnostic> zones = all.Where(x => x.Code == TasDiagnosticCodes.T3DZoneCreated).ToList();
            Assert.That(zones.Select(x => x.Subject).ToList(), Is.EqualTo(new[] { "Space 00000000-0000-0000-0000-0000000000b1", "Space 00000000-0000-0000-0000-0000000000b2" }));
            Assert.That(zones[0].Message, Does.Contain("'Zone A'"));
        }

        [Test]
        public void ADidNotCompleteConversion_IsAnError_AtEveryLevel_AndNeverInformation()
        {
            T3DImportReport report = SampleReport();
            report.Success = false;
            report.Skipped.Add("TAS reported that it could not create the imported model (CreateImportedModel returned false).");

            foreach (DiagnosticDetail detail in Enum.GetValues(typeof(DiagnosticDetail)))
            {
                Diagnostics diagnostics = report.ToDiagnostics(detail);
                Assert.That(diagnostics.Count(x => x.Code == TasDiagnosticCodes.T3DConversionFailed && x.Severity == DiagnosticSeverity.Error), Is.EqualTo(1), detail.ToString());
                Assert.That(diagnostics.Count(x => x.Severity == DiagnosticSeverity.Warning), Is.EqualTo(3), detail + ": every skipped entry stays a Warning");
                Assert.That(diagnostics.HasErrors, Is.True);
            }
        }

        [Test]
        public void DetailLevels_OnlyNarrowInformation_AndKeepEveryWarningAndError()
        {
            DiagnosticsAssert.AssertDetailLevels(detail => SampleReport().ToDiagnostics(detail));

            T3DImportReport failed = SampleReport();
            failed.Success = false;
            DiagnosticsAssert.AssertDetailLevels(detail => failed.ToDiagnostics(detail));

            Assert.That(SampleReport().ToDiagnostics(DiagnosticDetail.Summary).Select(x => x.Code).ToList(), Is.EqualTo(new[] { TasDiagnosticCodes.T3DTotals, TasDiagnosticCodes.T3DSkipped, TasDiagnosticCodes.T3DSkipped }), "Summary: totals and what was not imported");
            Assert.That(SampleReport().ToDiagnostics(DiagnosticDetail.Standard).Count(x => x.Code == TasDiagnosticCodes.T3DNote), Is.EqualTo(1));
            Assert.That(SampleReport().ToDiagnostics(DiagnosticDetail.Standard).Count(x => x.Code == TasDiagnosticCodes.T3DZoneCreated), Is.EqualTo(0));
            Assert.That(SampleReport().ToDiagnostics(DiagnosticDetail.Detailed).Count(x => x.Code == TasDiagnosticCodes.T3DZoneCreated), Is.EqualTo(2));
        }

        [Test]
        public void NullAndEmptyReports_AreHandled()
        {
            Assert.That(((T3DImportReport)null).ToDiagnostics().Count, Is.EqualTo(0));

            Diagnostics empty = new T3DImportReport() { Success = true }.ToDiagnostics();
            Assert.That(empty.Select(x => x.Code).ToList(), Is.EqualTo(new[] { TasDiagnosticCodes.T3DTotals }));
        }

        [Test]
        public void TheAdapter_DoesNotChangeTheReport()
        {
            T3DImportReport report = SampleReport();
            string text = report.ToString();
            List<string> skipped = new List<string>(report.Skipped);
            List<string> notes = new List<string>(report.Notes);
            Dictionary<Guid, string> zones = new Dictionary<Guid, string>(report.ZoneNames);

            List<string> first = DiagnosticsAssert.Keys(report.ToDiagnostics());
            List<string> second = DiagnosticsAssert.Keys(report.ToDiagnostics());

            Assert.That(second, Is.EqualTo(first));
            Assert.That(report.ToString(), Is.EqualTo(text));
            Assert.That(report.Skipped, Is.EqualTo(skipped));
            Assert.That(report.Notes, Is.EqualTo(notes));
            Assert.That(report.ZoneNames, Is.EqualTo(zones));
        }

        [Test]
        public void ARealPlan_SkippedShadesAreWarnings_AndThePlanIsUntouched()
        {
            AnalyticalModel model = SyntheticModels.BoxWithShade();
            T3DImportPlan plan = model.T3DImportPlan(new ToT3DOptions { ImportShades = false });

            Assume.That(plan.Report.Skipped, Is.Not.Empty, "fixture: ImportShades off skips the shade panel");

            plan.Report.Success = true; // what ToT3D(plan, document) sets once TAS has built the model

            string totals = plan.Report.ToString();
            List<string> skipped = new List<string>(plan.Report.Skipped);
            List<string> notes = new List<string>(plan.Report.Notes);
            int elements = plan.Elements.Count;
            int zones = plan.Zones.Count;
            List<double> widths = plan.Elements.Select(x => x.Width).ToList();

            Diagnostics diagnostics = plan.Report.ToDiagnostics(DiagnosticDetail.Summary);

            Assert.That(diagnostics.Where(x => x.Severity == DiagnosticSeverity.Warning).Select(x => x.Message).ToList(), Is.EqualTo(skipped), "every skipped entry reaches the diagnostics as a Warning, text unchanged");
            Assert.That(plan.Report.Skipped, Is.EqualTo(skipped));
            Assert.That(plan.Report.Notes, Is.EqualTo(notes));
            Assert.That(plan.Report.ToString(), Is.EqualTo(totals));
            Assert.That(plan.Elements.Count, Is.EqualTo(elements));
            Assert.That(plan.Zones.Count, Is.EqualTo(zones));
            Assert.That(plan.Elements.Select(x => x.Width).ToList(), Is.EqualTo(widths), "element widths are not touched");
        }

        [Test]
        public void ARealPlan_PhysicalThickness_IsReportedAsANote_AndIsNotChangedByTheAdapter()
        {
            AnalyticalModel model = SyntheticModels.Box();
            AdjacencyCluster cluster = model.AdjacencyCluster;
            foreach (Panel panel in cluster.GetPanels().Where(x => x.PanelType == PanelType.WallExternal).ToList())
            {
                panel.SetValue(PanelSourceParameter.PhysicalThickness, 0.25);
                cluster.AddObject(panel);
            }

            T3DImportPlan plan = new AnalyticalModel(model, cluster).T3DImportPlan();
            T3DElementSpec wall = plan.Elements.Single(x => x.Description == SyntheticModels.WallConstruction.Guid.ToString("D"));
            double width = wall.Width;
            double construction = SyntheticModels.WallConstruction.GetThickness(false);

            Assert.That(width, Is.EqualTo(0.25).Within(1e-9), "fixture: the source physical thickness is the T3D width");
            Assert.That(construction, Is.EqualTo(0.30).Within(1e-9), "fixture: the layered ConstructionThickness is independent");

            plan.Report.Success = true;
            Diagnostics diagnostics = plan.Report.ToDiagnostics(DiagnosticDetail.Standard);

            Assert.That(diagnostics.Where(x => x.Code == TasDiagnosticCodes.T3DNote).Select(x => x.Message).ToList(), Is.EqualTo(plan.Report.Notes));
            Assert.That(diagnostics.Any(x => x.Severity == DiagnosticSeverity.Warning || x.Severity == DiagnosticSeverity.Error), Is.False, "preserving the physical thickness is a note, not a warning");
            Assert.That(wall.Width, Is.EqualTo(width), "the adapter did not change the T3D width");
            Assert.That(SyntheticModels.WallConstruction.GetThickness(false), Is.EqualTo(construction), "nor the construction thickness: the two stay independent");
        }

        [Test]
        public void ARealPlan_WithApertures_ProducesNoPerApertureWarnings()
        {
            // Opening controls are optional. A model with openings and no opening properties must not turn into one Warning per aperture.
            T3DImportPlan plan = SyntheticModels.Box(3).T3DImportPlan();
            plan.Report.Success = true;

            Diagnostics diagnostics = plan.Report.ToDiagnostics(DiagnosticDetail.Detailed);

            Assert.That(plan.Report.Openings, Is.EqualTo(3), "fixture: three openings");
            Assert.That(diagnostics.Any(x => x.Severity != DiagnosticSeverity.Info), Is.False);
        }
    }

    /// <summary>
    /// <c>WorkflowCalculator.GetDiagnostics</c> and <c>TasQuery.StepNotesDiagnostics</c>: the funnel for everything the workflow tells its caller.
    /// The calculator's refusal gates run before any TAS type is touched, so they are exercised for real.
    /// </summary>
    [TestFixture]
    public class WorkflowDiagnosticsTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "SAM_Tas_Diagnostics_Workflow", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
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

        private static AnalyticalModel Model()
        {
            return new AnalyticalModel("Diagnostics Fixture", null, null, null, new AdjacencyCluster());
        }

        public static IEnumerable<TestCaseData> RefusalGates()
        {
            yield return new TestCaseData("gbXML+canonical", "This run was given both a gbXML to convert and a canonical TBD to start from, which are contradictory instructions - one says the geometry must be converted, the other that it already is. Nothing was run. Supply one or the other.");
            yield return new TestCaseData("direct+canonical", "This run was asked to convert the geometry directly (T3DRoute.Direct) and also to start from a canonical TBD, which are contradictory instructions - one says the geometry must be converted, the other that it already is. Nothing was run. Supply one or the other.");
            yield return new TestCaseData("canonical-missing", null);
            yield return new TestCaseData("canonical-is-target", null);
            yield return new TestCaseData("canonical-copy-fails", null);
        }

        private WorkflowCalculator Calculator(string gate, out string expected)
        {
            string path_TBD = Path.Combine(directory, "Round.tbd");
            string path_Canonical = Path.Combine(directory, "Canonical.tbd");
            expected = null;

            switch (gate)
            {
                case "gbXML+canonical":
                    File.WriteAllText(path_Canonical, "canonical");
                    return new WorkflowCalculator(new WorkflowSettings() { Path_TBD = path_TBD, Path_TBD_Canonical = path_Canonical, Path_gbXML = Path.Combine(directory, "Model.xml") });

                case "direct+canonical":
                    File.WriteAllText(path_Canonical, "canonical");
                    return new WorkflowCalculator(new WorkflowSettings() { Path_TBD = path_TBD, Path_TBD_Canonical = path_Canonical, T3DRoute = T3DRoute.Direct });

                case "canonical-missing":
                    expected = string.Format("The canonical TBD '{0}' this run was to start from does not exist, so there is nothing to warm start from and nothing was run. Convert the model in full instead.", path_Canonical);
                    return new WorkflowCalculator(new WorkflowSettings() { Path_TBD = path_TBD, Path_TBD_Canonical = path_Canonical });

                case "canonical-is-target":
                    File.WriteAllText(path_TBD, "baseline");
                    expected = string.Format("The canonical TBD and this run's TBD are the same file ('{0}'), so warm starting would write to the baseline every later run depends on. Nothing was run - give the run its own TBD path.", path_TBD);
                    return new WorkflowCalculator(new WorkflowSettings() { Path_TBD = path_TBD, Path_TBD_Canonical = path_TBD });

                default:
                    File.WriteAllText(path_Canonical, "canonical");
                    return new WorkflowCalculator(new WorkflowSettings() { Path_TBD = Path.Combine(directory, "no-such-folder", "Round.tbd"), Path_TBD_Canonical = path_Canonical });
            }
        }

        [TestCaseSource(nameof(RefusalGates))]
        public void ARefusedRun_IsAnError_AtEveryDetailLevel_AndTheLegacyNoteIsUnchanged(string gate, string expectedLegacy)
        {
            WorkflowCalculator workflowCalculator = Calculator(gate, out string expected);
            expectedLegacy = expectedLegacy ?? expected;

            Assert.That(workflowCalculator.Calculate(Model()), Is.Null, "fixture: the gate refuses the run");
            Assert.That(workflowCalculator.Notes, Has.Count.EqualTo(1));

            if (expectedLegacy != null)
            {
                Assert.That(workflowCalculator.Notes[0], Is.EqualTo(expectedLegacy), "the legacy note is byte-identical to what the producer always wrote");
            }
            else
            {
                Assert.That(workflowCalculator.Notes[0], Does.StartWith("The canonical TBD '").And.Contain("could not be copied to '").And.Contain("so this run has no TBD to work on and nothing was run: "));
            }

            foreach (DiagnosticDetail detail in Enum.GetValues(typeof(DiagnosticDetail)))
            {
                Diagnostics diagnostics = workflowCalculator.GetDiagnostics(detail);
                Assert.That(diagnostics.Count, Is.EqualTo(1), detail.ToString());

                Diagnostic diagnostic = diagnostics.Single();
                Assert.That(diagnostic.Severity, Is.EqualTo(DiagnosticSeverity.Error), detail + ": a refused run is never downgraded");
                Assert.That(diagnostic.Code, Is.EqualTo(TasDiagnosticCodes.WorkflowRefused));
                Assert.That(diagnostic.Message, Is.EqualTo(workflowCalculator.Notes[0]), "the structured message is the legacy note");
                Assert.That(diagnostic.SourceIndex, Is.EqualTo(0));
                Assert.That(diagnostics.HasErrors, Is.True);
            }
        }

        [Test]
        public void TheDiagnostics_AreEmptyBeforeTheFirstRun_AndEachRunReportsOnlyItsOwn()
        {
            WorkflowCalculator workflowCalculator = Calculator("gbXML+canonical", out _);
            Assert.That(workflowCalculator.GetDiagnostics().Count, Is.EqualTo(0));

            workflowCalculator.Calculate(Model());
            workflowCalculator.Calculate(Model());

            Assert.That(workflowCalculator.Notes, Has.Count.EqualTo(1), "legacy: notes are cleared per run");
            Assert.That(workflowCalculator.GetDiagnostics().Count, Is.EqualTo(1), "the diagnostics are cleared with them");

            Assert.That(workflowCalculator.Calculate(null), Is.Null);
            Assert.That(workflowCalculator.Notes, Is.Empty);
            Assert.That(workflowCalculator.GetDiagnostics().Count, Is.EqualTo(0), "a rejected call leaves no earlier run's diagnostics visible either");
        }

        [Test]
        public void TheDiagnostics_AreIndexAlignedWithTheNotes_ForTheNotesAStepReturns()
        {
            WorkflowCalculator workflowCalculator = new WorkflowCalculator(new WorkflowSettings());
            MethodInfo addNotes = typeof(WorkflowCalculator).GetMethod("AddNotes", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(addNotes, Is.Not.Null, "the private funnel the steps' notes go through");

            List<string> stepNotes = new List<string>
            {
                "Building elements: 4 opening(s) requested an availability schedule, 4 of those read a schedule back off the TBD profile.",
                TasModify.NotePrefix_Issue + "Building elements: SAM aperture 'W1' could not be rebound.",
                "Aperture definitions: 3 aperture part(s) considered; 3 rebound.",
            };
            addNotes.Invoke(workflowCalculator, new object[] { stepNotes });

            Assert.That(workflowCalculator.Notes, Is.EqualTo(stepNotes), "the legacy list is the steps' lines, byte for byte, marker included");

            List<Diagnostic> diagnostics = workflowCalculator.GetDiagnostics().ToList();
            Assert.That(diagnostics.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1, 2 }.ToList()));
            Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Info, DiagnosticSeverity.Warning, DiagnosticSeverity.Info }));
            Assert.That(diagnostics[1].Message, Is.EqualTo("Building elements: SAM aperture 'W1' could not be rebound."), "the marker is dropped from the structured message: the severity carries it");
            Assert.That(diagnostics.All(x => x.Subject == "Workflow"), Is.True, "no step is running: the stable fallback subject");
            Assert.That(diagnostics.Select(x => x.Code).ToList(), Is.EqualTo(new[] { TasDiagnosticCodes.WorkflowNote, TasDiagnosticCodes.WorkflowIssue, TasDiagnosticCodes.WorkflowNote }));

            DiagnosticsAssert.AssertDetailLevels(detail => workflowCalculator.GetDiagnostics(detail));
        }

        [Test]
        public void StepNotes_AreMappedByTheProducersMarker_NotByWhatTheTextSays()
        {
            List<string> notes = new List<string>
            {
                // Words that sound like trouble but carry no marker: the producers' contract says they are outcomes, so they are Information.
                "Building elements: 2 of 5 stated feature shade(s) failed to be written, an error the TBD did not report.",
                // An issue that sounds harmless: the marker makes it a Warning.
                TasModify.NotePrefix_Issue + "Aperture types: all fine.",
                null,
                "Aperture definitions: nothing to reuse.",
            };

            List<Diagnostic> diagnostics = TasQuery.StepNotesDiagnostics(notes, "Step").ToList();

            Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Info, DiagnosticSeverity.Warning, DiagnosticSeverity.Info }));
            Assert.That(diagnostics.Select(x => x.SourceIndex).ToList(), Is.EqualTo(new int?[] { 0, 1, 3 }.ToList()), "a null entry is skipped but counted");
            Assert.That(diagnostics.Any(x => x.Severity == DiagnosticSeverity.Error), Is.False, "the steps report problems as issues and carry on: nothing is an Error");
            Assert.That(diagnostics.All(x => x.Subject == "Step"), Is.True);
            Assert.That(diagnostics[1].Message, Is.EqualTo("Aperture types: all fine."));

            Assert.That(TasQuery.StepNotesDiagnostics(null).Count, Is.EqualTo(0));
            Assert.That(TasQuery.StepNotesDiagnostics(new List<string>()).Count, Is.EqualTo(0));
            Assert.That(TasQuery.IsIssueNote(TasModify.NotePrefix_Issue + "x"), Is.True);
            Assert.That(TasQuery.IsIssueNote("x " + TasModify.NotePrefix_Issue), Is.False, "the marker is a prefix, not a substring");
            Assert.That(TasQuery.IsIssueNote(null), Is.False);

            DiagnosticsAssert.AssertDetailLevels(detail => TasQuery.StepNotesDiagnostics(notes, "Step", detail));
        }

        [Test]
        public void ADirectT3DSkippedEntry_IsAWarning_AndAFailedConversionIsAnError()
        {
            // The two lines the calculator writes from a T3DImportReport carry the issue marker; the failed conversion also stops the run, so it is an Error.
            WorkflowCalculator workflowCalculator = new WorkflowCalculator(new WorkflowSettings());
            MethodInfo addNote = typeof(WorkflowCalculator).GetMethod("AddNote", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(addNote, Is.Not.Null);

            addNote.Invoke(workflowCalculator, new object[] { TasModify.NotePrefix_Issue + "Direct T3D conversion: Panel P1: no geometry.", DiagnosticSeverity.Warning, DiagnosticDetail.Summary, TasDiagnosticCodes.WorkflowIssue, null });
            addNote.Invoke(workflowCalculator, new object[] { TasModify.NotePrefix_Issue + "Direct T3D conversion: the SAM model could not be converted into a T3D, so nothing further was run.", DiagnosticSeverity.Error, DiagnosticDetail.Summary, TasDiagnosticCodes.WorkflowRefused, null });

            List<Diagnostic> diagnostics = workflowCalculator.GetDiagnostics(DiagnosticDetail.Summary).ToList();
            Assert.That(diagnostics.Select(x => x.Severity).ToList(), Is.EqualTo(new[] { DiagnosticSeverity.Warning, DiagnosticSeverity.Error }));
            Assert.That(diagnostics[1].Message, Does.StartWith("Direct T3D conversion: the SAM model could not be converted"), "the marker is not part of the structured message");
            Assert.That(workflowCalculator.Notes[1], Does.StartWith(TasModify.NotePrefix_Issue), "but the legacy note keeps it");
        }
    }

    /// <summary>
    /// What phase 4B must not change: the public surface the Grasshopper components and the saved definitions depend on, and the codes it publishes.
    /// </summary>
    [TestFixture]
    public class DiagnosticsCompatibilityTests
    {
        private static MethodInfo[] Methods(Type type, string name)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).Where(x => x.Name == name).ToArray();
        }

        [Test]
        public void TheExistingPublicSurface_IsStillThere_WithTheSameShapes()
        {
            // WorkflowCalculator.Notes is still the text list the components show as runtime messages.
            PropertyInfo notes = typeof(WorkflowCalculator).GetProperty("Notes");
            Assert.That(notes.PropertyType, Is.EqualTo(typeof(IReadOnlyList<string>)));

            MethodInfo[] calculate = Methods(typeof(WorkflowCalculator), "Calculate");
            Assert.That(calculate.Select(x => x.GetParameters().Length).OrderBy(x => x).ToList(), Is.EqualTo(new[] { 1, 2 }), "both Calculate overloads");
            Assert.That(calculate.All(x => x.ReturnType == typeof(AnalyticalModel)), Is.True);

            // SimulationEvidence: the lists and flags the TBD/TPD callers read.
            Assert.That(typeof(SimulationEvidence).GetProperty("Notes").PropertyType, Is.EqualTo(typeof(List<string>)));
            Assert.That(typeof(SimulationEvidence).GetProperty("Refusals").PropertyType, Is.EqualTo(typeof(List<string>)));
            Assert.That(typeof(SimulationEvidence).GetProperty("Completed").PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(typeof(SimulationEvidence).GetProperty("NativeDiagnostic").PropertyType, Is.EqualTo(typeof(string)));

            // T3DImportReport: the lists and totals.
            Assert.That(typeof(T3DImportReport).GetProperty("Skipped").PropertyType, Is.EqualTo(typeof(List<string>)));
            Assert.That(typeof(T3DImportReport).GetProperty("Notes").PropertyType, Is.EqualTo(typeof(List<string>)));
            Assert.That(typeof(T3DImportReport).GetProperty("Success").PropertyType, Is.EqualTo(typeof(bool)));

            // The producers whose string lists the steps return are untouched: same name, same out parameter.
            foreach (string name in new[] { "UpdateBuildingElements", "UpdateApertureDefinitions", "SetApertureTypes" })
            {
                Assert.That(Methods(typeof(TasModify), name).Any(x => x.GetParameters().Any(p => p.ParameterType == typeof(List<string>).MakeByRefType() && p.Name == "notes")), Is.True, name);
            }

            Assert.That(Methods(typeof(SAM.Analytical.Tas.Convert), "ToT3D").Any(x => x.ReturnType == typeof(bool) && x.GetParameters().Any(p => p.ParameterType == typeof(T3DImportReport).MakeByRefType())), Is.True);
            Assert.That(Methods(typeof(SAM.Analytical.Tas.Create), "Log").All(x => x.ReturnType == typeof(Log)), Is.True);
        }

        [Test]
        public void TheAdaptersAreAdditive_ExtensionsAndNewMembersOnly()
        {
            Assert.That(Methods(typeof(SAM.Core.Tas.Query), "ToDiagnostics").Length, Is.EqualTo(2), "SimulationEvidence and Log");
            Assert.That(Methods(typeof(TasQuery), "ToDiagnostics").Length, Is.EqualTo(1), "T3DImportReport");
            Assert.That(typeof(WorkflowCalculator).GetMethod("GetDiagnostics"), Is.Not.Null);
            Assert.That(typeof(WorkflowCalculator).GetProperty("Notes").CanWrite, Is.False, "Notes stays read only");
        }

        [Test]
        public void TheReleasedCodes_AreStable_Unique_AndPrefixed()
        {
            Dictionary<string, string> released = new Dictionary<string, string>
            {
                { "SimulationRefusal", "TAS-SIM-001" }, { "SimulationNote", "TAS-SIM-002" }, { "SimulationUnrecognisedAnswer", "TAS-SIM-003" },
                { "SimulationNotEvidenced", "TAS-SIM-004" }, { "SimulationCompleted", "TAS-SIM-005" },
                { "LogMessage", "TAS-LOG-001" }, { "LogWarning", "TAS-LOG-002" }, { "LogError", "TAS-LOG-003" }, { "LogUnclassified", "TAS-LOG-004" },
                { "T3DConversionFailed", "TAS-T3D-001" }, { "T3DSkipped", "TAS-T3D-002" }, { "T3DNote", "TAS-T3D-003" }, { "T3DTotals", "TAS-T3D-004" }, { "T3DZoneCreated", "TAS-T3D-005" },
                { "WorkflowRefused", "TAS-WF-001" }, { "WorkflowNote", "TAS-WF-002" }, { "WorkflowIssue", "TAS-WF-003" },
            };

            Dictionary<string, string> actual = typeof(TasDiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.IsLiteral)
                .ToDictionary(x => x.Name, x => (string)x.GetRawConstantValue());

            foreach (KeyValuePair<string, string> pair in released)
            {
                Assert.That(actual.ContainsKey(pair.Key), Is.True, pair.Key + " was removed");
                Assert.That(actual[pair.Key], Is.EqualTo(pair.Value), pair.Key + " changed value");
            }

            Assert.That(actual.Values.Distinct().Count(), Is.EqualTo(actual.Count), "codes are unique");
            Assert.That(actual.Values.All(x => x.StartsWith("TAS-")), Is.True);
        }
    }
}
