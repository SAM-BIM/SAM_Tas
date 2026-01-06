// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    public enum WaterSideControllerSetup
    {
        [Description("Undefined")] Undefined,
        [Description("Load")] Load,
        [Description("Temperature Low Zero")] TemperatureLowZero,
        [Description("Temperature Difference")] TemperatureDifference,
    }
}

