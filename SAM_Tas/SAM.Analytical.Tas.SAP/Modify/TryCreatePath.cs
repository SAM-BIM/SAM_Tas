// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.SAP
{
    public static partial class Modify
    {
        public static bool TryCreatePath(string path_TBD, out string path_SAP)
        {
            path_SAP = null;

            if(string.IsNullOrWhiteSpace(path_TBD))
            {
                return false;
            }

            path_SAP = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path_TBD), System.IO.Path.GetFileNameWithoutExtension(path_TBD) + "_sapgeomtool.txt");
            return true;
        }
    }
}
