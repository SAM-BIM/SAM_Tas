// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.InputEquivalence;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>The committed rule table, its reproduction from the committed calibration evidence, and the T-FRAME geometry.</summary>
    [TestFixture]
    public class CalibrationTests
    {
        [Test]
        public void CommittedRules_Parse_AndEveryConsumedRuleIsVerified()
        {
            CalibrationRules rules = CalibrationRules.Load(Evidence.Rules);
            Assert.That(rules.Schema, Is.EqualTo(CalibrationRules.CurrentSchema));
            Assert.That(rules.SourceSha256, Is.Not.Null.And.Length.EqualTo(64));
            foreach (string id in CalibrationRules.Consumed)
            {
                Assert.That(rules.IsVerified(id), Is.True, id);
                Assert.That(rules.Rules[id].Evidence, Is.Not.Empty, id);
            }

            Assert.That(rules.Answer(CalibrationRules.HostSurfaceArea), Is.EqualTo("net"));
            Assert.That(rules.Answer(CalibrationRules.ZeroFrame), Is.EqualTo("no-frame-surface"));
        }

        [Test]
        public void UnverifiedRule_IsNeverTakenAsTheWritersConvention()
        {
            CalibrationRules rules = CalibrationRules.None();
            Assert.That(rules.Offset(CalibrationRules.HourlySlot, 1, out bool verified), Is.EqualTo(1));
            Assert.That(verified, Is.False);
            Assert.That(rules.IsVerified(CalibrationRules.Alignment), Is.False);
            rules.Rules[CalibrationRules.Alignment] = new CalibrationRule { Status = "provisional", Answer = "aligned" };
            Assert.That(rules.IsVerified(CalibrationRules.Alignment), Is.False, "only 'verified' counts");
        }

        [Test]
        public void CalibrationDecisions_ReproduceFromTheCommittedEvidence()
        {
            List<CalibrationRun> runs = new List<CalibrationRun>
            {
                Calibration.Load("calib-box", Evidence.Unpack("calib-box")),
                Calibration.Load("calib-two", Evidence.Unpack("calib-two"))
            };

            string align = Evidence.Unpack("calib-align");
            TsdSnapshot tsd = SnapshotJson.Read<TsdSnapshot>(Path.Combine(align, "tsd.json"));
            List<CalibrationFinding> findings = Calibration.Analyse(runs, tsd);
            CalibrationRules committed = CalibrationRules.Load(Evidence.Rules);

            foreach (string id in CalibrationRules.Consumed)
            {
                CalibrationFinding finding = findings.Single(x => x.Id == id);
                Assert.That(finding.Consistent, Is.True, id + ": " + finding.Note);
                Assert.That(finding.Answer, Is.EqualTo(committed.Rules[id].Answer), id);
            }
        }

        [Test]
        public void Alignment_AShiftedPulse_IsNotAligned()
        {
            string align = Evidence.Unpack("calib-align");
            TsdSnapshot tsd = SnapshotJson.Read<TsdSnapshot>(Path.Combine(align, "tsd.json"));
            double[] lighting = tsd.Zones[0].Arrays["lightingGain"].Values;
            double[] shifted = new double[lighting.Length];
            for (int t = 0; t < lighting.Length; t++) shifted[(t + 1) % lighting.Length] = lighting[t];
            tsd.Zones[0].Arrays["lightingGain"].Values = shifted;

            CalibrationFinding finding = Calibration.Analyse(new List<CalibrationRun>(), tsd).Single(x => x.Id == CalibrationRules.Alignment);
            Assert.That(finding.Consistent, Is.False);
            Assert.That(finding.Evidence.Any(x => x.Contains("NOT aligned")));
        }

        [Test]
        public void TFrame_InwardOffset_OfARectangle_AndItsLimits()
        {
            Polygon3D window = new Polygon3D(new List<Point3D> { new Point3D(1.5, 0, 1), new Point3D(3.5, 0, 1), new Point3D(3.5, 0, 2), new Point3D(1.5, 0, 2) });
            Assert.That(ReadBackExpected.InwardOffsetArea(window, 0.052, out string problem), Is.EqualTo(1.896 * 0.896).Within(1e-12));
            Assert.That(problem, Is.Null);
            Assert.That(double.IsNaN(ReadBackExpected.InwardOffsetArea(window, 0.6, out _)), "an offset past half the height leaves no pane");

            Polygon3D l = new Polygon3D(new List<Point3D> { new Point3D(0, 0, 0), new Point3D(2, 0, 0), new Point3D(2, 0, 1), new Point3D(1, 0, 1), new Point3D(1, 0, 2), new Point3D(0, 0, 2) });
            ReadBackExpected.InwardOffsetArea(l, 0.05, out problem);
            Assert.That(problem, Does.Contain("not convex"));
        }

        [Test]
        public void TFrame_ExpectedSplit_OfTheCalibrationBox()
        {
            ExpectedModel expected = ReadBackExpected.Build(CalibrationModels.CreateBox(), CalibrationModels.CreateWeather(), "box");
            List<ExpectedAperture> apertures = expected.Spaces[0].Panels.SelectMany(x => x.Apertures).ToList();
            ExpectedAperture framed = apertures.Single(x => x.ConstructionName == "CAL_GLZ_FRAMED");
            ExpectedAperture unframed = apertures.Single(x => x.ConstructionName == "CAL_GLZ_UNFRAMED");

            Assert.That(framed.PaneArea.Value, Is.EqualTo(1.896 * 0.896).Within(1e-9));
            Assert.That(framed.FrameArea.Value, Is.EqualTo(2 - 1.896 * 0.896).Within(1e-9));
            Assert.That(framed.CrossCheckPaneArea, Is.EqualTo(framed.PaneArea.Value).Within(1e-9), "SAM's own GetFace3Ds agrees (cross-check only)");
            Assert.That(framed.FrameWidth.Value, Is.EqualTo(0.068).Within(1e-12), "the writer rule: frame thickness first");
            Assert.That(unframed.FrameArea.State, Is.EqualTo(SamInputState.Zero));
            Assert.That(unframed.FramePercentage.State, Is.EqualTo(SamInputState.Zero));
        }

        [Test]
        public void ExpectedModel_StatesFixtureCriteriaViolations()
        {
            AnalyticalModel model = CalibrationModels.CreateBox();
            Space space = model.AdjacencyCluster.GetSpaces()[0];
            InternalCondition ic = space.InternalCondition;
            ic.SetValue(InternalConditionParameter.ExhaustAirFlowPerPerson, 0.01);
            space.InternalCondition = ic;
            model.AdjacencyCluster.AddObject(space);

            ExpectedModel expected = ReadBackExpected.Build(new AnalyticalModel(model, model.AdjacencyCluster), null, "exhaust");
            Assert.That(expected.FixtureCriteria.Any(x => x.Contains("Exhaust")));
        }

        [Test]
        public void ElementsFile_InTheEvidence_HasEveryElementOfTheBox()
        {
            string path = Path.Combine(Evidence.Root, "runs", "calib-box", "compare", "series-elements.csv.gz");
            int rows = 0;
            using (StreamReader reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress)))
            {
                Assert.That(reader.ReadLine(), Does.StartWith("quantity,object,index"));
                while (reader.ReadLine() != null) rows++;
            }

            Assert.That(rows, Is.GreaterThan(8760 * 10));
        }
    }
}
