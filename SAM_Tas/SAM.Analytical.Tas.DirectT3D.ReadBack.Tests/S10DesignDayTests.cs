// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using SAM.Analytical.Tas.DirectT3D.Validation.Results;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 revision-1 review findings R1 (design-day index and day-origin convention) and R2 (design-day coverage), through
    /// the full path: synthetic reference inputs and subject, reference, comparison. Each malformed or unsupported case must
    /// BLOCK through its intended rows while the rows that do not depend on it keep their verdict; the unmodified control
    /// and the justified no-design-day case PASS.
    /// </summary>
    [TestFixture]
    public class S10DesignDayTests
    {
        private const string A = S10SyntheticTriple.ZoneA, B = S10SyntheticTriple.ZoneB;

        private static (ResultReferenceModel reference, ResultComparison comparison) Run(Action<ResultCase> inputs = null, Action<ResultSubjectSnapshot> subject = null, bool designDays = true, bool heatingTimeSignal = true)
        {
            ResultInputs serialised = S10SyntheticTriple.Inputs(designDays, heatingTimeSignal);
            ResultCase copy = ResultCase.From(serialised);
            inputs?.Invoke(copy);
            ResultSubjectSnapshot captured = copy.Subject(null);
            subject?.Invoke(captured);
            ResultReferenceModel reference = ResultReference.Build(copy.Tsd, copy.Tbd, copy.Coverage, serialised.Rules);
            return (reference, ResultCompare.Run(reference, captured));
        }

        private static RowStatus Status(ResultComparison comparison, string zone, string loadType, string quantity)
        {
            return comparison.Rows.Single(x => x.Layer == "SAM" && x.ZoneName == zone && x.LoadType == loadType && x.Quantity == quantity).Status;
        }

        private static CalibrationCheck Check(ResultReferenceModel reference, string id) => reference.Checks.Single(x => x.Id == id);

        private static string NotPass(ResultComparison comparison)
        {
            return string.Join("\n", comparison.Rows.Where(x => x.Status != RowStatus.Pass).Take(40).Select(x => x.Id + " (" + x.ZoneName + "): " + x.Status + " - " + x.Reason));
        }

        private static readonly string[] DesignAndLegacy = { "designDayPeak.load", "designDayPeak.hourOfDay", "designDayPeak.name", "legacy.sizingMethod", "legacy.load", "legacy.loadIndex" };

        private static void AssertRoleBlocked(ResultComparison comparison, string loadType)
        {
            foreach (string zone in new[] { A, B })
            {
                foreach (string quantity in DesignAndLegacy)
                {
                    Assert.That(Status(comparison, zone, loadType, quantity), Is.EqualTo(RowStatus.Blocked), zone + " " + loadType + " " + quantity);
                }

                Assert.That(Status(comparison, zone, loadType, "annualPeak.load"), Is.EqualTo(RowStatus.Pass), "the annual peak does not depend on the design days");
            }
        }

        [Test]
        public void Control_Passes_WithTheConventionAndCoverageVerified()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run();

            Assert.That(comparison.Verdict, Is.EqualTo("PASS"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C9").Status, Is.EqualTo("verified"), Check(reference, "S10-C9").Answer);
            Assert.That(Check(reference, "S10-C10").Status, Is.EqualTo("verified"));
            Assert.That(Check(reference, "S10-C9").Evidence, Has.Some.Contains("fits at offset(s) [0]"));
        }

        // ---- R1 ---------------------------------------------------------------------------------------------------------

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-23)]
        public void UnexpectedDesignDayLowerBound_BlocksTheHourAndIndexRows_NotTheLoads(int lowerBound)
        {
            // The review probe: every design-day series moved to another bound with the same values and an unchanged subject.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.SelectMany(x => x.Zones))
                {
                    foreach (ResultSeries series in zone.Arrays.Values) series.LowerBound = lowerBound;
                }
            });

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C9").Status, Is.EqualTo("unresolved"));
            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("lower bound " + lowerBound));

            // Hour of day and the design-governed legacy index depend on the convention: BLOCKED.
            Assert.That(Status(comparison, A, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Blocked), "zone B heating: the design day governs");

            // Loads, names, the sizing method, annual-governed indices and a zero peak's absent hour do not.
            foreach (string zone in new[] { A, B })
            {
                foreach (string type in new[] { "Heating", "Cooling" })
                {
                    foreach (string quantity in new[] { "designDayPeak.load", "designDayPeak.name", "legacy.sizingMethod", "legacy.load" })
                    {
                        Assert.That(Status(comparison, zone, type, quantity), Is.EqualTo(RowStatus.Pass), zone + " " + type + " " + quantity);
                    }
                }
            }

            Assert.That(Status(comparison, A, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Pass), "annual governs");
            Assert.That(Status(comparison, B, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass), "a zero peak has no hour whatever the convention");
        }

        [Test]
        public void DesignPeakIndexContradictingTheSeries_BlocksTheIndexRows()
        {
            // Tas's own index is one later than the CLR index of the maximum: the index convention is contradicted.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                TsdDesignDataset heating = copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating");
                heating.PeakZoneGains.Cells[2] = heating.PeakZoneGains.Cells[2].Select(x => x.Value == 0 ? x : NativeField.Of(x.Value + 1)).ToList();
            });

            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("contradicted"));
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass), "the cooling role keeps its own evidence");
            Assert.That(Status(comparison, B, "Heating", "legacy.load"), Is.EqualTo(RowStatus.Pass));
        }

        [Test]
        public void AnnualPeakTableUnread_LeavesTheNativeIndexUnresolved()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Tsd.AnnualPeakZoneGains.Clear());

            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("annual GetPeakZoneGains heatingLoad table was not read"));
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Pass), "the annual index rests on N-5, not on the table");
        }

        [Test]
        public void TimeSignalOneHourLate_BlocksTheHourOfDay_NotTheIndex()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.Single(x => x.Role == "Cooling").Zones)
                {
                    double[] values = zone.Arrays["lightingGain"].Values;
                    zone.Arrays["lightingGain"].Values = new[] { values[23] }.Concat(values.Take(23)).ToArray();
                }
            });

            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("offset(s) [1]"));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass), "the heating role has its own signal");
        }

        [Test]
        public void HeatingDesignDayWithoutATimeSignal_BlocksOnlyItsHourOfDay()
        {
            // As in the S08 PR-C fixtures: the HDD condition has no gains, so nothing places the heating design day in time.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(heatingTimeSignal: false);

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"));
            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("Heating: no independent time signal"));
            Assert.That(Status(comparison, A, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Pass), "the native index is established without a time signal");
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass));
            Assert.That(comparison.Rows.Where(x => x.Status != RowStatus.Pass).Select(x => x.Quantity).Distinct(), Is.EqualTo(new[] { "designDayPeak.hourOfDay" }));
        }

        [Test]
        public void Offsets_FindOnlyTheTrueShift()
        {
            double[] schedule = Enumerable.Range(0, 24).Select(S10SyntheticTriple.LightingShape).ToArray();
            double[] late = Enumerable.Range(0, 48).Select(p => 3 * schedule[((p - 5) % 24 + 24) % 24]).ToArray();

            Assert.That(ResultReference.Offsets(late, schedule), Is.EqualTo(new[] { 5 }));
            Assert.That(ResultReference.Offsets(late, Enumerable.Repeat(1.0, 24).ToArray()), Is.Empty, "a constant schedule never fits a varying gain");
            Assert.That(ResultReference.Offsets(Enumerable.Repeat(2.0, 24).ToArray(), Enumerable.Repeat(1.0, 24).ToArray()), Has.Count.EqualTo(24), "a constant schedule fits every offset: no evidence");
        }

        // ---- R2 ---------------------------------------------------------------------------------------------------------

        [Test]
        public void AllDatasetsMissing_WhileTheTbdHoldsGroups_Blocks_EvenWhenTheSubjectOmitsThemToo()
        {
            // The review probe: the TSD lost every design-day data set and SAM matches the omission (no DesignDayPeak, the
            // annual peak projected into the legacy fields).
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Tsd.DesignDatasets.Clear(), subject =>
            {
                foreach (SubjectSpaceResult result in subject.Spaces.SelectMany(x => x.Results))
                {
                    result.DesignDayPeak = null;
                    result.SizingMethod = "Simulation";
                    result.Load = result.AnnualPeak.Load == 0 ? -1 : result.AnnualPeak.Load;
                    result.LoadIndex = result.AnnualPeak.HourOfYear.HasValue ? result.AnnualPeak.HourOfYear.Value + 1 : 0;
                }
            });

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C10").Status, Is.EqualTo("unresolved"));
            Assert.That(Check(reference, "S10-C10").Answer, Does.Contain("expected but missing in the TSD: 'CDD'").And.Contain("expected but missing in the TSD: 'HDD'"));
            AssertRoleBlocked(comparison, "Heating");
            AssertRoleBlocked(comparison, "Cooling");
        }

        [Test]
        public void OneRoleMissing_BlocksThatRoleOnly()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Tsd.DesignDatasets.RemoveAll(x => x.Role == "Heating"));

            Assert.That(Check(reference, "S10-C10").Answer, Does.Contain("Heating design-day coverage: expected but missing in the TSD: 'HDD'"));
            AssertRoleBlocked(comparison, "Heating");
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.load"), Is.EqualTo(RowStatus.Pass));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass));
        }

        [Test]
        public void OneExpectedGroupMissing_BlocksThatRole()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Coverage.Groups.Add(new DesignCoverageGroup { Role = "Cooling", Index = 1, Name = "CDD 2", Days = { new DesignCoverageDay { Index = 0, DayType = "CDD", Repetitions = NativeField.Of(1) } } }));

            Assert.That(Check(reference, "S10-C10").Answer, Does.Contain("expected but missing in the TSD: 'CDD 2'"));
            AssertRoleBlocked(comparison, "Cooling");
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Pass));
        }

        [Test]
        public void DuplicateDataset_BlocksThatRole()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                TsdDesignDataset heating = copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating");
                TsdDesignDataset duplicate = System.Text.Json.JsonSerializer.Deserialize<TsdDesignDataset>(System.Text.Json.JsonSerializer.Serialize(heating, SnapshotJson.Options), SnapshotJson.Options);
                duplicate.Index = 1;
                copy.Tsd.DesignDatasets.Add(duplicate);
            });

            Assert.That(Check(reference, "S10-C10").Answer, Does.Contain("duplicate TSD data sets: 'HDD' x2"));
            AssertRoleBlocked(comparison, "Heating");
        }

        [Test]
        public void UnexpectedDataset_TheTbdDoesNotHold_BlocksThatRole()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Coverage.Groups.RemoveAll(x => x.Role == "Cooling"));

            Assert.That(Check(reference, "S10-C10").Answer, Does.Contain("in the TSD but not a TBD group: 'CDD'"));
            AssertRoleBlocked(comparison, "Cooling");
        }

        [Test]
        public void UnreadableOrAbsentCoverage_Blocks()
        {
            (ResultReferenceModel unreadable, ResultComparison groupUnreadable) = Run(copy => copy.Coverage.Groups.Single(x => x.Role == "Heating").Detail = "design day 0 unreadable: COMException");
            Assert.That(Check(unreadable, "S10-C10").Answer, Does.Contain("Heating design-day coverage is unreadable"));
            AssertRoleBlocked(groupUnreadable, "Heating");

            (ResultReferenceModel failed, ResultComparison readFailed) = Run(copy => copy.Coverage.Errors.Add("Cooling design-day group 1 could not be read: COMException"));
            Assert.That(Check(failed, "S10-C10").Answer, Does.Contain("coverage is unreadable"));
            AssertRoleBlocked(readFailed, "Heating");
            AssertRoleBlocked(readFailed, "Cooling");

            (ResultReferenceModel absent, ResultComparison notRead) = Run(copy => copy.Coverage = null);
            Assert.That(Check(absent, "S10-C10").Answer, Does.Contain("no TBD design-day coverage was read"));
            AssertRoleBlocked(notRead, "Heating");
            AssertRoleBlocked(notRead, "Cooling");
        }

        [Test]
        public void JustifiedNoDesignDays_Passes()
        {
            // The completely read TBD holds no design-day group, the TSD no data set, SAM no design-day peak: an expected absence.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(designDays: false);

            Assert.That(comparison.Verdict, Is.EqualTo("PASS"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C10").Status, Is.EqualTo("verified"));
            Assert.That(Check(reference, "S10-C10").Evidence, Has.Some.Contains("no design-day peak is expected"));
            Assert.That(Check(reference, "S10-C9").Status, Is.EqualTo("not-exercised"));
            Assert.That(comparison.Rows.Where(x => x.Quantity == "designDayPeak.load").Select(x => x.Expected), Is.All.EqualTo("no value"));
            Assert.That(comparison.Rows.Where(x => x.Quantity == "legacy.sizingMethod").Select(x => x.Expected), Is.All.EqualTo("'Simulation'"));
        }

        [Test]
        public void NoDesignDaysExpected_ButTheSubjectHoldsDesignDayPeaks_Fails()
        {
            (_, ResultComparison comparison) = Run(designDays: false, subject: subject =>
            {
                foreach (SubjectSpaceResult result in subject.Spaces.SelectMany(x => x.Results))
                {
                    result.DesignDayPeak = new SubjectPeak { Basis = "DesignDay", Load = 100, HourOfDay = 7, DesignDayName = "invented" };
                }
            });

            Assert.That(comparison.Verdict, Is.EqualTo("FAIL"));
            Assert.That(Status(comparison, A, "Heating", "designDayPeak.load"), Is.EqualTo(RowStatus.Fail));
        }

        [Test]
        public void NegativeMatrix_CoversTheNewDesignDayControls()
        {
            List<NegativeOutcome> outcomes = ResultNegatives.Run(S10SyntheticTriple.Inputs(), "PASS");
            string[] cases = { "dd-lower-bound-0", "dd-lower-bound-2", "dd-lower-bound-minus-23", "dd-peak-table-absent", "dd-time-signal-shifted", "dd-all-datasets-missing", "dd-role-missing", "dd-duplicate-dataset", "dd-group-missing", "dd-unexpected-dataset", "dd-coverage-unreadable", "dd-coverage-absent" };

            foreach (string name in cases)
            {
                NegativeOutcome outcome = outcomes.Single(x => x.Case == name);
                Assert.That(outcome.Applicable, Is.True, name + ": " + outcome.Applied);
                Assert.That(outcome.AsIntended, Is.True, name + " " + outcome.Verdict + ": " + string.Join("; ", outcome.Evidence));
                Assert.That(outcome.Verdict, Is.EqualTo("BLOCKED"), name);
            }
        }
    }
}
