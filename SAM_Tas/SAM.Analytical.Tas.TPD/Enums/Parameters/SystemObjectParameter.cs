// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using SAM.Core.Attributes;
using SAM.Core.Systems;

namespace SAM.Analytical.Tas.TPD
{
    [AssociatedTypes(typeof(ISystemObject)), Description("System Object Parameter")]
    public enum SystemObjectParameter
    {
        [ParameterProperties("Reference", "Reference"), ParameterValue(Core.ParameterType.String)] Reference,
    }
}
