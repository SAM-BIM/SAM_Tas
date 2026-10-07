// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Query
    {
        /// <summary>
        /// Puts each TAS3D zone's properties onto the SAM space it was made from - the zone loop
        /// <c>Query.UpdateT3D</c> runs on the gbXML route, for the direct route (<c>Convert.ToT3D</c>).
        /// <para>
        /// A zone resolves to its space by the SAM space GUID its description carries
        /// (<see cref="ZoneDescription"/>), and only by name where it carries none. Both routes then do the same
        /// thing - clone the space and add the <c>ParameterSet</c> <c>Create.ParameterSet(ActiveSetting.Setting, zone)</c>
        /// reads from the zone - so a model that has been through either route carries the same TAS stamps.
        /// </para>
        /// <para>
        /// Call this once TAS has computed the zones: a T3D zone reports no floor area or volume until the T3D is
        /// exported.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">The model whose spaces are stamped.</param>
        /// <param name="building">The TAS3D building holding the zones.</param>
        /// <returns>A model with the stamped spaces; the model itself when there is nothing to stamp.</returns>
        public static AnalyticalModel UpdateSpaces(this AnalyticalModel analyticalModel, TAS3D.Building building)
        {
            AdjacencyCluster adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster == null || building == null)
            {
                return analyticalModel;
            }

            Dictionary<string, ISpace> spacesByName = adjacencyCluster.SpaceDictionary<ISpace>();

            bool updated = false;

            Dictionary<string, TAS3D.Zone> zones = building.ZoneDictionary();
            foreach (KeyValuePair<string, TAS3D.Zone> keyValuePair in zones)
            {
                TAS3D.Zone zone = keyValuePair.Value;
                if (zone == null)
                {
                    continue;
                }

                ISpace space = null;
                if (zone.description.TryGetSpaceGuid(out Guid spaceGuid))
                {
                    space = adjacencyCluster.GetObject<Space>(spaceGuid);
                    if (space == null)
                    {
                        space = adjacencyCluster.GetObject<ExternalSpace>(spaceGuid);
                    }
                }

                if (space == null && (spacesByName == null || !spacesByName.TryGetValue(keyValuePair.Key, out space)))
                {
                    continue;
                }

                if (space == null)
                {
                    continue;
                }

                ISpace space_New = space.Clone();
                if (space_New is SAMObject)
                {
                    ((SAMObject)space_New).Add(Create.ParameterSet(ActiveSetting.Setting, zone));
                }

                if (space_New is ExternalSpace)
                {
                    zone.external = true;
                }

                adjacencyCluster.AddObject(space_New);
                updated = true;
            }

            return updated ? new AnalyticalModel(analyticalModel, adjacencyCluster) : analyticalModel;
        }
    }
}
