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

        private static (ResultReferenceModel reference, ResultComparison comparison) Run(Action<ResultCase> inputs = null, Action<ResultSubjectSnapshot> subject = null, bool designDays = true, bool heatingTimeSignal = true, bool heatingFullDay = false)
        {
            ResultInputs serialised = S10SyntheticTriple.Inputs(designDays, heatingTimeSignal, heatingFullDay);
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
            Assert.That(Check(reference, "S10-C7").Status, Is.EqualTo("verified"), Check(reference, "S10-C7").Answer);
            Assert.That(Check(reference, "S10-C9").Status, Is.EqualTo("verified"), Check(reference, "S10-C9").Answer);
            Assert.That(Check(reference, "S10-C10").Status, Is.EqualTo("verified"));
            Assert.That(Check(reference, "S10-C9").Evidence, Has.Some.Contains("fits at offset(s) [0]"));
            Assert.That(Check(reference, "S10-C7").Evidence, Has.Some.Contains("TBD yearDay 182 -> CLR 4345..4368"));
            Assert.That(Check(reference, "S10-C7").Evidence, Has.Some.Contains("TBD yearDay 39 -> CLR 913..936"));
        }

        // ---- the calendar-shaped design day (S10 revision-2 correction, S10-C7) -----------------------------------------

        private static int Start(string role) => S10SyntheticTriple.Start(role);

        private static ResultSeries Load(ResultCase copy, string role, int zone = 0) => copy.Tsd.DesignDatasets.Single(x => x.Role == role).Zones[zone].Arrays[role == "Heating" ? "heatingLoad" : "coolingLoad"];

        private static NativeDesignDay TbdDay(ResultCase copy, string role) => (role == "Heating" ? copy.Tbd.HeatingDesignDays : copy.Tbd.CoolingDesignDays).Single().Days.Single();

        /// <summary>The role is off the calendar shape: S10-C7 unresolved with <paramref name="answer"/>, its design-day and legacy rows BLOCKED, the other role untouched.</summary>
        private static void AssertOffTheCalendar(ResultReferenceModel reference, ResultComparison comparison, string role, string answer)
        {
            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C7").Status, Is.EqualTo("unresolved"));
            Assert.That(Check(reference, "S10-C7").Answer, Does.Contain(answer));
            AssertRoleBlocked(comparison, role);
            string other = role == "Heating" ? "Cooling" : "Heating";
            Assert.That(comparison.Rows.Where(x => x.LoadType == other && x.Status != RowStatus.Pass), Is.Empty, "the other role keeps its verdicts");
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-23)]
        public void UnexpectedDesignDayLowerBound_TakesTheRolesOffTheCalendarShape(int lowerBound)
        {
            // The revision-1 review probe: every design-day series moved to another bound with the same values and an unchanged
            // subject. The expected mask is in CLR indices, so the loads are no longer at the TBD year day: both roles BLOCKED.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.SelectMany(x => x.Zones))
                {
                    foreach (ResultSeries series in zone.Arrays.Values) series.LowerBound = lowerBound;
                }
            });

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C7").Answer, Does.Contain("at lower bound " + lowerBound + "; the calendar representation is 8760 at the N-5 lower bound 1"));
            AssertRoleBlocked(comparison, "Heating");
            AssertRoleBlocked(comparison, "Cooling");
        }

        [Test]
        public void DesignDayMovedOneHourLater_IsOffTheCalendarShape()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (ResultSeries series in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").Zones.SelectMany(x => x.Arrays.Values))
                {
                    series.Values = new[] { series.Values[8759] }.Concat(series.Values.Take(8759)).ToArray();
                }
            });

            AssertOffTheCalendar(reference, comparison, "Heating", "other than Tas's -1 outside the design day CLR 913..936, first");
        }

        [Test]
        public void LoadHourInsideTheDayHoldingTheSentinel_IsOffTheCalendarShape()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => Load(copy, "Cooling").Values[Start("Cooling") + 3] = -1);

            AssertOffTheCalendar(reference, comparison, "Cooling", "1 negative element(s) inside the design day CLR 4345..4368, first -1 at CLR 4348");
        }

        [Test]
        public void ValueOutsideTheDay_IsOffTheCalendarShape()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => Load(copy, "Heating").Values[Start("Heating") + 24] = 0);

            AssertOffTheCalendar(reference, comparison, "Heating", "first 0 at CLR 937");
        }

        [Test]
        public void GainValueOutsideTheDay_IsOffTheCalendarShape_EveryReadSeriesIsChecked()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy => copy.Tsd.DesignDatasets.Single(x => x.Role == "Cooling").Zones[1].Arrays["lightingGain"].Values[0] = 5);

            AssertOffTheCalendar(reference, comparison, "Cooling", "lightingGain holds 1 element(s) other than Tas's -1 outside the design day");
        }

        [Test]
        public void TemperatureOfMinusOneInsideTheDay_IsAValue_NotTheSentinel()
        {
            // Only a load inside the day must be non-negative; -1 C is a valid air temperature.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").Zones) zone.Arrays["dryBulbTemp"].Values[Start("Heating") + 5] = -1;
            });

            Assert.That(comparison.Verdict, Is.EqualTo("PASS"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C7").Status, Is.EqualTo("verified"));
        }

        [Test]
        public void UnreadOrNaNLoadInsideTheDay_IsNeverAZero()
        {
            (ResultReferenceModel unreadReference, ResultComparison unread) = Run(copy =>
            {
                ResultSeries series = Load(copy, "Heating", 1);
                series.Values[Start("Heating") + 6] = double.NaN;
                series.UnreadElements.Add(Start("Heating") + 7);
            });
            AssertOffTheCalendar(unreadReference, unread, "Heating", "1 unread element(s), first at CLR index 919");

            (ResultReferenceModel nanReference, ResultComparison nan) = Run(copy => Load(copy, "Cooling").Values[Start("Cooling") + 14] = double.NaN);
            AssertOffTheCalendar(nanReference, nan, "Cooling", "1 NaN/infinite element(s), first at CLR index 4359");
        }

        [Test]
        public void CompactOrWrongLengthSeries_AreNotTheMeasuredRepresentation()
        {
            // The earlier assumed layout (24 elements per design day) was disproved by the acc1 TSD: it now BLOCKS.
            (ResultReferenceModel compactReference, ResultComparison compact) = Run(copy =>
            {
                foreach (ResultSeries series in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").Zones.SelectMany(x => x.Arrays.Values)) series.Values = ResultReference.Day(series.Values, Start("Heating"));
            });
            AssertOffTheCalendar(compactReference, compact, "Heating", "has 24 elements at lower bound 1; the calendar representation is 8760");

            (ResultReferenceModel shortReference, ResultComparison shortSeries) = Run(copy => Load(copy, "Cooling").Values = Load(copy, "Cooling").Values.Take(8759).ToArray());
            AssertOffTheCalendar(shortReference, shortSeries, "Cooling", "has 8759 elements at lower bound 1");
        }

        [TestCase("unread", "yearDay is Unreadable")]
        [TestCase("absent", "yearDay is not read")]
        [TestCase("plus1", "outside the design day CLR 937..960")]
        [TestCase("zero", "yearDay 0 is not a day 1..365")]
        [TestCase("366", "yearDay 366 is not a day 1..365")]
        [TestCase("fraction", "yearDay 39.5 is not a day 1..365")]
        public void MissingOrWrongYearDay_Blocks(string kind, string answer)
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                NativeDesignDay day = TbdDay(copy, "Heating");
                day.YearDay = kind == "unread" ? NativeField.Unreadable("COMException") : kind == "absent" ? null : NativeField.Of(kind == "plus1" ? 40 : kind == "zero" ? 0 : kind == "366" ? 366 : 39.5);
            });

            AssertOffTheCalendar(reference, comparison, "Heating", answer);
        }

        [Test]
        public void YearDayFromAnotherTbdOrAnotherDay_Blocks()
        {
            (ResultReferenceModel otherTbd, ResultComparison otherTbdComparison) = Run(copy => copy.Tbd.Reader.TbdSha256After = "another");
            Assert.That(Check(otherTbd, "S10-C7").Answer, Does.Contain("are not reads of the same TBD"));
            AssertRoleBlocked(otherTbdComparison, "Heating");
            AssertRoleBlocked(otherTbdComparison, "Cooling");

            (ResultReferenceModel dayType, ResultComparison dayTypeComparison) = Run(copy => TbdDay(copy, "Cooling").DayType = "Weekday");
            AssertOffTheCalendar(dayType, dayTypeComparison, "Cooling", "disagree on the design day (day type 'Weekday' / 'CDD'");

            (ResultReferenceModel renamed, ResultComparison renamedComparison) = Run(copy => copy.Tbd.CoolingDesignDays.Single().Name = "CDD renamed");
            AssertOffTheCalendar(renamed, renamedComparison, "Cooling", "the TBD snapshot holds 0 Cooling group(s) named 'CDD'");
        }

        [Test]
        public void GroupOfSeveralDesignDays_IsNotMeasured_AndBlocks()
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                copy.Coverage.Groups.Single(x => x.Role == "Cooling").Days.Add(new DesignCoverageDay { Index = 1, Name = "CDD day 2", DayType = "CDD", Repetitions = NativeField.Of(1) });
                copy.Tbd.CoolingDesignDays.Single().Days.Add(new NativeDesignDay { Index = 1, Name = "CDD day 2", YearDay = NativeField.Of(183), Repetitions = NativeField.Of(1), DayType = "CDD" });
            });

            AssertOffTheCalendar(reference, comparison, "Cooling", "its TBD group holds 2 design day(s); only the single-design-day calendar representation was measured");
        }

        [Test]
        public void TwoDataSetsOnOneYearDay_AreNotMeasured_AndBlockBothRoles()
        {
            // A consistent overlap: the heating day moved onto the cooling year day (values and the TBD day moved together).
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                TbdDay(copy, "Heating").YearDay = NativeField.Of(S10SyntheticTriple.CoolingYearDay);
                foreach (ResultSeries series in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").Zones.SelectMany(x => x.Arrays.Values))
                {
                    double[] day = ResultReference.Day(series.Values, Start("Heating"));
                    series.Values = Enumerable.Repeat(-1.0, 8760).ToArray();
                    Array.Copy(day, 0, series.Values, Start("Cooling"), 24);
                }

                foreach (List<NativeField> row in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").PeakZoneGains.Cells.Skip(2))
                {
                    for (int i = 0; i < row.Count; i++) if (row[i].Value > 0) row[i] = NativeField.Of(row[i].Value - Start("Heating") + Start("Cooling"));
                }
            });

            Assert.That(comparison.Verdict, Is.EqualTo("BLOCKED"));
            Assert.That(Check(reference, "S10-C7").Answer, Does.Contain("Cooling#0, Heating#0 share year day 182"));
            AssertRoleBlocked(comparison, "Heating");
            AssertRoleBlocked(comparison, "Cooling");
        }

        [Test]
        public void DayPeak_KeepsTheSeriesOwnPositions()
        {
            double[] values = Enumerable.Repeat(-1.0, 8760).ToArray();
            values[912 + 7] = 900;
            values[912 + 9] = 900;

            (double max, List<int> ties) = ResultReference.DayPeak(values, ResultReference.DayStart(39));

            Assert.That(max, Is.EqualTo(900));
            Assert.That(ties, Is.EqualTo(new[] { 919, 921 }), "positions of the whole series: CLR index = position + 1");
        }

        [Test]
        public void DesignGovernedLegacyIndex_IsTheAbsoluteCalendarIndex()
        {
            (ResultReferenceModel reference, ResultComparison control) = Run();
            ResultRow index = control.Rows.Single(x => x.Layer == "SAM" && x.ZoneName == B && x.LoadType == "Heating" && x.Quantity == "legacy.loadIndex");
            Assert.That(index.Status, Is.EqualTo(RowStatus.Pass));
            Assert.That(reference.Rows.Single(x => x.Id == index.Id).Allowed, Is.EqualTo(new[] { (double)(Start("Heating") + 6 + 1) }), "zone B's heating design day governs at hour 6 of year day 39: CLR 919");

            // A day-relative index (the compact layout's) is not Tas's raw index.
            (_, ResultComparison relative) = Run(subject: s => s.Spaces.Single(x => x.Name == B).Results.Single(x => x.LoadType == "Heating").LoadIndex = 7);
            Assert.That(Status(relative, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Fail));
        }

        // ---- R1 ---------------------------------------------------------------------------------------------------------

        [TestCase("outside")]
        [TestCase("non-maximal")]
        public void DesignPeakIndexOutsideTheDayOrAtANonMaximum_BlocksTheIndexRows_NotTheLoads(string kind)
        {
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                TsdDesignDataset heating = copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating");
                int clr = kind == "outside" ? Start("Heating") : Start("Heating") + 1 + 1;
                heating.PeakZoneGains.Cells[2] = heating.PeakZoneGains.Cells[2].Select(x => x.Value == 0 ? x : NativeField.Of(clr)).ToList();
            });

            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("is not the CLR index of a tied maximum"));
            Assert.That(Status(comparison, B, "Heating", "legacy.loadIndex"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.load"), Is.EqualTo(RowStatus.Pass));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass), "the cooling role keeps its own evidence");
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
            // Rotated within the TBD year day's block, so the calendar shape (S10-C7) still holds and only the origin moves.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.Single(x => x.Role == "Cooling").Zones)
                {
                    double[] values = zone.Arrays["lightingGain"].Values, day = ResultReference.Day(values, Start("Cooling"));
                    for (int h = 0; h < 24; h++) values[Start("Cooling") + h] = day[(h + 23) % 24];
                }
            });

            Assert.That(Check(reference, "S10-C7").Status, Is.EqualTo("verified"));
            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("offset(s) [1]"));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, A, "Cooling", "designDayPeak.load"), Is.EqualTo(RowStatus.Pass));
            Assert.That(Status(comparison, A, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Pass), "the heating role has its own signal");
        }

        [Test]
        public void HeatingFullDayTie_WithoutATimeSignal_DerivesEveryHourOfDay()
        {
            // As on the acc1 box: no heating time signal, and each zone's heating design load holds its maximum at all 24
            // hours. Any within-day origin maps the tie set onto itself, so the hour row is derived (all 24 hours accepted).
            (ResultReferenceModel reference, ResultComparison comparison) = Run(heatingTimeSignal: false, heatingFullDay: true);

            Assert.That(comparison.Verdict, Is.EqualTo("PASS"), NotPass(comparison));
            Assert.That(Check(reference, "S10-C9").Status, Is.EqualTo("unresolved"), "the heating origin itself stays unresolved");
            Assert.That(Check(reference, "S10-C9").Answer, Does.Contain("Heating: no independent time signal"));
            Assert.That(Check(reference, "S10-C9").Evidence, Has.Some.Contains("Heating: the within-day origin is unresolved, but every positive design-day zone maximum of the role holds all 24 hours"));
            ReferenceRow hour = reference.Rows.Single(x => x.Layer == "SAM" && x.ZoneName == B && x.LoadType == "Heating" && x.Quantity == "designDayPeak.hourOfDay");
            Assert.That(hour.Allowed, Is.EqualTo(Enumerable.Range(0, 24).Select(x => (double)x)));
            Assert.That(reference.Rows.Single(x => x.Layer == "SAM" && x.ZoneName == B && x.LoadType == "Heating" && x.Quantity == "legacy.loadIndex").Allowed, Is.EqualTo(Enumerable.Range(Start("Heating") + 1, 24).Select(x => (double)x)), "the design-governed index accepts any tied CLR index of the day");

            // The row still requires an hour: an absent or out-of-day hour FAILs.
            (_, ResultComparison absent) = Run(heatingTimeSignal: false, heatingFullDay: true, subject: s => s.Spaces.Single(x => x.Name == B).Results.Single(x => x.LoadType == "Heating").DesignDayPeak.HourOfDay = null);
            Assert.That(Status(absent, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Fail));
            (_, ResultComparison outOfDay) = Run(heatingTimeSignal: false, heatingFullDay: true, subject: s => s.Spaces.Single(x => x.Name == B).Results.Single(x => x.LoadType == "Heating").DesignDayPeak.HourOfDay = 24);
            Assert.That(Status(outOfDay, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Fail));
        }

        [Test]
        public void PartialTieWithoutATimeSignal_StaysBlocked()
        {
            // 23 of 24 hours tied is not the whole day: the origin decides the hour set, so the row stays BLOCKED.
            (ResultReferenceModel reference, ResultComparison comparison) = Run(heatingTimeSignal: false, heatingFullDay: true, inputs: copy =>
            {
                foreach (TsdResultZone zone in copy.Tsd.DesignDatasets.Single(x => x.Role == "Heating").Zones) zone.Arrays["heatingLoad"].Values[Start("Heating") + 23] = 1;
            });

            Assert.That(Status(comparison, A, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Status(comparison, B, "Heating", "designDayPeak.hourOfDay"), Is.EqualTo(RowStatus.Blocked));
            Assert.That(Check(reference, "S10-C9").Evidence, Has.None.Contains("Heating: the within-day origin is unresolved, but"));
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
            string[] cases =
            {
                "dd-lower-bound-0", "dd-lower-bound-2", "dd-lower-bound-minus-23", "dd-peak-table-absent", "dd-time-signal-shifted", "dd-all-datasets-missing", "dd-role-missing", "dd-duplicate-dataset", "dd-group-missing", "dd-unexpected-dataset", "dd-coverage-unreadable", "dd-coverage-absent",
                "dd-day-shifted", "dd-in-day-sentinel", "dd-outside-day-value", "dd-in-day-unread", "dd-in-day-nan", "dd-compact-24", "dd-year-day-unread", "dd-year-day-plus-1", "dd-year-day-0", "dd-peak-index-outside-day", "dd-peak-index-non-maximal",
            };

            foreach (string name in cases)
            {
                NegativeOutcome outcome = outcomes.Single(x => x.Case == name);
                Assert.That(outcome.Applicable, Is.True, name + ": " + outcome.Applied);
                Assert.That(outcome.AsIntended, Is.True, name + " " + outcome.Verdict + ": " + string.Join("; ", outcome.Evidence));
                Assert.That(outcome.Verdict, Is.EqualTo("BLOCKED"), name);
            }
        }

        [Test]
        public void NegativeMatrix_OnABoxLikeInput_ReportsInapplicableCasesHonestly()
        {
            // The acc1 box shape: no heating time signal and a 24-way heating tie. The control PASSes; every applicable case is
            // as intended; a case with no target here (the time-signal shift of a tie-derived hour) is n/a with its reason.
            List<NegativeOutcome> outcomes = ResultNegatives.Run(S10SyntheticTriple.Inputs(heatingTimeSignal: false, heatingFullDay: true), "PASS");

            Assert.That(outcomes.Single(x => x.Case == ResultNegatives.ControlName).Verdict, Is.EqualTo("PASS"));
            Assert.That(outcomes.Where(x => !x.AsIntended).Select(x => x.Case + ": " + x.Verdict + " " + string.Join("; ", x.Evidence)), Is.Empty);
            NegativeOutcome shifted = outcomes.Single(x => x.Case == "dd-time-signal-shifted");
            Assert.That(shifted.Applicable, Is.True, "the cooling role still has a time signal: " + shifted.Applied);
            Assert.That(shifted.Applied, Does.StartWith("Cooling"));
        }
    }
}
