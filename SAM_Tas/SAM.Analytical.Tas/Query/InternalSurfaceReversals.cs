// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// For every internal panel that separates two spaces, which of its two TBD zone surfaces is <b>reversed</b> - the one
        /// whose construction layers run the other way round for its zone - by SAM's own convention: the panel's first space
        /// sees the layers as listed, the second sees them reversed. The same convention <c>Modify.Update</c> (SAM's direct TBD
        /// export) writes and the gbXML route ends up with.
        /// <para>
        /// <b>Why this has to be said at all on the direct route.</b> <c>WrImportIDF.AddInternalSurface</c> takes a
        /// <c>reverseElement</c> flag, and measured on the real model it has <i>no effect</i> on the TBD; TAS chooses the
        /// reversed side from its own geometry, and on an 18-partition model that choice disagrees with SAM's convention for
        /// half the partitions. It matters whenever a construction is not symmetric - a paint film on one face only -
        /// and the simulation results move with it. So the importer cannot be told, and the TBD is corrected afterwards
        /// from the identities <c>Modify.UpdateIds</c> stamped.
        /// </para>
        /// <para>
        /// Panels that are not walls (floors, ceilings and slopes) are not touched: TAS assigns those from the geometry (the face seen from
        /// above is the unreversed one), identically to the gbXML route in every horizontal case measured; slopes were not measured, so
        /// they are not given a rule either.
        /// </para>
        /// </summary>
        /// <param name="adjacencyCluster">The model, after <c>Modify.UpdateIds</c> has stamped panels and spaces.</param>
        /// <returns>
        /// Physical surface key -> whether it is reversed. A panel whose stamps do not name both of its sides, or whose spaces
        /// are not stamped with a zone, contributes nothing: this never guesses.
        /// </returns>
        public static Dictionary<ZoneSurfaceKey, bool> InternalSurfaceReversals(this AdjacencyCluster adjacencyCluster)
        {
            Dictionary<ZoneSurfaceKey, bool> result = new Dictionary<ZoneSurfaceKey, bool>();

            List<Panel> panels = adjacencyCluster?.GetPanels();
            if (panels == null)
            {
                return result;
            }

            foreach (Panel panel in panels)
            {
                if (panel == null || panel.PanelType == Analytical.PanelType.Shade)
                {
                    continue;
                }

                List<Space> spaces = adjacencyCluster.GetSpaces(panel);
                if (spaces == null || spaces.Count != 2)
                {
                    continue;
                }

                if (!IsVertical(panel))
                {
                    continue;
                }

                if (!spaces[0].TryGetValue(SpaceParameter.ZoneGuid, out string zoneGuid_First) || !spaces[1].TryGetValue(SpaceParameter.ZoneGuid, out string zoneGuid_Second))
                {
                    continue;
                }

                zoneGuid_First = NormalizeZoneGuid(zoneGuid_First);
                zoneGuid_Second = NormalizeZoneGuid(zoneGuid_Second);
                if (zoneGuid_First == null || zoneGuid_Second == null || zoneGuid_First == zoneGuid_Second)
                {
                    continue;
                }

                panel.TryGetValue(PanelParameter.ZoneSurfaceReference_1, out Core.Tas.ZoneSurfaceReference reference_1);
                panel.TryGetValue(PanelParameter.ZoneSurfaceReference_2, out Core.Tas.ZoneSurfaceReference reference_2);

                ZoneSurfaceKey key_1 = reference_1.ZoneSurfaceKey();
                ZoneSurfaceKey key_2 = reference_2.ZoneSurfaceKey();
                if (key_1 == null || key_2 == null)
                {
                    continue;
                }

                string zone_1 = NormalizeZoneGuid(reference_1.ZoneGuid);
                string zone_2 = NormalizeZoneGuid(reference_2.ZoneGuid);

                // Each of the two stamps must belong to a different one of the two spaces' zones, or the pairing is not what it claims.
                bool straight = zone_1 == zoneGuid_First && zone_2 == zoneGuid_Second;
                bool crossed = zone_1 == zoneGuid_Second && zone_2 == zoneGuid_First;
                if (!straight && !crossed)
                {
                    continue;
                }

                result[key_1] = crossed;   // stamp 1 belongs to the second space -> reversed
                result[key_2] = straight;  // stamp 2 belongs to the second space -> reversed
            }

            return result;
        }

        // A wall: its normal is (nearly) horizontal. Anything else - floors, ceilings, and slopes in between - keeps the side TAS chose.
        private static bool IsVertical(Panel panel)
        {
            Vector3D normal = panel.Normal;
            return normal != null && global::System.Math.Abs(normal.Unit.Z) < 0.05;
        }
    }
}
