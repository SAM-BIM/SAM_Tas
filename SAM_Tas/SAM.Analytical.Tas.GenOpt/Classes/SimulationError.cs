// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    [Attributes.Name("SimulationError")]
    public class SimulationError : GenOptObject
    {
        [Attributes.Name("ErrorMessage"), Attributes.QuotedValue()]
        public string ErrorMessage { get; set; } = "Error";
    }
}
