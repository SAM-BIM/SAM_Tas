// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using SAM.Core.Attributes;

namespace SAM.Analytical.Tas
{
    [AssociatedTypes(typeof(Zone)), Description("Zone Parameter")]
    public enum ZoneParameter
    {
        [ParameterProperties("Zone Group Category TBD", "Zone Group Category TBD"), ParameterValue(Core.ParameterType.String)] TBDZoneGroup,
    }
}
