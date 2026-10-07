// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The name a TAS3D storey (<c>TAS3D.Floor</c>) made by the direct SAM -> T3D importer should carry: the SAM level name
        /// every one of its zones' spaces states (<c>SpaceParameter.LevelName</c>), as the gbXML route's storey carries the
        /// gbXML <c>BuildingStorey</c> name.
        /// <para>
        /// It never guesses: null - leave TAS's own "Storey at level ... m" - when the storey has no zones, when any of them has no
        /// level name, or when they disagree.
        /// </para>
        /// </summary>
        /// <param name="levelNames">The level name of the space behind each zone on the storey (null or blank where it has none).</param>
        /// <returns>The common level name, trimmed, or null.</returns>
        public static string StoreyName(this IEnumerable<string> levelNames)
        {
            if (levelNames == null)
            {
                return null;
            }

            string result = null;
            foreach (string levelName in levelNames)
            {
                if (string.IsNullOrWhiteSpace(levelName))
                {
                    return null;
                }

                string levelName_Trimmed = levelName.Trim();
                if (result == null)
                {
                    result = levelName_Trimmed;
                }
                else if (!string.Equals(result, levelName_Trimmed, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            return result;
        }
    }
}
