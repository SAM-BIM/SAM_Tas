// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.IO;
using NUnit.Framework;
using SAM.Analytical.Benchmark;

namespace SAM.Analytical.Tas.Benchmark.Tests
{
    /// <summary>
    /// The units correction (SAM_Tas#92, option A) does not move a published benchmark number: the
    /// model result SAM_Tas writes now (kWh/kW, marked) and the one it wrote before (Wh/W, unmarked,
    /// the committed golden's input) produce the same document, byte for byte. A model result whose
    /// units are not known is published as unavailable, never guessed.
    /// </summary>
    [TestFixture]
    public class ResultUnitsTests
    {
        [Test]
        public void MarkedModelResult_ProducesTheCommittedGolden_ByteForByte()
        {
            byte[] legacy = BenchmarkSerializer.SerializeToUtf8(Document(BenchmarkFixture.ModelResult()));
            byte[] marked = BenchmarkSerializer.SerializeToUtf8(Document(BenchmarkFixture.MarkedModelResult()));

            Assert.That(marked, Is.EqualTo(legacy));
            Assert.That(marked, Is.EqualTo(File.ReadAllBytes(GoldenFixturePath())));
        }

        [Test]
        public void MarkedModelResult_IsPublishedAsStored_NotDividedAgain()
        {
            BenchmarkModelResult model = Document(BenchmarkFixture.MarkedModelResult()).Model;

            Assert.That(model.ConsumptionHeating.Value, Is.EqualTo(1234.5));
            Assert.That(model.ConsumptionCooling.Value, Is.EqualTo(678.25));
            Assert.That(model.PeakHeatingLoad.Value, Is.EqualTo(3.6));
            Assert.That(model.PeakCoolingLoad.Value, Is.EqualTo(2.4));
            Assert.That(model.ConsumptionHeating.Unit, Is.EqualTo(MetricUnit.KilowattHour));
            Assert.That(model.PeakHeatingLoad.Unit, Is.EqualTo(MetricUnit.Kilowatt));
        }

        [TestCase("Tas")]
        [TestCase("SAM.Analytical.Tas.TPD")]
        [TestCase("")]
        public void UnmarkedModelResult_FromAnUnknownProducer_PublishesNoConsumptionOrPeak(string source)
        {
            BenchmarkModelResult model = Document(WithSource(BenchmarkFixture.ModelResult(), source)).Model;

            AssertUnavailable(model);

            // The values that do not depend on these units are still published.
            Assert.That(model.FloorArea.Available, Is.True);
            Assert.That(model.FloorArea.Value, Is.EqualTo(BenchmarkFixture.FloorArea));
        }

        [TestCase(2)]
        [TestCase(0)]
        public void TasModelResult_WithAnUnsupportedMarker_PublishesNoConsumptionOrPeak(int version)
        {
            AnalyticalModelSimulationResult modelResult = BenchmarkFixture.ModelResult();
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.ResultUnitsVersion, version);

            AssertUnavailable(Document(modelResult).Model);
        }

        private static BenchmarkDocument Document(AnalyticalModelSimulationResult modelResult)
        {
            TasBenchmarkContext context = BenchmarkFixture.GoldenContext();
            context.ModelResult = modelResult;
            return BenchmarkFixture.SingleSpaceModel().ToBenchmark(context);
        }

        private static AnalyticalModelSimulationResult WithSource(AnalyticalModelSimulationResult result, string source)
        {
            AnalyticalModelSimulationResult copy = new AnalyticalModelSimulationResult(result.Name, source, result.Reference);
            foreach (AnalyticalModelSimulationResultParameter parameter in new[]
            {
                AnalyticalModelSimulationResultParameter.ConsumptionHeating,
                AnalyticalModelSimulationResultParameter.ConsumptionCooling,
                AnalyticalModelSimulationResultParameter.PeakHeatingLoad,
                AnalyticalModelSimulationResultParameter.PeakCoolingLoad,
                AnalyticalModelSimulationResultParameter.FloorArea,
                AnalyticalModelSimulationResultParameter.Volume,
            })
            {
                copy.SetValue(parameter, result.GetValue<double>(parameter));
            }

            copy.SetValue(AnalyticalModelSimulationResultParameter.PeakHeatingHour, result.GetValue<int>(AnalyticalModelSimulationResultParameter.PeakHeatingHour));
            copy.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingHour, result.GetValue<int>(AnalyticalModelSimulationResultParameter.PeakCoolingHour));
            return copy;
        }

        private static void AssertUnavailable(BenchmarkModelResult model)
        {
            Assert.That(model.ConsumptionHeating.Available, Is.False);
            Assert.That(model.ConsumptionCooling.Available, Is.False);
            Assert.That(model.PeakHeatingLoad.Available, Is.False);
            Assert.That(model.PeakCoolingLoad.Available, Is.False);
            Assert.That(model.ConsumptionHeating.Value, Is.Null);
            Assert.That(model.PeakHeatingLoad.Value, Is.Null);
        }

        private static string GoldenFixturePath()
        {
            DirectoryInfo directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                string fixtures = Path.Combine(directory.FullName, "Fixtures");
                if (File.Exists(Path.Combine(fixtures, "BenchmarkFixture.cs")))
                {
                    return Path.Combine(fixtures, "golden-tas-benchmark.json");
                }

                directory = directory.Parent;
            }

            return Path.GetFullPath(Path.Combine("Fixtures", "golden-tas-benchmark.json"));
        }
    }
}
