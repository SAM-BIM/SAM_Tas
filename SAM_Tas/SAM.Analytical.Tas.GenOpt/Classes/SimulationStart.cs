// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    [Attributes.Name("SimulationStart")]
    public class SimulationStart : GenOptObject
    {
        [Attributes.Name("Command"), Attributes.QuotedValue()]
        public string Command { get; set; } = "cmd /c \\\"start  /WAIT /MIN \\\"\\\" \\\"C:\\\\Program Files\\\\Environmental Design Solutions Ltd\\\\Tas\\\\TasGenOpt\\\\TasGenExecute.exe\\\" \\\"C:\\\\Users\\\\DengusiakM\\\\Desktop\\\\SAM\\\\2023-11-21_GenOpt1\\\" ";

        [Attributes.Name("WriteInputFileExtension")]
        public bool WriteInputFileExtension { get; set; } = true;
    }
}
