// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// The marker of the one segment of a TAS zone description that carries the SAM space a zone was made
        /// from. Same <c>[Key]=value</c> segment style (separated by <c>"; "</c>) as <c>[Id]</c>, <c>[LevelName]</c>
        /// and <c>[SAM_META_V1]</c> - <see cref="SAMZoneMetadata.Compose"/> preserves a segment it does not own
        /// verbatim, so the marker survives <c>Modify.UpdateZone</c>.
        /// </summary>
        public const string ZoneDescriptionMarker_SpaceGuid = "[SpaceGuid]=";

        /// <summary>
        /// The zone description the direct SAM -> T3D route writes: the SAM space GUID, in a segment.
        /// <para>
        /// <b>Why a description at all.</b> A TAS <c>Zone.GUID</c> cannot be set through COM (it throws 0xF18D on
        /// write even though the type library declares a setter), and TAS mints its own. The description is the
        /// writable field that survives <c>T3D -> TBD</c> (measured), so it is where the identity that lets the
        /// later TBD steps map a zone back to its space has to live.
        /// </para>
        /// </summary>
        public static string ZoneDescription(this Guid spaceGuid)
        {
            return ZoneDescriptionMarker_SpaceGuid + spaceGuid.ToString("D");
        }

        /// <summary>
        /// The SAM space GUID a zone description names, if it carries a <see cref="ZoneDescriptionMarker_SpaceGuid"/>
        /// segment. False for a description that does not - every zone the gbXML route makes, and any zone a user
        /// authored - so nothing resolves by this identity unless the direct route put it there.
        /// </summary>
        public static bool TryGetSpaceGuid(this string zoneDescription, out Guid spaceGuid)
        {
            spaceGuid = Guid.Empty;

            if (string.IsNullOrWhiteSpace(zoneDescription))
            {
                return false;
            }

            foreach (string segment in zoneDescription.Split(';'))
            {
                string segment_Temp = segment.Trim();
                if (!segment_Temp.StartsWith(ZoneDescriptionMarker_SpaceGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                return Guid.TryParse(segment_Temp.Substring(ZoneDescriptionMarker_SpaceGuid.Length).Trim(), out spaceGuid);
            }

            return false;
        }
    }
}
