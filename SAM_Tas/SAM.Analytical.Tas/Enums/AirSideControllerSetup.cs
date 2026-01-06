// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    public enum AirSideControllerSetup
    {
        [Description("Undefined")] Undefined,
        [Description("ThermUL")] ThermUL,
        [Description("ThermLL")] ThermLL,
        [Description("TempHighZero")] TempHighZero,
        [Description("TempLowZero")] TempLowZero,
        [Description("TempPassThrough")] TempPassThrough,
        [Description("ThermBothL")] ThermBothL,
        [Description("Press")] Press,
        [Description("AlwaysReturnOne")] AlwaysReturnOne,
        [Description("TempDifference")] TempDifference,
        [Description("EnthLowZero")] EnthLowZero
    }
}

