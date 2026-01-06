// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Core.Tas.TPD
{
    [Description("Result Period.")]
    public enum ResultPeriod
    {
        [Description("Undefined")] Undefined,
        [Description("Hourly")] Hourly,
        [Description("Daily")] Daily,
        [Description("Weekly")] Weekly,
        [Description("Monthly")] Monthly,
        [Description("Annual")] Annual,
    }
}
