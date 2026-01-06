// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using SAM.Core.Attributes;

namespace SAM.Analytical.Tas
{
    [AssociatedTypes(typeof(Core.Material)), Description("Material Parameter")]
    public enum MaterialParameter
    {
        [ParameterProperties("UniqueId", "UniqueId"), ParameterValue(Core.ParameterType.String)] UniqueId,
    }
}
