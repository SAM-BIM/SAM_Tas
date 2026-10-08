// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using NUnit.Framework;
using SAM.Math;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas.GenOpt.Tests
{
    /// <summary>
    /// Mapping of the existing SAM_Tas GenOpt objects (NumberParameter, algorithm objects, OptimizationSettings,
    /// objectives) to the generic SAM.Math kernel.
    /// </summary>
    [TestFixture]
    public class MappingTests
    {
        /// <summary>An algorithm object for any AlgorithmType value, including the ones with no SAM_Tas class.</summary>
        private sealed class AnyAlgorithm : Algorithm
        {
            private readonly AlgorithmType algorithmType;

            public AnyAlgorithm(AlgorithmType algorithmType)
            {
                this.algorithmType = algorithmType;
            }

            public override AlgorithmType AlgorithmType => algorithmType;
        }

        // PR6: GPSCoordinateSearch is no longer listed as native; it is refused as unsupported like any other algorithm (D3).
        private static readonly AlgorithmType[] PhaseOne = { AlgorithmType.GPSHookeJeeves, AlgorithmType.GoldenSection };

        public static IEnumerable<AlgorithmType> UnsupportedTypes() => Enum.GetValues(typeof(AlgorithmType)).Cast<AlgorithmType>().Where(t => !PhaseOne.Contains(t));

        /// <summary>Every concrete algorithm class in SAM.Analytical.Tas.GenOpt except the Phase-1 ones.</summary>
        public static IEnumerable<Type> UnsupportedClasses() => typeof(Algorithm).Assembly.GetTypes()
            .Where(t => typeof(Algorithm).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t != typeof(GPSHookeJeevesAlgorithm) && t != typeof(GoldenSectionAlgorithm))
            .OrderBy(t => t.Name);

        private static GPSHookeJeevesAlgorithm HookeJeeves(double divider = 2, double s0 = 0, double t = 1, double m = 4) => new GPSHookeJeevesAlgorithm
        {
            MeshSizeDivider = divider,
            InitialMeshSizeExponent = s0,
            MeshSizeExponentIncrement = t,
            NumberOfStepReduction = m,
        };

        // ------------------------------------------------------------------ supported algorithms

        [Test]
        public void HookeJeeves_MapsMeshAndMaxIte()
        {
            Optimiser optimiser = HookeJeeves(3, 1, 2, 1).ToSAM_Optimiser(new OptimizationSettings { MaxIterations = 77 }, 2);

            HookeJeeves result = optimiser as HookeJeeves;
            Assert.That(result, Is.Not.Null);
            Assert.That(result.MeshSizeDivider, Is.EqualTo(3));
            Assert.That(result.InitialMeshSizeExponent, Is.EqualTo(1));
            Assert.That(result.MeshSizeExponentIncrement, Is.EqualTo(2));
            Assert.That(result.NumberOfStepReduction, Is.EqualTo(1));
            Assert.That(result.MaximumSimulations, Is.EqualTo(77));
        }

        [Test]
        public void HookeJeeves_TasDefaults_Map()
        {
            HookeJeeves result = (HookeJeeves)new GPSHookeJeevesAlgorithm().ToSAM_Optimiser(new OptimizationSettings(), 1);

            Assert.That(new[] { result.MeshSizeDivider, result.InitialMeshSizeExponent, result.MeshSizeExponentIncrement, result.NumberOfStepReduction }, Is.EqualTo(new[] { 2, 0, 1, 4 }));
            Assert.That(result.MaximumSimulations, Is.EqualTo(2000));
        }

        [Test]
        public void GoldenSection_MapsAbsDiffFunction()
        {
            GoldenSection result = (GoldenSection)new GoldenSectionAlgorithm { AbsDiffFunction = 0.25 }.ToSAM_Optimiser(new OptimizationSettings { MaxIterations = 12 }, 1);

            Assert.That(result.StoppingCriterion, Is.EqualTo(GoldenSectionStoppingCriterion.AbsoluteDifference));
            Assert.That(result.AbsoluteDifference, Is.EqualTo(0.25));
            Assert.That(result.MaximumSimulations, Is.EqualTo(12));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(3)]
        public void GoldenSection_RequiresExactlyOneParameter(int parameterCount)
        {
            GenOptCompatibilityException exception = Assert.Throws<GenOptCompatibilityException>(() => new GoldenSectionAlgorithm().ToSAM_Optimiser(new OptimizationSettings(), parameterCount));

            Assert.That(exception.Message, Does.Contain("exactly one"));
        }

        [Test]
        public void GoldenSection_ExponentAbsDiffFunction_IsRefused()
        {
            Assert.Throws<GenOptCompatibilityException>(() => new GoldenSectionAlgorithm { AbsDiffFunction = 1e-5 }.ToSAM_Optimiser(new OptimizationSettings(), 1));
        }

        /// <summary>
        /// Owner decision D3, PR3 Java-only oracle: with the existing writer, GenOpt 3.1.1 rejects every
        /// GPSCoordinateSearchAlgorithm ("Unknown or unexpected keyword 'Seed'" / 'NumberOfInitialPoint'). That held
        /// for the Tas defaults, for Seed = 0 / NumberOfInitialPoint = 0 with a valid mesh, and for Seed = 7 /
        /// NumberOfInitialPoint = 5. PR6 (Java route retired): it is refused as unsupported, with that reason, whatever
        /// its values, and never substituted.
        /// </summary>
        [TestCase(0, 0, 0)]
        [TestCase(0, 0, 2)]
        [TestCase(7, 5, 2)]
        public void CoordinateSearch_IsRefusedAsUnsupportedWithTheReason(double seed, int numberOfInitialPoint, double divider)
        {
            GPSCoordinateSearchAlgorithm algorithm = new GPSCoordinateSearchAlgorithm
            {
                Seed = seed,
                NumberOfInitialPoint = numberOfInitialPoint,
                MeshSizeDivider = divider,
                InitialMeshSizeExponent = 0,
                MeshSizeExponentIncrement = 1,
                NumberOfStepReduction = 4,
            };

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => algorithm.ToSAM_Optimiser(new OptimizationSettings(), 2));

            Assert.That(exception.Message, Does.Contain("'GPSCoordinateSearch' (GPSCoordinateSearchAlgorithm) is not supported"));
            Assert.That(exception.Message, Does.Contain("Seed").And.Contain("NumberOfInitialPoint").And.Contain("MultiStart"));
            Assert.That(exception.Message, Does.Contain("Supported: GPSHookeJeeves, GoldenSection."));
        }

        // ------------------------------------------------------------------ unsupported algorithms

        [TestCaseSource(nameof(UnsupportedTypes))]
        public void EveryNonPhaseOneAlgorithmType_ThrowsNotSupported(AlgorithmType algorithmType)
        {
            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => new AnyAlgorithm(algorithmType).ToSAM_Optimiser(new OptimizationSettings(), 1));

            Assert.That(exception.Message, Does.Contain(algorithmType.ToString()));
            Assert.That(exception.Message, Does.Contain("Supported: GPSHookeJeeves, GoldenSection."));
        }

        [Test]
        public void UnsupportedTypes_AreEveryEnumValueExceptPhaseOne()
        {
            int all = Enum.GetValues(typeof(AlgorithmType)).Length;

            Assert.That(UnsupportedTypes().Count(), Is.EqualTo(all - 2));
            Assert.That(UnsupportedTypes(), Does.Contain(AlgorithmType.GPSCoordinateSearch));
            Assert.That(Convert.NativeAlgorithmTypes, Is.EquivalentTo(PhaseOne));
        }

        /// <summary>Includes MultiStartGPSAlgorithm, which reports AlgorithmType.GPSCoordinateSearch but is multi-start.</summary>
        [TestCaseSource(nameof(UnsupportedClasses))]
        public void EveryNonPhaseOneAlgorithmClass_ThrowsNotSupported(Type type)
        {
            Algorithm algorithm = (Algorithm)Activator.CreateInstance(type);

            NotSupportedException exception = Assert.Throws<NotSupportedException>(() => algorithm.ToSAM_Optimiser(new OptimizationSettings(), 1));

            Assert.That(exception.Message, Does.Contain(type.Name));
        }

        [TestCase(AlgorithmType.GPSHookeJeeves)]
        [TestCase(AlgorithmType.GoldenSection)]
        [TestCase(AlgorithmType.GPSCoordinateSearch)]
        public void PhaseOneType_OnAForeignClass_IsNotSubstituted(AlgorithmType algorithmType)
        {
            Assert.Throws<NotSupportedException>(() => new AnyAlgorithm(algorithmType).ToSAM_Optimiser(new OptimizationSettings(), 1));
        }

        // ------------------------------------------------------------------ GPS mesh domain

        /// <summary>
        /// GenOpt 3.1.1's domain (PR3 oracle) is divider &gt; 1, s0 ≥ 0, t &gt; 0, m &gt; 0, all integers. GenOpt rejects 0/1/-1/0/0, and
        /// silently truncates fractions (2.5 ran as 2). The adapter refuses both; it never repairs.
        /// </summary>
        [TestCase(0, 0, 1, 4)]     // the GPSCoordinateSearchAlgorithm default divider
        [TestCase(1, 0, 1, 4)]
        [TestCase(2.5, 0, 1, 4)]
        [TestCase(2, -1, 1, 4)]
        [TestCase(2, 0.5, 1, 4)]
        [TestCase(2, 0, 0, 4)]
        [TestCase(2, 0, 1.5, 4)]
        [TestCase(2, 0, 1, 0)]
        [TestCase(2, 0, 1, 1.5)]
        [TestCase(double.NaN, 0, 1, 4)]
        [TestCase(1e20, 0, 1, 4)]
        public void InvalidMesh_IsRefusedNotRepaired(double divider, double s0, double t, double m)
        {
            Assert.Throws<GenOptCompatibilityException>(() => HookeJeeves(divider, s0, t, m).ToSAM_Optimiser(new OptimizationSettings(), 1));
        }

        // ------------------------------------------------------------------ OptimizationSettings

        [Test]
        public void MaxEqualResults_BelowTwo_IsRefusedLikeJava()
        {
            Assert.Throws<GenOptCompatibilityException>(() => HookeJeeves().ToSAM_Optimiser(new OptimizationSettings { MaxEqualResults = 1 }, 1));
            Assert.That(HookeJeeves().ToSAM_Optimiser(new OptimizationSettings { MaxEqualResults = 2 }, 1), Is.Not.Null);
        }

        [Test]
        public void WriteStepNumber_True_IsRefused()
        {
            Assert.Throws<GenOptCompatibilityException>(() => HookeJeeves().ToSAM_Optimiser(new OptimizationSettings { WriteStepNumber = true }, 1));
        }

        [TestCase(0)]
        [TestCase(-3)]
        public void MaxIte_BelowOne_IsRefused(int maxIte)
        {
            Assert.Throws<GenOptCompatibilityException>(() => HookeJeeves().ToSAM_Optimiser(new OptimizationSettings { MaxIterations = maxIte }, 1));
        }

        /// <summary>UnitsOfExecution is accepted but has no kernel setting: the native run is always sequential.</summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(8)]
        public void UnitsOfExecution_IsAcceptedAndDoesNotChangeTheOptimiser(int unitsOfExecution)
        {
            HookeJeeves result = (HookeJeeves)HookeJeeves().ToSAM_Optimiser(new OptimizationSettings { UnitsOfExecution = unitsOfExecution }, 1);

            Assert.That(result.MaximumSimulations, Is.EqualTo(2000));
        }

        // ------------------------------------------------------------------ parameters and objectives

        [Test]
        public void NumberParameter_MapsThroughJavaReading()
        {
            OptimisationParameter result = new NumberParameter { Name = "x", Initial = -0.0, Min = 1.5e-30, Max = 1.5e20, Step = 0.30000000000000004 }.ToSAM_OptimisationParameter();

            Assert.That(result.Name, Is.EqualTo("x"));
            Assert.That(BitConverter.DoubleToInt64Bits(result.Initial), Is.EqualTo(BitConverter.DoubleToInt64Bits(0.0)));
            Assert.That(result.Minimum, Is.EqualTo(1.5000000000000001e-30));
            Assert.That(result.Maximum, Is.EqualTo(1.5e20));
            Assert.That(result.Step, Is.EqualTo(0.30000000000000004));
        }

        [Test]
        public void Problem_HasOneOutputPerObjective_FirstIsMinimised()
        {
            ObjectiveFunctionLocation objectives = new ObjectiveFunctionLocation(new[] { "Result", "Cooling", "Heating" });

            OptimisationProblem problem = new List<IParameter> { new NumberParameter { Name = "a", Initial = 1, Min = 0, Max = 2, Step = 1 } }.ToSAM_OptimisationProblem(objectives);

            Assert.That(problem.OutputCount, Is.EqualTo(3));
            Assert.That(Convert.Objectives(objectives).Select(o => o.Name), Is.EqualTo(new[] { "Result", "Cooling", "Heating" }));
            Assert.That(Convert.Objectives(objectives)[0].Delimiter, Is.EqualTo("Result::"));
        }

        [Test]
        public void Problem_InvalidInputs_AreRefused()
        {
            ObjectiveFunctionLocation result = new ObjectiveFunctionLocation(new[] { "Result" });
            NumberParameter ok = new NumberParameter { Name = "a", Initial = 1, Min = 0, Max = 2, Step = 1 };

            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter>().ToSAM_OptimisationProblem(result));
            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter> { ok }.ToSAM_OptimisationProblem(new ObjectiveFunctionLocation()));
            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter> { ok, new NumberParameter { Name = "a" } }.ToSAM_OptimisationProblem(result));
            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter> { new NumberParameter { Name = "a,b" } }.ToSAM_OptimisationProblem(result));
            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter> { new NumberParameter { Name = " " } }.ToSAM_OptimisationProblem(result));
            Assert.Throws<GenOptCompatibilityException>(() => new List<IParameter> { new NumberParameter { Name = "a", Step = double.NaN } }.ToSAM_OptimisationProblem(result));
        }
    }
}
