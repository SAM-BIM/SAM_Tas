// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.Win32;

namespace SAM.Core.Tas
{
    public static partial class Query
    {
        public static string TasDirectory()
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\EDSL\TasManager\TasFiles", "Path", null) as string;
        }
    }
}
