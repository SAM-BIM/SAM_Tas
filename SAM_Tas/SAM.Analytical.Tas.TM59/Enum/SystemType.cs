// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.Tas
{
    public enum SystemType
    {
        [Description("Undefined")] Undefined = 2,
        [Description("Nat Vent")] NaturalVentilation = 0,
        [Description("Mech Vent")] MechanicalVentilation = 1,
    }
}
