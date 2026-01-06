// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas.GenOpt
{
    public class CommandFile: GenOptFile
    {
        [Attributes.Name("Vary"), Attributes.Index(0)]
        public List<IParameter> Parameters { get; set; }

        [Attributes.Name("OptimizationSettings"), Attributes.Index(1)]
        public OptimizationSettings OptimizationSettings { get; set; }

        [Attributes.Name("Algorithm"), Attributes.Index(2)]
        public Algorithm Algorithm { get; set; }
    }
}
