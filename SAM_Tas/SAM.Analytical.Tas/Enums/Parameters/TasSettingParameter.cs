// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;
using SAM.Core;
using SAM.Core.Attributes;

namespace SAM.Analytical.Tas
{
    [AssociatedTypes(typeof(Setting)), Description("Tas Setting Parameter")]
    public enum TasSettingParameter
    {
        [ParameterProperties("Default TIC File Name", "Default TIC File Name"), ParameterValue(ParameterType.String)] DefaultTICFileName,
        [ParameterProperties("Default TCR File Name", "Default TCR File Name"), ParameterValue(ParameterType.String)] DefaultTCRFileName,
    }
}
