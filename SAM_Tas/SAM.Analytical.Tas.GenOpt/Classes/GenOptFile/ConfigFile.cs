// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public class ConfigFile : GenOptFile
    {
        [Attributes.Name("Simulation")]
        public Simulation Simulation { get; set; }

        [Attributes.Name("Optimization")]
        public Optimization Optimization { get; set; }
    }
}
