// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SAM.Analytical.Benchmark;

namespace SAM.Analytical.Tas.Benchmark.Tests
{
    /// <summary>
    /// S10 peak semantics of the TAS producer (F4-F6 of SAM_UI#239). A space's B1a <c>peakLoad</c>/<c>peakHour</c> are its
    /// ANNUAL peak (<see cref="SpaceSimulationResultParameter.AnnualPeak"/>), never the legacy governing projection
    /// (<c>Load</c>/<c>LoadIndex</c>); a peak of 0 is an available zero with no hour, for spaces and the model. Each legacy-only
    /// case below is a replay of a defect: the legacy fields hold what the pre-S10 producer emitted.
    /// </summary>
    [TestFixture]
    public class PeakSemanticsTests
    {
        private static SpaceSimulationResult Result(LoadType loadType, double legacyLoad, int legacyIndex, SizingMethod sizingMethod, SpaceLoadPeak annualPeak, SpaceLoadPeak designDayPeak = null)
        {
            SpaceSimulationResult result = new SpaceSimulationResult(BenchmarkFixture.SpaceName, BenchmarkFixture.TasSource, BenchmarkFixture.TasZoneReference);
            result.SetValue(SpaceSimulationResultParameter.LoadType, loadType.Text());
            result.SetValue(SpaceSimulationResultParameter.Load, legacyLoad);
            result.SetValue(SpaceSimulationResultParameter.LoadIndex, legacyIndex);
            result.SetValue(SpaceSimulationResultParameter.SizingMethod, sizingMethod.ToString());
            result.SetValue(SpaceSimulationResultParameter.DesignLoad, 5000.0);
            result.SetValue(SpaceSimulationResultParameter.UnmetHours, 0.0);
            if (annualPeak != null)
            {
                result.SetValue(SpaceSimulationResultParameter.AnnualPeak, annualPeak);
            }

            if (designDayPeak != null)
            {
                result.SetValue(SpaceSimulationResultParameter.DesignDayPeak, designDayPeak);
            }

            return result;
        }

        private static BenchmarkConditionResult Heating(params SpaceSimulationResult[] results)
        {
            return Document(results).Spaces.Single().Heating;
        }

        private static BenchmarkDocument Document(IEnumerable<SpaceSimulationResult> results, AnalyticalModelSimulationResult modelResult = null)
        {
            TasBenchmarkContext context = BenchmarkFixture.GoldenContext();
            if (modelResult != null)
            {
                context.ModelResult = modelResult;
            }

            return BenchmarkFixture.BuildModel(results).ToBenchmark(context);
        }

        [Test]
        public void AnnualGoverned_PeakHourIsTheAnnualHourOfYear_NotTheRawIndex()
        {
            // F4 replay: the legacy LoadIndex is Tas's 1-based index (hour + 1).
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, BenchmarkFixture.AnnualPeak(3600, 205)));

            AssertValue(heating.PeakLoad, 3600, MetricUnit.Watt);
            AssertValue(heating.PeakHour, 205, MetricUnit.HourOfYear);
        }

        [Test]
        public void AnnualPeakInTheLastHour_IsAvailableAtHour8759()
        {
            // F4 replay: the raw index 8760 failed the 0..8759 check and lost both values.
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 4100, 8760, SizingMethod.Simulation, BenchmarkFixture.AnnualPeak(4100, 8759)));

            AssertValue(heating.PeakLoad, 4100, MetricUnit.Watt);
            AssertValue(heating.PeakHour, 8759, MetricUnit.HourOfYear);
        }

        [Test]
        public void DesignDayGoverned_EmitsTheAnnualLoadAndHour_NeverTheDesignDay()
        {
            // F5 replay: the governing design day (6200 W at design-day index 20) was emitted as the peak and its series
            // index as an "hour of year".
            SpaceSimulationResult result = Result(LoadType.Heating, 6200, 20, SizingMethod.HDD, BenchmarkFixture.AnnualPeak(1500, 4000), BenchmarkFixture.DesignDayPeak(6200, 19, "HDD 1"));

            BenchmarkConditionResult heating = Heating(result);

            AssertValue(heating.PeakLoad, 1500, MetricUnit.Watt);
            AssertValue(heating.PeakHour, 4000, MetricUnit.HourOfYear);
            AssertValue(heating.DesignLoad, 5000, MetricUnit.Watt);
        }

        [Test]
        public void ZeroAnnualPeak_IsAnAvailableZero_WithNoHour()
        {
            // F6 replay (space): the legacy projection of a zero peak stores the -1 sentinel at index 0, which emitted
            // both values unavailable and lost the measured zero.
            SpaceLoadPeak zero = new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, 0);
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, -1, 0, SizingMethod.HDD, zero, new SpaceLoadPeak(LoadPeakBasis.DesignDay, 0) { DesignDayName = "HDD 1" }));

            AssertValue(heating.PeakLoad, 0, MetricUnit.Watt);
            Assert.That(heating.PeakHour.Available, Is.False, "a zero peak has no hour");
            Assert.That(heating.PeakHour.Value, Is.Null);
        }

        [Test]
        public void ZeroAnnualPeak_WithAStoredHour_StillHasNoHour()
        {
            SpaceLoadPeak zero = new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, 0) { HourOfYear = 0, HourOfDay = 0 };
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 0, 1, SizingMethod.Simulation, zero));

            AssertValue(heating.PeakLoad, 0, MetricUnit.Watt);
            Assert.That(heating.PeakHour.Available, Is.False);
        }

        [Test]
        public void NoAnnualPeak_IsUnavailable_WithNoLegacyFallback()
        {
            // A result written before AnnualPeak existed has only the legacy fields: never read them.
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, null));

            Assert.That(heating.PeakLoad.Available, Is.False);
            Assert.That(heating.PeakHour.Available, Is.False);
            AssertValue(heating.DesignLoad, 5000, MetricUnit.Watt);
        }

        [Test]
        public void ADesignDayPeakStoredAsAnnualPeak_IsUnavailable()
        {
            SpaceLoadPeak wrongBasis = new SpaceLoadPeak(LoadPeakBasis.DesignDay, 3600) { HourOfYear = 205, HourOfDay = 13 };
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, wrongBasis));

            Assert.That(heating.PeakLoad.Available, Is.False);
            Assert.That(heating.PeakHour.Available, Is.False);
        }

        [TestCase(null)]
        [TestCase(8760)]
        [TestCase(-1)]
        public void PositiveAnnualPeak_WithoutAValidHour_IsUnavailable(int? hourOfYear)
        {
            SpaceLoadPeak peak = new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, 3600) { HourOfYear = hourOfYear };
            BenchmarkConditionResult heating = Heating(Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, peak));

            Assert.That(heating.PeakLoad.Available, Is.False, "a load without a valid hour is missing, never a measured value");
            Assert.That(heating.PeakHour.Available, Is.False);
        }

        [Test]
        public void HeatingAndCooling_ReadTheirOwnAnnualPeaks()
        {
            BenchmarkDocument document = Document(new[]
            {
                Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, BenchmarkFixture.AnnualPeak(3600, 205)),
                Result(LoadType.Cooling, 2400, 4603, SizingMethod.Simulation, BenchmarkFixture.AnnualPeak(2400, 4602)),
            });

            BenchmarkSpaceResult space = document.Spaces.Single();
            AssertValue(space.Heating.PeakHour, 205, MetricUnit.HourOfYear);
            AssertValue(space.Cooling.PeakLoad, 2400, MetricUnit.Watt);
            AssertValue(space.Cooling.PeakHour, 4602, MetricUnit.HourOfYear);
        }

        [Test]
        public void AllZeroModelProfile_IsAnAvailableZero_WithNoHour()
        {
            // F6 replay (model): SAM stores hour 0 (IndexOf the maximum 0) for an all-zero profile; it is not an hour.
            AnalyticalModelSimulationResult modelResult = BenchmarkFixture.MarkedModelResult();
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.ConsumptionCooling, 0.0);
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingLoad, 0.0);
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingHour, 0);

            BenchmarkModelResult model = Document(BenchmarkFixture.SpaceResults(), modelResult).Model;

            AssertValue(model.ConsumptionCooling, 0, MetricUnit.KilowattHour);
            AssertValue(model.PeakCoolingLoad, 0, MetricUnit.Kilowatt);
            Assert.That(model.PeakCoolingHour.Available, Is.False, "an all-zero profile has no peak hour");
            AssertValue(model.PeakHeatingHour, BenchmarkFixture.PeakHeatingHour, MetricUnit.HourOfYear);
        }

        [Test]
        public void ZeroPeakDocuments_ValidateWithoutErrors()
        {
            AnalyticalModelSimulationResult modelResult = BenchmarkFixture.MarkedModelResult();
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingLoad, 0.0);
            modelResult.SetValue(AnalyticalModelSimulationResultParameter.PeakCoolingHour, 0);
            BenchmarkDocument document = Document(new[]
            {
                Result(LoadType.Heating, 3600, 206, SizingMethod.Simulation, BenchmarkFixture.AnnualPeak(3600, 205)),
                Result(LoadType.Cooling, -1, 0, SizingMethod.CDD, new SpaceLoadPeak(LoadPeakBasis.AnnualSimulation, 0)),
            }, modelResult);

            BenchmarkValidationResult validation = BenchmarkValidator.Validate(document);

            Assert.That(validation.Errors, Is.Empty, "Errors: " + string.Join(" | ", validation.Errors.Select(x => x.ToString())));
            TestContext.Out.WriteLine("Warnings: " + string.Join(" | ", validation.Warnings.Select(x => x.ToString())));
        }

        [Test]
        public void AnnualPeak_SurvivesTheSamJsonRoundTrip_TheSubjectReads()
        {
            // The S10 subject reads the workflow's saved <tbd>.json: the peak must come back as a SpaceLoadPeak.
            AnalyticalModel model = BenchmarkFixture.SingleSpaceModel();
            string json = Core.Convert.ToString(model);
            AnalyticalModel reloaded = Core.Convert.ToSAM<AnalyticalModel>(json).Single();

            BenchmarkSpaceResult before = model.ToBenchmark(BenchmarkFixture.GoldenContext()).Spaces.Single();
            BenchmarkSpaceResult after = reloaded.ToBenchmark(BenchmarkFixture.GoldenContext()).Spaces.Single();

            AssertValue(after.Heating.PeakLoad, before.Heating.PeakLoad.Value.Value, MetricUnit.Watt);
            AssertValue(after.Heating.PeakHour, before.Heating.PeakHour.Value.Value, MetricUnit.HourOfYear);
            AssertValue(after.Cooling.PeakHour, before.Cooling.PeakHour.Value.Value, MetricUnit.HourOfYear);
        }

        private static void AssertValue(MetricValue metric, double expected, MetricUnit unit)
        {
            Assert.That(metric, Is.Not.Null);
            Assert.That(metric.Available, Is.True, "expected available");
            Assert.That(metric.Unit, Is.EqualTo(unit));
            Assert.That(metric.Value, Is.EqualTo(expected), "exact");
        }
    }
}
