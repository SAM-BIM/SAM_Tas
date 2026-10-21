// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EQ = SAM.Analytical.InputEquivalence.Query;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// Replays the real persisted-input snapshots of the licensed calibration (committed evidence) and mutates them. These
    /// are calibration models, not equivalence fixtures: a PASS here is the read-back reproducing its own calibration, never S08 acceptance.
    /// </summary>
    [TestFixture]
    public class RealSnapshotTests
    {
        [Test]
        public void CalibrationBox_UnderTheCommittedRules_Passes_WithCompleteCoverageFromTheModel()
        {
            string run = Evidence.Unpack("calib-box");
            ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run));

            Assert.That(comparison.Required, Is.Not.Empty);
            Assert.That(comparison.Gate.Coverage, Is.EqualTo(CoverageStatus.Complete));
            Assert.That(comparison.Gate.Unrequested, Is.Empty);
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Pass), comparison.Gate.ToString());
            Assert.That(comparison.Identity.Zones.All(x => x.Status == IdentityStatus.Verified));
        }

        [Test]
        public void CalibrationBox_WithNoRules_IsNeverPass_AndTheAgreementShowsAsNotRun()
        {
            string run = Evidence.Unpack("calib-box");
            ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run), rules: CalibrationRules.None());

            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.NotRun));
            Assert.That(comparison.Results.Any(x => x.Status == GateStatus.Fail), Is.False, "the values agree; only the unverified conventions hold them back");
            Assert.That(comparison.Results.Where(x => x.Definition.Id == "6.hourly").All(x => x.Status == GateStatus.NotRun));
        }

        [Test]
        public void AnUnresolvedConvention_HoldsBackExactlyTheQuantitiesThatDependOnIt()
        {
            string run = Evidence.Unpack("calib-box");
            CalibrationRules rules = CalibrationRules.Load(Evidence.Rules);
            rules.Rules.Remove(CalibrationRules.HostSurfaceArea);
            rules.Rules.Remove(CalibrationRules.HourlySlot);
            ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run), rules: rules);

            // Alignment stays verified, so only the convention can hold these back.
            Assert.That(Evidence.Result(comparison, "6.hourly", ".ticLG.hourly").Status, Is.EqualTo(GateStatus.NotRun), "a 24-hour profile depends on N-5.hourlySlot");
            Assert.That(Evidence.Result(comparison, "6.hourly", ".ticESG.hourly").Status, Is.EqualTo(GateStatus.Pass), "a yearly profile does not");
            Assert.That(comparison.Results.Where(x => x.Definition.Id == "1b" && x.Input.ObjectKey.StartsWith("panel:")).Count(x => x.Status == GateStatus.NotRun), Is.EqualTo(2), "the two hosts with openings depend on N-1");
            Assert.That(comparison.Results.Any(x => x.Status == GateStatus.Fail), Is.False);
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.NotRun));
        }

        [Test]
        public void CalibrationTwoZones_NoDeliveryInV0AndV1_IsTheProposedG1Case_BlockedUnderBothRuleSets()
        {
            string run = Evidence.Unpack("calib-two");
            foreach (GateRuleSet ruleSet in new[] { GateRuleSet.Interim, GateRuleSet.ProvisionalProposed })
            {
                ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run), ruleSet: ruleSet);
                List<QuantityResult> delivery = comparison.Results.Where(x => x.Definition.Id == "8b").ToList();

                Assert.That(delivery, Has.Count.EqualTo(2));
                Assert.That(delivery.All(x => x.Status == GateStatus.BlockedOwnerDecision && x.Proposed != null && x.Proposed.Contains("OD-G1")));
                Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
                Assert.That(comparison.Gate.Provisional, Is.True);
            }
        }

        [Test]
        public void ReadBackCompare_EndToEnd_ExitCodeIsZeroOnlyForPass()
        {
            string box = Evidence.Unpack("calib-box"), two = Evidence.Unpack("calib-two");
            ReadBackReport boxReport = ReadBackCompare.Run(box, Evidence.Rules, "calib-box", "test", GateRuleSet.Interim, Path.Combine(box, "out"));
            ReadBackReport twoReport = ReadBackCompare.Run(two, Evidence.Rules, "calib-two", "test", GateRuleSet.Interim, Path.Combine(two, "out"));

            Assert.That(boxReport.ExitCode, Is.EqualTo(ReadBackExit.Pass));
            Assert.That(twoReport.ExitCode, Is.EqualTo(ReadBackExit.BlockedOwner));
            Assert.That(File.Exists(Path.Combine(box, "out", ReadBackCompare.ElementsFile)));
            Assert.That(boxReport.Inputs["exportProcess"], Is.Not.EqualTo(boxReport.Inputs["readerProcess"]), "the reader ran in a fresh process");
        }

        // ---- the reader states, kept distinct -----------------------------------------------------------------------------

        private static ReadBackComparison WithFloorArea(NativeField field, GateRuleSet ruleSet = GateRuleSet.Interim)
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Zones[0].FloorArea = field;
            return Evidence.Compare(run, snapshot, ruleSet: ruleSet);
        }

        [Test]
        public void ReaderState_Absent_IsAnExportDefect()
        {
            QuantityResult result = Evidence.Result(WithFloorArea(NativeField.Absent("removed")), "1a", "c001");
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void ReaderState_Unreadable_IsATechnicalBlocker_NeverANumber()
        {
            QuantityResult result = Evidence.Result(WithFloorArea(NativeField.Unreadable("COM failure")), "1a", "c001");
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnverifiedUnreadable));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(result.TechnicalBlockerNamed, Does.Contain("COM failure"));
        }

        [Test]
        public void ReaderState_NaN_IsTheProposedG3_BlockedInterim_FailProvisional()
        {
            QuantityResult interim = Evidence.Result(WithFloorArea(NativeField.Of(double.NaN)), "1a", "c001");
            QuantityResult provisional = Evidence.Result(WithFloorArea(NativeField.Of(double.NaN), GateRuleSet.ProvisionalProposed), "1a", "c001");
            Assert.That(interim.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(provisional.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(interim.Proposed, Does.Contain("G3"));
            Assert.That(provisional.Proposed, Does.Contain("G3"));
        }

        [Test]
        public void ReaderState_Zero_ForAStatedValue_IsAZeroedExportDefect()
        {
            QuantityResult result = Evidence.Result(WithFloorArea(NativeField.Of(0)), "1a", "c001");
            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.ExportDefect));
            Assert.That(result.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void UnreadableProfile_BlocksTheGainAndTheSchedule_Technically()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeInternalCondition ic = snapshot.Zones[0].InternalConditions.First(x => !x.DayTypes.Contains("HDD"));
            ic.GainProfiles["ticLG"] = new NativeProfile { Slot = "ticLG", Kind = FieldKind.Unreadable, Detail = "RPC_E_SERVERFAULT" };
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(Evidence.Result(comparison, "4a.density", ".ticLG.factor").Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(Evidence.Result(comparison, "6.hourly", ".ticLG.hourly").Status, Is.EqualTo(GateStatus.BlockedTechnical));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.BlockedTechnical));
        }

        // ---- coverage ----------------------------------------------------------------------------------------------------

        [Test]
        public void Coverage_ComesFromTheFrozenModel_SoMissingNativeObjectsCannotShrinkIt()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            int required = Evidence.Compare(run, snapshot).Required.Count;
            snapshot.Zones[0].Surfaces.Clear();
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Required.Count, Is.EqualTo(required));
            Assert.That(comparison.Gate.Coverage, Is.EqualTo(CoverageStatus.Complete), "every required comparison still has a (failing) result");
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Coverage_AMissingResult_IsIncomplete_AndNeverPass()
        {
            string run = Evidence.Unpack("calib-box");
            ReadBackComparison comparison = Evidence.Compare(run, Evidence.Snapshot(run));
            List<QuantityResult> partial = comparison.Results.Where(x => x.Definition.Id != "1a").ToList();
            GateReport gate = EQ.Gate(partial, comparison.Configuration);

            Assert.That(gate.Coverage, Is.EqualTo(CoverageStatus.Incomplete));
            Assert.That(gate.MissingComparisons.Select(x => x.QuantityId), Does.Contain("1a"));
            Assert.That(gate.Status, Is.EqualTo(GateStatus.NotRun));
        }

        // ---- identity: geometry decides, the writer's carrier can only contradict -----------------------------------------

        [Test]
        public void Identity_SwappedSpaceGuidCarriers_Fail()
        {
            string run = Evidence.Unpack("calib-two");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            string a = snapshot.Zones[0].Description, b = snapshot.Zones[1].Description;
            snapshot.Zones[0].Description = b;
            snapshot.Zones[1].Description = a;
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Identity.Zones.All(x => x.Status == IdentityStatus.Mismatch && x.CarrierAgrees == false));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Identity_WithoutTheCarrier_GeometryStillVerifies()
        {
            string run = Evidence.Unpack("calib-two");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            foreach (NativeZone zone in snapshot.Zones)
            {
                zone.Description = string.Join(";", zone.Description.Split(';').Where(x => !x.Trim().StartsWith(ReadBackIdentity.SpaceGuidMarker)));
            }

            ReadBackComparison comparison = Evidence.Compare(run, snapshot);
            Assert.That(comparison.Identity.Zones.All(x => x.Status == IdentityStatus.Verified && x.CarrierAgrees == null));
        }

        [Test]
        public void Identity_SwappedBoundaries_PairTheOtherSurface_AndItsAreaFails()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            List<NativeSurface> walls = snapshot.Zones[0].Surfaces.Where(x => x.BEType == 2 && x.Area.HasNumber).ToList();
            NativeSurface west = walls.First(x => x.Area.Value == 12), east = walls.First(x => x.Area.Value == 11);
            List<NativeRoomSurface> rooms = west.RoomSurfaces;
            west.RoomSurfaces = east.RoomSurfaces;
            east.RoomSurfaces = rooms;
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Results.Count(x => x.Definition.Id == "1b" && x.Status == GateStatus.Fail), Is.GreaterThanOrEqualTo(2));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Identity_ANativeSurfaceSamNeverStated_Fails()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeSurface extra = SnapshotJson.Read<NativeSnapshot>(Path.Combine(run, ReadBackFiles.Snapshot)).Zones[0].Surfaces[0];
            foreach (double[] point in extra.RoomSurfaces[0].Outer) point[2] += 10;
            snapshot.Zones[0].Surfaces.Add(extra);
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Results.Any(x => x.Input.ObjectKey.StartsWith("native:zone[0].surface[") && x.Outcome == EquivalenceOutcome.ExportDefect));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Identity_ADuplicatedBoundary_IsAmbiguous_AndFails()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            snapshot.Zones[0].Surfaces.Add(SnapshotJson.Read<NativeSnapshot>(Path.Combine(run, ReadBackFiles.Snapshot)).Zones[0].Surfaces[0]);
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Identity.Zones[0].Status, Is.EqualTo(IdentityStatus.Mismatch));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        // ---- layers: order per zone side --------------------------------------------------------------------------------

        [Test]
        public void Layers_SwappedInThePersistedConstruction_Fail()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeBuildingElement wall = snapshot.BuildingElements.First(x => x.BEType == 2);
            NativeLayer first = wall.Layers[0];
            wall.Layers[0] = wall.Layers[2];
            wall.Layers[2] = first;
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Results.Any(x => x.Definition.Id == "3a" && x.Input.ObjectKey.EndsWith("#layer1.thickness") && x.Status == GateStatus.Fail));
            Assert.That(comparison.Gate.Status, Is.EqualTo(GateStatus.Fail));
        }

        [Test]
        public void Layers_AFlippedReversedFlag_ReversesTheZoneSide_AndFails()
        {
            string run = Evidence.Unpack("calib-box");
            NativeSnapshot snapshot = Evidence.Snapshot(run);
            NativeSurface wall = snapshot.Zones[0].Surfaces.First(x => x.BEType == 2);
            wall.Reversed = NativeField.Of(-1);
            ReadBackComparison comparison = Evidence.Compare(run, snapshot);

            Assert.That(comparison.Results.Count(x => x.Definition.Id == "3a" && x.Status == GateStatus.Fail), Is.GreaterThan(0));
        }

        [Test]
        public void LeapYearProfile_IsAnUnsupportedMapping_NeverTruncated()
        {
            string run = Evidence.Unpack("calib-box");
            ExpectedModel expected = Evidence.Expected(run, "leap");
            ExpectedProfile lighting = expected.Spaces[0].Profiles["ticLG"];
            lighting.RawValues = Enumerable.Repeat(0.5, 8784).ToArray();
            lighting.Expanded = EQ.ExpandSchedule(lighting.RawValues);
            ReadBackComparison comparison = ReadBackComparison.Run(expected, Evidence.Snapshot(run), CalibrationRules.Load(Evidence.Rules));
            QuantityResult result = Evidence.Result(comparison, "6.hourly", ".ticLG.hourly");

            Assert.That(result.Outcome, Is.EqualTo(EquivalenceOutcome.UnsupportedMapping));
            Assert.That(result.Status, Is.EqualTo(GateStatus.BlockedOwnerDecision));
            Assert.That(comparison.Series.Any(x => x.ObjectKey.EndsWith(".ticLG.hourly")), Is.False, "an 8784-hour schedule is never cut to 8760 and compared");
        }
    }
}
