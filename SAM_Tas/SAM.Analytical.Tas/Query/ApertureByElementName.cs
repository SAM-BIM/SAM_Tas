// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The SAM aperture a TBD aperture surface was made from, read off its building element's NAME, and which half of it the
        /// surface is - or null.
        /// <para>
        /// Both routes give TAS one window object per aperture named after the aperture (<c>Windows: &lt;name&gt; &lt;aperture GUID&gt; </c>,
        /// see <c>Query.T3DImportPlan</c> and the gbXML opening names), and TAS names that aperture's pane and frame elements
        /// <c>... &lt;GUID&gt; -pane</c> / <c>-frame</c>. Until <c>Modify.UpdateApertureDefinitions</c> rebinds them onto shared
        /// definitions, that GUID is a stable identity that does not depend on where TAS put the surface. Geometry does: with
        /// UseBEWidths ON every surface is offset from the SAM panel by half its element's width, and a point-in-face match
        /// within tolerance finds nothing (house model: 0 of 25 apertures).
        /// </para>
        /// <para>
        /// Only an aperture element (pane or frame by <paramref name="bEType"/>) whose name decodes to a GUID present in
        /// <paramref name="apertures"/> answers; anything else - a shared definition name, an opaque element, a GUID of another
        /// space's aperture - answers null and the caller matches by geometry as before.
        /// </para>
        /// </summary>
        public static Aperture ApertureByElementName(string buildingElementName, int bEType, IDictionary<System.Guid, Aperture> apertures, out AperturePart aperturePart)
        {
            aperturePart = Analytical.AperturePart.Undefined;

            if (string.IsNullOrWhiteSpace(buildingElementName) || apertures == null || apertures.Count == 0)
            {
                return null;
            }

            AperturePart aperturePart_Temp = AperturePart_BEType(bEType);
            if (aperturePart_Temp == Analytical.AperturePart.Undefined)
            {
                return null;
            }

            if (!UniqueNameDecomposition(buildingElementName, out string _, out string _, out System.Guid? guid, out int _) || guid == null || !guid.HasValue)
            {
                return null;
            }

            if (!apertures.TryGetValue(guid.Value, out Aperture aperture) || aperture == null)
            {
                return null;
            }

            aperturePart = aperturePart_Temp;
            return aperture;
        }
    }
}
