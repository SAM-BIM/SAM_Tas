// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// Which zone a SAM space resolves to when its stamps are being refreshed
        /// (<c>Modify.UpdateIds</c>).
        /// <para>
        /// <b>A stamped zone GUID is authoritative when it still identifies a zone</b> - it was
        /// captured before the stamp was cleared (<see cref="SpaceZoneGuids"/>), so a space whose
        /// name no longer equals the TAS zone name still finds its zone. The exact space name is
        /// only a compatibility fallback, for models never stamped or whose stamps name a zone the
        /// TBD no longer holds. No match is a refusal - a null result must never become an
        /// arbitrary zone.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The zone representation (TBD.zone in production; anything in tests).</typeparam>
        /// <param name="zoneGuid">The space's captured <c>SpaceParameter.ZoneGuid</c>, or null.</param>
        /// <param name="spaceName">The space's exact name.</param>
        /// <param name="zonesByGuid">The building's zones by GUID (ordinal keys).</param>
        /// <param name="zonesByName">The building's zones by exact name.</param>
        /// <returns>The resolved zone, or null when neither identity matches.</returns>
        public static T ResolvedZone<T>(string zoneGuid, string spaceName, IReadOnlyDictionary<string, T> zonesByGuid, IReadOnlyDictionary<string, T> zonesByName) where T : class
        {
            T zone = null;

            if (!string.IsNullOrWhiteSpace(zoneGuid) && zonesByGuid != null)
            {
                zonesByGuid.TryGetValue(zoneGuid, out zone);
            }

            if (zone == null && !string.IsNullOrWhiteSpace(spaceName) && zonesByName != null)
            {
                zonesByName.TryGetValue(spaceName, out zone);
            }

            return zone;
        }

        /// <summary>
        /// <see cref="ResolvedZone{T}(string, string, IReadOnlyDictionary{string, T}, IReadOnlyDictionary{string, T})"/>
        /// with one identity ahead of it: the SAM space GUID a zone's description carries.
        /// <para>
        /// The direct SAM -> T3D route writes it there (<see cref="ZoneDescription"/>), because TAS's own zone GUID
        /// cannot be set. It is the strongest identity a zone can have - the converter put it there for exactly this
        /// space, in this run - so it outranks a stamped zone GUID (which may be left from an earlier run) and the
        /// name (which a model need not keep unique). A zone without the marker - every gbXML-route zone - is simply
        /// absent from <paramref name="zonesBySpaceGuid"/>, and resolution is exactly the three-argument form.
        /// </para>
        /// </summary>
        /// <param name="spaceGuid">The space's own GUID.</param>
        /// <param name="zonesBySpaceGuid">The building's zones by the space GUID their description names.</param>
        public static T ResolvedZone<T>(Guid spaceGuid, IReadOnlyDictionary<Guid, T> zonesBySpaceGuid, string zoneGuid, string spaceName, IReadOnlyDictionary<string, T> zonesByGuid, IReadOnlyDictionary<string, T> zonesByName) where T : class
        {
            if (zonesBySpaceGuid != null && zonesBySpaceGuid.Count != 0 && zonesBySpaceGuid.TryGetValue(spaceGuid, out T zone) && zone != null)
            {
                return zone;
            }

            return ResolvedZone(zoneGuid, spaceName, zonesByGuid, zonesByName);
        }
    }
}
