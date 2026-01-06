// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.Tas.GenOpt
{
    public static partial class Query
    {
        public static string TasGenOptJavaPath()
        {
            return System.IO.Path.Combine(TasGenOptDirectory(), "genopt.jar");
        }
    }
}
