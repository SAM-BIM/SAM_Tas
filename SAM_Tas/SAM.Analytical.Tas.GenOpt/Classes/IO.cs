// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    [Attributes.Name("IO")]
    public class IO : GenOptObject
    {
        [Attributes.Name("NumberFormat")]
        public NumberFormat NumberFormat { get; set; } = NumberFormat.Double;
    }
}
