// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// <b>Which aperture constructions the canonicalisation has left orphaned</b>, as a pure function of
        /// names - no COM.
        /// <para>
        /// TAS's gbXML conversion names the constructions it creates after the T3D window, which carries the
        /// physical aperture GUID. Once every surface points at a shared canonical element carrying a shared
        /// construction, those per-aperture constructions are referenced by nothing.
        /// </para>
        /// <para>
        /// <b>A construction is only orphaned here if it names a physical aperture</b>
        /// (<see cref="NamesContainingApertureGuid(IEnumerable{string}, IEnumerable{Guid})"/>). That is the
        /// whole gate, and it is an exact test against the model's own aperture GUIDs rather than a heuristic:
        /// an unreferenced construction whose name carries NO aperture GUID is a reusable definition that
        /// simply has no aperture using it right now - a library template, or an <c>ApertureConstruction</c>
        /// in the model with no windows built from it - and the export has always kept those. Removing one
        /// would be a behaviour change nothing here asks for, so those are reported through
        /// <paramref name="unreferenced_Kept"/> instead of removed.
        /// </para>
        /// </summary>
        /// <param name="constructionNames">Every construction name in the building.</param>
        /// <param name="referencedNames">The names carried by building elements that survived the sweep.</param>
        /// <param name="apertureGuids">The model's physical aperture GUIDs.</param>
        /// <param name="unreferenced_Kept">Unreferenced aperture constructions that name no physical aperture, and so were left in place.</param>
        /// <returns>The construction names to remove, in input order. Never null.</returns>
        public static List<string> OrphanApertureConstructionNames(IEnumerable<string> constructionNames, IEnumerable<string> referencedNames, IEnumerable<Guid> apertureGuids, out List<string> unreferenced_Kept)
        {
            List<string> result = new List<string>();
            unreferenced_Kept = new List<string>();

            if (constructionNames == null)
            {
                return result;
            }

            HashSet<string> referenced = new HashSet<string>(referencedNames == null ? Enumerable.Empty<string>() : referencedNames.Where(x => x != null));

            //Only this export's own pane/frame convention is considered at all. A panel construction has no
            //business in an aperture sweep, whether anything references it or not.
            List<string> candidates = constructionNames
                .Where(x => !string.IsNullOrWhiteSpace(x) && !referenced.Contains(x) && TryDecomposeConstructionName(x, out string _, out Analytical.AperturePart _))
                .ToList();

            HashSet<string> instanceNamed = new HashSet<string>(NamesContainingApertureGuid(candidates, apertureGuids));

            foreach (string candidate in candidates)
            {
                if (instanceNamed.Contains(candidate))
                {
                    result.Add(candidate);
                }
                else
                {
                    unreferenced_Kept.Add(candidate);
                }
            }

            return result;
        }
    }
}
