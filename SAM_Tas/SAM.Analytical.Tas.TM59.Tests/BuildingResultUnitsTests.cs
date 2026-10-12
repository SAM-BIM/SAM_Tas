// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TasConvert = SAM.Analytical.Tas.Convert;

namespace SAM.Analytical.Tas.TM59.Tests
{
    /// <summary>
    /// The Tas building result after the units correction (SAM_Tas#92, option A): the TSD's hourly building loads are W, so
    /// the converter stores their sum / 1000 in kWh and their maximum / 1000 in kW, once, marks the result, and keeps the
    /// producer name SAM reads legacy results by. The COM-free half of
    /// <see cref="TasConvert.ToSAM_AnalyticalModelSimulationResult(string, AnalyticalModel)"/>; no TSD, no licence.
    /// </summary>
    [TestFixture]
    public class BuildingResultUnitsTests
    {
        private const double FloorArea = 1_234.5;
        private const double Volume = 3_703.5;

        [Test]
        public void Converter_StoresKilowattHoursAndKilowatts_AndMarksTheResult()
        {
            List<double> heating = Profile(1);
            List<double> cooling = Profile(2);

            AnalyticalModelSimulationResult result = TasConvert.ToSAM_AnalyticalModelSimulationResult("Model", "model.tsd", heating, cooling, FloorArea, Volume);

            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating), Is.EqualTo(heating.Sum() / 1000));
            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling), Is.EqualTo(cooling.Sum() / 1000));
            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad), Is.EqualTo(heating.Max() / 1000));
            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad), Is.EqualTo(cooling.Max() / 1000));

            // The hour of the peak is unchanged by the units.
            Assert.That(result.GetValue<int>(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingHour), Is.EqualTo(heating.IndexOf(heating.Max())));
            Assert.That(result.GetValue<int>(Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingHour), Is.EqualTo(cooling.IndexOf(cooling.Max())));
            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.FloorArea), Is.EqualTo(FloorArea));
            Assert.That(Value(result, Analytical.AnalyticalModelSimulationResultParameter.Volume), Is.EqualTo(Volume));

            Assert.That(result.GetValue<int>(Analytical.AnalyticalModelSimulationResultParameter.ResultUnitsVersion), Is.EqualTo(AnalyticalModelSimulationResult.CurrentResultUnitsVersion));
            Assert.That(result.ResultUnitsState(), Is.EqualTo(ResultUnitsState.Marked));
        }

        [Test]
        public void ConverterSource_IsTheProducerNameSamReadsLegacyResultsBy()
        {
            AnalyticalModelSimulationResult result = TasConvert.ToSAM_AnalyticalModelSimulationResult("Model", "model.tsd", Profile(1), Profile(2), FloorArea, Volume);

            // A rename of this assembly would make every persisted (unmarked, Wh/W) Tas result an unknown producer.
            Assert.That(typeof(TasConvert).Assembly.GetName().Name, Is.EqualTo(AnalyticalModelSimulationResult.TasSource));
            Assert.That(result.Source, Is.EqualTo(AnalyticalModelSimulationResult.TasSource));
        }

        [Test]
        public void NewResult_ReadsBitIdenticalToTheLegacyResultFromTheSameProfiles()
        {
            List<double> heating = Profile(3);
            List<double> cooling = Profile(4);

            AnalyticalModelSimulationResult marked = TasConvert.ToSAM_AnalyticalModelSimulationResult("Model", "model.tsd", heating, cooling, FloorArea, Volume);

            // What the converter stored before the correction: the raw sums and maxima, no marker.
            AnalyticalModelSimulationResult legacy = new AnalyticalModelSimulationResult("Model", AnalyticalModelSimulationResult.TasSource, "model.tsd");
            legacy.SetValue(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating, heating.Sum());
            legacy.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad, heating.Max());
            legacy.SetValue(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling, cooling.Sum());
            legacy.SetValue(Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad, cooling.Max());

            foreach (Analytical.AnalyticalModelSimulationResultParameter parameter in new[]
            {
                Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating,
                Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling,
                Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad,
                Analytical.AnalyticalModelSimulationResultParameter.PeakCoolingLoad,
            })
            {
                Assert.That(marked.TryGetConsumptionOrPeakLoad(parameter, out double fromMarked), Is.True);
                Assert.That(legacy.TryGetConsumptionOrPeakLoad(parameter, out double fromLegacy), Is.True);
                Assert.That(BitConverter.DoubleToInt64Bits(fromMarked), Is.EqualTo(BitConverter.DoubleToInt64Bits(fromLegacy)), parameter.ToString());
            }
        }

        [Test]
        public void Converter_SAM_Tas92RecordedNumbers()
        {
            // SAM_Tas#92: Systems Demo heating 15 227 840.66370964 Wh, peak 38 727.40625 W; the SAM-generated Part O model
            // cooling 2 523 751.8729965687 Wh. A one-hour profile carries each total as its own peak.
            AnalyticalModelSimulationResult demo = TasConvert.ToSAM_AnalyticalModelSimulationResult("Demo", "demo.tsd", new[] { 0.0, 15_227_840.66370964 }, new[] { 2_523_751.8729965687 }, double.NaN, double.NaN);
            AnalyticalModelSimulationResult peak = TasConvert.ToSAM_AnalyticalModelSimulationResult("Demo", "demo.tsd", new[] { 38_727.40625 }, new[] { 0.0 }, double.NaN, double.NaN);

            Assert.That(demo.ConsumptionOrPeakLoad(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionHeating), Is.EqualTo(15_227.84066370964).Within(1e-9));
            Assert.That(demo.ConsumptionOrPeakLoad(Analytical.AnalyticalModelSimulationResultParameter.ConsumptionCooling), Is.EqualTo(2_523.7518729965687).Within(1e-9));
            Assert.That(peak.ConsumptionOrPeakLoad(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingLoad), Is.EqualTo(38.72740625).Within(1e-12));
            Assert.That(demo.GetValue<int>(Analytical.AnalyticalModelSimulationResultParameter.PeakHeatingHour), Is.EqualTo(1));

            // Floor area and volume Tas could not read are not stored.
            Assert.That(demo.HasValue(Analytical.AnalyticalModelSimulationResultParameter.FloorArea), Is.False);
            Assert.That(demo.HasValue(Analytical.AnalyticalModelSimulationResultParameter.Volume), Is.False);
        }

        // A deterministic year of hourly loads [W] with a single largest hour.
        private static List<double> Profile(int seed)
        {
            Random random = new Random(seed);
            List<double> result = Enumerable.Range(0, 8760).Select(_ => random.NextDouble() * 12_000).ToList();
            result[1000 + seed * 977] = 38_727.40625 + seed;
            return result;
        }

        private static double Value(AnalyticalModelSimulationResult result, Analytical.AnalyticalModelSimulationResultParameter parameter)
        {
            Assert.That(result.TryGetValue(parameter, out double value), Is.True, parameter.ToString());
            return value;
        }
    }
}
