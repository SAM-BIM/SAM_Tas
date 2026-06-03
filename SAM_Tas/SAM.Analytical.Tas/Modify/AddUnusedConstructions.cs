// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        /// <summary>
        /// Imports the TBD building's <b>full</b> construction library — including constructions
        /// not attached to any imported surface — into <paramref name="adjacencyCluster"/> as
        /// standalone template objects.
        /// <para/>
        /// The geometry-driven import (<see cref="Convert.ToSAM(TBD.Building, System.Collections.Generic.Dictionary{string, SAM.Geometry.Spatial.Polygon3D})"/>)
        /// only reaches constructions referenced by a zone surface, so unused entries — e.g. the
        /// transparent <c>Air_Glass</c> placeholder created during HDD sizing, or the Null building
        /// element's construction — are silently dropped on round-trip. This restores them so the
        /// exported TBD keeps the same library it started with.
        /// <para/>
        /// Constructions already present (matched by name) are left untouched; only the missing
        /// ones are added. Transparent constructions become <see cref="ApertureConstruction"/>
        /// templates (pane/frame sides sharing a base name are combined), opaque ones become
        /// <see cref="Construction"/> templates. Both surface back through
        /// <c>AdjacencyCluster.GetConstructions</c>/<c>GetApertureConstructions</c>, which the
        /// exporter (<see cref="UpdateConstructions(TBD.TBDDocument, AnalyticalModel)"/>) already
        /// writes out in full.
        /// </summary>
        /// <returns>The construction/aperture-construction templates added, or null on invalid input.</returns>
        public static List<SAMObject> AddUnusedConstructions(this AdjacencyCluster adjacencyCluster, TBD.Building building)
        {
            if (adjacencyCluster == null || building == null)
            {
                return null;
            }

            List<TBD.Construction> constructions_TBD = Query.Constructions(building);
            if (constructions_TBD == null || constructions_TBD.Count == 0)
            {
                return null;
            }

            // Names already represented in the cluster (attached to a panel/aperture, or added by
            // an earlier call). Aperture constructions are keyed by their base name (the import
            // strips the "-pane"/"-frame" suffix), so dedup against the same stripped form below.
            HashSet<string> constructionNames = new HashSet<string>();
            List<Construction> constructions_Existing = adjacencyCluster.GetConstructions();
            if (constructions_Existing != null)
            {
                foreach (Construction construction in constructions_Existing)
                {
                    if (!string.IsNullOrWhiteSpace(construction?.Name))
                    {
                        constructionNames.Add(construction.Name);
                    }
                }
            }

            HashSet<string> apertureConstructionNames = new HashSet<string>();
            List<ApertureConstruction> apertureConstructions_Existing = adjacencyCluster.GetApertureConstructions();
            if (apertureConstructions_Existing != null)
            {
                foreach (ApertureConstruction apertureConstruction in apertureConstructions_Existing)
                {
                    if (!string.IsNullOrWhiteSpace(apertureConstruction?.Name))
                    {
                        apertureConstructionNames.Add(apertureConstruction.Name);
                    }
                }
            }

            List<SAMObject> result = new List<SAMObject>();

            // Transparent constructions come over as two TBD constructions sharing a base name
            // ("… -pane" / "… -frame"); combine them into one ApertureConstruction template keyed
            // by that base name before adding, so neither side is dropped or double-counted.
            Dictionary<string, ApertureConstruction> apertureConstructions_New = new Dictionary<string, ApertureConstruction>();

            foreach (TBD.Construction construction_TBD in constructions_TBD)
            {
                if (construction_TBD == null || string.IsNullOrWhiteSpace(construction_TBD.name))
                {
                    continue;
                }

                // A glazing's frame side is opaque-typed but still belongs to an aperture (named
                // "… -frame"); route it down the aperture path by suffix so it combines with its
                // pane rather than landing as a stray opaque construction.
                string name_TBD = construction_TBD.name.Trim();
                bool aperturePart = name_TBD.EndsWith(AperturePart.Pane.Sufix()) || name_TBD.EndsWith(AperturePart.Frame.Sufix());

                if (construction_TBD.type == TBD.ConstructionTypes.tcdTransparentConstruction || aperturePart)
                {
                    ApertureConstruction apertureConstruction = construction_TBD.ToSAM_ApertureConstruction(ApertureType.Window);
                    if (apertureConstruction == null || string.IsNullOrWhiteSpace(apertureConstruction.Name))
                    {
                        continue;
                    }

                    if (apertureConstructionNames.Contains(apertureConstruction.Name))
                    {
                        continue;
                    }

                    if (apertureConstructions_New.TryGetValue(apertureConstruction.Name, out ApertureConstruction apertureConstruction_Existing))
                    {
                        List<ConstructionLayer> paneConstructionLayers = apertureConstruction_Existing.HasPaneConstructionLayers() ? apertureConstruction_Existing.PaneConstructionLayers : apertureConstruction.PaneConstructionLayers;
                        List<ConstructionLayer> frameConstructionLayers = apertureConstruction_Existing.HasFrameConstructionLayers() ? apertureConstruction_Existing.FrameConstructionLayers : apertureConstruction.FrameConstructionLayers;

                        apertureConstruction = new ApertureConstruction(apertureConstruction_Existing.Guid, apertureConstruction_Existing.Name, apertureConstruction_Existing.ApertureType, paneConstructionLayers, frameConstructionLayers);
                    }

                    apertureConstructions_New[apertureConstruction.Name] = apertureConstruction;
                }
                else
                {
                    Construction construction = construction_TBD.ToSAM();
                    if (construction == null || string.IsNullOrWhiteSpace(construction.Name))
                    {
                        continue;
                    }

                    if (constructionNames.Contains(construction.Name))
                    {
                        continue;
                    }

                    if (adjacencyCluster.AddObject(construction))
                    {
                        constructionNames.Add(construction.Name);
                        result.Add(construction);
                    }
                }
            }

            foreach (ApertureConstruction apertureConstruction in apertureConstructions_New.Values)
            {
                if (adjacencyCluster.AddObject(apertureConstruction))
                {
                    result.Add(apertureConstruction);
                }
            }

            return result;
        }
    }
}
