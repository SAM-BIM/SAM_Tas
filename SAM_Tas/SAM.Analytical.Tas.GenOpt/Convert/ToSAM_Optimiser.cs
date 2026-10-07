// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Math;
using System;
using System.Globalization;

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Convert
    {
        /// <summary>The algorithms the native optimiser supports (Phase 1).</summary>
        public static readonly AlgorithmType[] NativeAlgorithmTypes = { AlgorithmType.GPSCoordinateSearch, AlgorithmType.GPSHookeJeeves, AlgorithmType.GoldenSection };

        /// <summary>
        /// Maps a GenOpt algorithm object and its optimisation settings to the native SAM.Math optimiser.
        /// <para>
        /// Only GPSCoordinateSearch, GPSHookeJeeves and GoldenSection are supported. Every other algorithm throws
        /// <see cref="NotSupportedException"/> and is never replaced by another one. Settings the Java route could
        /// not run, or that are invalid, throw <see cref="GenOptCompatibilityException"/>. Nothing is rounded, clamped
        /// or repaired.
        /// </para>
        /// </summary>
        /// <param name="algorithm">The algorithm object, as written by the Java route.</param>
        /// <param name="optimizationSettings">The OptimizationSettings section.</param>
        /// <param name="parameterCount">Number of optimisation parameters (golden section requires exactly one).</param>
        public static Optimiser ToSAM_Optimiser(this Algorithm algorithm, OptimizationSettings optimizationSettings, int parameterCount)
        {
            if (algorithm == null)
            {
                throw new ArgumentNullException(nameof(algorithm));
            }

            if (optimizationSettings == null)
            {
                throw new ArgumentNullException(nameof(optimizationSettings));
            }

            Optimiser result;
            switch (algorithm.AlgorithmType)
            {
                case AlgorithmType.GPSHookeJeeves:
                    GPSHookeJeevesAlgorithm hookeJeeves = algorithm as GPSHookeJeevesAlgorithm;
                    if (hookeJeeves == null)
                    {
                        throw Unsupported(algorithm);
                    }

                    result = Mesh(new HookeJeeves(), hookeJeeves.MeshSizeDivider, hookeJeeves.InitialMeshSizeExponent, hookeJeeves.MeshSizeExponentIncrement, hookeJeeves.NumberOfStepReduction);
                    break;

                case AlgorithmType.GPSCoordinateSearch:
                    if (!(algorithm is GPSCoordinateSearchAlgorithm))
                    {
                        throw Unsupported(algorithm);
                    }

                    // Owner decision D3, from a Java-only GenOpt 3.1.1 oracle: the existing writer always emits
                    // 'Seed' and 'NumberOfInitialPoint' for GPSCoordinateSearch. Without 'MultiStart', GenOpt rejects
                    // both ("Unknown or unexpected keyword 'Seed'"), whatever their values, so the Java route cannot run
                    // any GPSCoordinateSearchAlgorithm. The native adapter mirrors that.
                    throw new GenOptCompatibilityException("GPSCoordinateSearch cannot be run: the Java route writes 'Seed' and 'NumberOfInitialPoint' for it, and GenOpt 3.1.1 rejects both without 'MultiStart' ('Unknown or unexpected keyword'), whatever their values. The native adapter mirrors the Java route and does not run this configuration.");

                case AlgorithmType.GoldenSection:
                    GoldenSectionAlgorithm goldenSectionAlgorithm = algorithm as GoldenSectionAlgorithm;
                    if (goldenSectionAlgorithm == null)
                    {
                        throw Unsupported(algorithm);
                    }

                    if (parameterCount != 1)
                    {
                        throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "GoldenSection requires exactly one optimisation parameter; {0} were given.", parameterCount));
                    }

                    double absoluteDifference = GenOptNumber.ReadAlgorithmValue(goldenSectionAlgorithm.AbsDiffFunction, "AbsDiffFunction");
                    if (absoluteDifference < 0)
                    {
                        throw new GenOptCompatibilityException("AbsDiffFunction must not be negative; got " + GenOptNumber.WriterText(goldenSectionAlgorithm.AbsDiffFunction) + ".");
                    }

                    result = new GoldenSection
                    {
                        StoppingCriterion = GoldenSectionStoppingCriterion.AbsoluteDifference,
                        AbsoluteDifference = absoluteDifference,
                    };
                    break;

                default:
                    throw Unsupported(algorithm);
            }

            result.MaximumSimulations = MaximumSimulations(optimizationSettings);
            return result;
        }

        private static NotSupportedException Unsupported(Algorithm algorithm)
        {
            return new NotSupportedException(string.Format(
                CultureInfo.InvariantCulture,
                "GenOpt algorithm '{0}' ({1}) is not supported by the native optimiser. Supported (Phase 1): {2}. No other algorithm is substituted.",
                algorithm.AlgorithmType,
                algorithm.GetType().Name,
                string.Join(", ", NativeAlgorithmTypes)));
        }

        /// <summary>
        /// GPS mesh keywords. GenOpt 3.1.1's domain (Java-only oracle): MeshSizeDivider &gt; 1, InitialMeshSizeExponent
        /// ≥ 0, MeshSizeExponentIncrement &gt; 0, NumberOfStepReduction &gt; 0. GenOpt reads these as integers and silently
        /// truncates a fractional value (e.g. 2.5 runs as 2); the native adapter refuses it instead of repairing it.
        /// </summary>
        private static GeneralisedPatternSearch Mesh(GeneralisedPatternSearch search, double meshSizeDivider, double initialMeshSizeExponent, double meshSizeExponentIncrement, double numberOfStepReduction)
        {
            search.MeshSizeDivider = MeshInteger("MeshSizeDivider", meshSizeDivider, 2, "larger than 1");
            search.InitialMeshSizeExponent = MeshInteger("InitialMeshSizeExponent", initialMeshSizeExponent, 0, "larger than or equal to 0");
            search.MeshSizeExponentIncrement = MeshInteger("MeshSizeExponentIncrement", meshSizeExponentIncrement, 1, "larger than 0");
            search.NumberOfStepReduction = MeshInteger("NumberOfStepReduction", numberOfStepReduction, 1, "larger than 0");
            return search;
        }

        private static int MeshInteger(string keyword, double value, int minimum, string domain)
        {
            string text = GenOptNumber.WriterText(value);
            if (double.IsNaN(value) || double.IsInfinity(value) || text.IndexOf('E') >= 0 || value != System.Math.Floor(value) || value > int.MaxValue)
            {
                throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "{0} must be an integer {1}; got '{2}'. (Java GenOpt 3.1.1 would silently truncate a fractional value; the native adapter does not repair settings.)", keyword, domain, text));
            }

            if (value < minimum)
            {
                throw new GenOptCompatibilityException(string.Format(CultureInfo.InvariantCulture, "{0} must be an integer {1}; got '{2}' (GenOpt 3.1.1 rejects it too).", keyword, domain, text));
            }

            return (int)value;
        }

        /// <summary>
        /// OptimizationSettings: MaxIte is the simulation limit. MaxEqualResults must be ≥ 2, as GenOpt requires (it
        /// has no effect on the Phase-1 algorithms). WriteStepNumber = true is outside the supported scope.
        /// UnitsOfExecution is accepted but never makes the native run parallel.
        /// </summary>
        private static int MaximumSimulations(OptimizationSettings optimizationSettings)
        {
            if (optimizationSettings.MaxIterations < 1)
            {
                throw new GenOptCompatibilityException("MaxIte must be at least 1; got " + optimizationSettings.MaxIterations.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (optimizationSettings.MaxEqualResults < 2)
            {
                throw new GenOptCompatibilityException("MaxEqualResults must be greater than 1 (GenOpt 3.1.1 rejects it otherwise); got " + optimizationSettings.MaxEqualResults.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (optimizationSettings.WriteStepNumber)
            {
                throw new GenOptCompatibilityException("WriteStepNumber = true is not supported by the native optimiser.");
            }

            return optimizationSettings.MaxIterations;
        }
    }
}
