// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public class SimulationConfigFile : GenOptFile
    {
        [Attributes.Name("SimulationError")]
        public SimulationError SimulationError { get; set; }

        [Attributes.Name("IO")]
        public IO IO { get; set; }

        [Attributes.Name("SimulationStart")]
        public SimulationStart SimulationStart { get; set; }
    }
}
