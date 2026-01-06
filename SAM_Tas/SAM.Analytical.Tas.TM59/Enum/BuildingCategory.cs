// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    public enum BuildingCategory
    {
        [Description("Undefined")] Undefined,
        [Description("Category I")] Category_I = 0,
        [Description("Category II")] Category_II = 1,
    }
}
