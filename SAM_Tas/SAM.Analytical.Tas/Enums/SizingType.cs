// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    public enum SizingType
    {
        [Description("Undefined")] Undefined,
        [Description("No Sizing")]  NoSizing,
        [Description("Design Sizing Only")] DesignSizingOnly,
        [Description("Sizing")] Sizing,

    }
}
