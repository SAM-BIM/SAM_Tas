// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public class FibonacciDivisionAlgorithm : Algorithm
    {
        [Attributes.Name("Main"), Attributes.Index(0)]
        public override AlgorithmType AlgorithmType { get; } = AlgorithmType.Fibonacci;

        [Attributes.Name("IntervalReduction"), Attributes.Index(1)]
        public double IntervalReduction { get; set; } = 0;


    }
}
