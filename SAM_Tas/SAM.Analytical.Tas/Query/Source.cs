// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Reflection;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        public static string Source()
        {
            return Assembly.GetExecutingAssembly().GetName()?.Name;
        }
    }
}
