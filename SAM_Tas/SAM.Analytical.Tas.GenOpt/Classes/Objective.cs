// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public class Objective
    {
        public string Name { get; set; } = null;
        public string Delimiter { get; set; } = null;

        public Objective(string name, string delimiter)
        {
            Name = name;
            Delimiter = delimiter;
        }

        public Objective(string name) 
        {
            Name = name;
            Delimiter = name == null ? null : string.Format("{0}::", name);
        }

    }
}
