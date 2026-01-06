// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Create
    {
        public static List<InternalCondition> InternalConditions(this TIC.Document document)
        {
            List<TIC.InternalCondition> internalConditions = Query.InternalConditions(document);

            return internalConditions?.ConvertAll(x => Convert.ToSAM(x));
        }
    }
}
