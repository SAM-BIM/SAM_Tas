// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The SAM aperture whose PANE stamps (<see cref="ApertureParameter.PaneZoneSurfaceReference_1"/> / <c>_2</c>, written by
        /// <c>Modify.UpdateIds</c>) name a pane surface <paramref name="aperture_TBD"/> - an aperture read off the TBD by
        /// <c>Convert.ToSAM</c>, which stamps the same parameters with the surfaces it was read from - or null.
        /// <para>
        /// Zone and surface number identify one physical TBD surface, so this is an identity match that holds wherever TAS put the
        /// surface (UseBEWidths ON offsets every pane from SAM's). A frame or an unstamped aperture never answers; the caller then
        /// matches by geometry.
        /// </para>
        /// </summary>
        public static Aperture PaneStampMatch(this Aperture aperture_TBD, IEnumerable<Aperture> apertures)
        {
            if (aperture_TBD == null || apertures == null)
            {
                return null;
            }

            foreach (ApertureParameter apertureParameter in new ApertureParameter[] { ApertureParameter.PaneZoneSurfaceReference_1, ApertureParameter.PaneZoneSurfaceReference_2 })
            {
                if (!aperture_TBD.TryGetValue(apertureParameter, out Core.Tas.ZoneSurfaceReference zoneSurfaceReference) || zoneSurfaceReference == null)
                {
                    continue;
                }

                Aperture aperture = Match(zoneSurfaceReference, apertures, out Analytical.AperturePart aperturePart);
                if (aperture != null && aperturePart == Analytical.AperturePart.Pane)
                {
                    return aperture;
                }
            }

            return null;
        }
    }
}
