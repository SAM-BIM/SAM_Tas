// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        /// <summary>
        /// Sets which side of each internal wall is <c>reversed</c> in the TBD to SAM's convention - of the panel's two spaces, the
        /// one earlier in the model sees the construction's layers as listed, the later one sees them reversed - for a TBD the
        /// direct SAM -> T3D route made.
        /// See <see cref="Query.InternalSurfaceReversals(AdjacencyCluster)"/> for why the importer cannot be told and what is
        /// left alone. A surface already on the right side is not written to.
        /// <para>
        /// Run after <see cref="UpdateIds(AdjacencyCluster, TBD.Building, double)"/>: the stamps it leaves are what name the
        /// two surfaces of a panel, and the zone each space resolved to.
        /// </para>
        /// </summary>
        /// <param name="building">The TBD building.</param>
        /// <param name="adjacencyCluster">The model, stamped by <c>UpdateIds</c>.</param>
        /// <param name="count">How many surfaces were changed.</param>
        /// <returns>False when there is nothing to work on.</returns>
        public static bool UpdateReversed(this TBD.Building building, AdjacencyCluster adjacencyCluster, out int count)
        {
            count = 0;

            if (building == null || adjacencyCluster == null)
            {
                return false;
            }

            Dictionary<ZoneSurfaceKey, bool> reversals = adjacencyCluster.InternalSurfaceReversals();
            if (reversals.Count == 0)
            {
                return true;
            }

            Dictionary<ZoneSurfaceKey, TBD.IZoneSurface> surfaceIndex = building.ZoneSurfaceIndex();
            foreach (KeyValuePair<ZoneSurfaceKey, bool> keyValuePair in reversals)
            {
                if (!surfaceIndex.TryGetValue(keyValuePair.Key, out TBD.IZoneSurface zoneSurface) || zoneSurface == null)
                {
                    continue;
                }

                // TAS reports a true flag as -1 or 1 depending on who wrote it; any non-zero value is reversed.
                bool reversed = zoneSurface.reversed != 0;
                if (reversed == keyValuePair.Value)
                {
                    continue;
                }

                zoneSurface.reversed = keyValuePair.Value ? 1 : 0;
                count++;
            }

            return true;
        }
    }
}
