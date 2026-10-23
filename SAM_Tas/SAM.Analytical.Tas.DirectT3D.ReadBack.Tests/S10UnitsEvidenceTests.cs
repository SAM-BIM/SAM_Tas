// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Analytical.Tas.DirectT3D.Validation.ReadBack;
using System.IO;
using System.Linq;

namespace SAM.Analytical.Tas.DirectT3D.ReadBack.Tests
{
    /// <summary>
    /// S10 units and interval evidence, independent of the code under test: the committed S08 P-ALIGN TSD read
    /// (Documentation/S08-PRB-ReadBack-evidence/runs/calib-align/tsd.json, licensed Tas 2.0.0.1) against the authored P-ALIGN
    /// model. An authored gain of 10 W/m2 over the 20 m2 zone reads back as 200 (the TSD gain arrays are W), and the
    /// one-hour sensible-equipment pulse occupies exactly one element whose value is its whole annual sum (one element per
    /// hour, so an element's W is also its Wh). The per-run balance check (S10-C5) carries the W to the load arrays.
    /// </summary>
    [TestFixture]
    public class S10UnitsEvidenceTests
    {
        private static TsdSnapshot Align()
        {
            return SnapshotJson.Read<TsdSnapshot>(Path.Combine(Evidence.Unpack("calib-align"), "tsd.json"));
        }

        private static double Authored(InternalConditionParameter parameter)
        {
            AnalyticalModel model = CalibrationModels.CreateAlign();
            Space space = model.AdjacencyCluster.GetSpaces().Single();
            space.TryGetValue(SpaceParameter.Area, out double area);
            space.InternalCondition.TryGetValue(parameter, out double perArea);
            return perArea * area;
        }

        [Test]
        public void GainArrays_AreWatts_FromAnAuthoredGain()
        {
            TsdSnapshot tsd = Align();
            double[] lighting = tsd.Zones.Single().Arrays["lightingGain"].Values;
            double authored = Authored(InternalConditionParameter.LightingGainPerArea);

            Assert.That(authored, Is.EqualTo(200));
            Assert.That(lighting.Max(), Is.EqualTo(authored), "10 W/m2 x 20 m2 reads back as 200: W");
            Assert.That(lighting.Count(x => x != 0), Is.EqualTo(365), "one lit hour per day");
        }

        [Test]
        public void OneElementIsOneHour_FromTheEquipmentPulse()
        {
            TsdSnapshot tsd = Align();
            NativeSeries equipment = tsd.Zones.Single().Arrays["equipmentSensibleGain"];
            double authored = Authored(InternalConditionParameter.EquipmentSensibleGainPerArea);

            Assert.That(equipment.LowerBound, Is.EqualTo(1));
            Assert.That(equipment.Values, Has.Length.EqualTo(8760), "24 elements per simulated day for days 1..365");
            Assert.That(tsd.FirstDay, Is.EqualTo(1));
            Assert.That(tsd.LastDay, Is.EqualTo(365));
            Assert.That(equipment.Values.Count(x => x != 0), Is.EqualTo(1));
            Assert.That(equipment.Values.Sum(), Is.EqualTo(authored), "the one-hour pulse is one element holding its whole sum");
            Assert.That(System.Array.FindIndex(equipment.Values, x => x != 0), Is.EqualTo(CalibrationModels.PulseEquipmentHourOfYear), "position = SAM hour (CLR index = hour + 1)");
        }

        [Test]
        public void LoadArrays_AreNonNegative_AndCoolingIsUnusedHere()
        {
            TsdSnapshot tsd = Align();

            Assert.That(tsd.Zones.Single().Arrays["heatingLoad"].Values.Min(), Is.GreaterThanOrEqualTo(0));
            Assert.That(tsd.Zones.Single().Arrays["coolingLoad"].Values.All(x => x == 0), Is.True, "the P-ALIGN cooling setpoint (60 C) is never reached");
        }
    }
}
