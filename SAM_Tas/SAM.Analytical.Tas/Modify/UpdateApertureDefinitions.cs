// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using SAM.Core;
using System.Collections.Generic;
using System.Linq;
using TBD;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        /// <summary>
        /// <b>Gives the gbXML/T3D route the same reusable aperture definitions the direct export has.</b>
        /// <para>
        /// On the direct route (<c>Modify.Update</c>) SAM_Tas writes the TBD itself, so it resolves one shared
        /// <c>TBD.Construction</c> and one shared aperture <c>TBD.buildingElement</c> per DEFINITION as it
        /// goes. On the gbXML route it does not: TAS's own <c>T3DDocument.ExportNew</c> writes the TBD, from a
        /// T3D in which every aperture is its own <c>window</c> - it has to be, because the gbXML opening name
        /// carries the aperture GUID and <c>Query.UpdateT3D</c> decodes it back to find the SAM aperture. TAS
        /// therefore creates one aperture element and one construction PER APERTURE PER PART, named after that
        /// aperture. Nothing afterwards collapsed them; <c>Modify.UpdateBuildingElements</c> only ever SPLITS a
        /// diverged aperture off a shared element, never merges.
        /// </para>
        /// <para>
        /// This pass closes that gap by rebinding each physical surface onto the definition its SAM aperture
        /// actually states, and then sweeping up what TAS left behind. Twenty identical windows go from forty
        /// elements and forty constructions to two and two, while all forty physical <c>zoneSurface</c>s
        /// remain.
        /// </para>
        /// <para>
        /// <b>It creates no new rules.</b> Definition resolution is
        /// <see cref="ResolveApertureDefinition(Building, BuildingReuseCache, Aperture, AperturePart, MaterialLibrary, IDictionary{string, TBD.Construction}, IDictionary{string, buildingElement}, out TBD.Construction, out buildingElement, out ConstructionDefinition, out BuildingElementDefinition, out bool, out string)"/>,
        /// the very code the direct export runs. Physical resolution is Stage 3's
        /// <c>Query.AperturePhysicalIndex</c> and <c>Query.ApertureRebindKeys</c>, unchanged - so a
        /// two-sided aperture moves as one complete set or not at all, and a physical surface claimed by two
        /// apertures REFUSES rather than being guessed at.
        /// </para>
        /// <para>
        /// <b>Run it AFTER <c>Modify.UpdateIds</c>.</b> That is what makes it small: <c>UpdateIds</c> has
        /// already stamped every aperture's <c>Pane/FrameZoneSurfaceReference</c> and its current
        /// <c>Pane/FrameBuildingElementGuid</c>, so this pass reads which surfaces an aperture owns and which
        /// element they are currently on rather than re-deriving either from geometry. A later
        /// <c>UpdateIds</c> re-derives the same stamps from the actual bindings, so the order stays idempotent.
        /// </para>
        /// <para>
        /// <b>Physical instances are never merged.</b> <c>{ZoneGuid, SurfaceNumber}</c> remains physical
        /// identity and is not touched; <c>BuildingElementGuid</c> remains a reusable-definition binding that
        /// many apertures legitimately share, and is re-stamped to the definition each aperture now points at.
        /// </para>
        /// </summary>
        /// <param name="building">The open TBD building, as TAS's gbXML conversion produced it.</param>
        /// <param name="adjacencyCluster">The SAM model. Its apertures are re-stamped in place.</param>
        /// <param name="materialLibrary">The model's material library, for the construction content.</param>
        /// <param name="notes">What happened, one sentence each; problems carry <see cref="NotePrefix_Issue"/>.</param>
        /// <returns>True when the pass ran.</returns>
        public static bool UpdateApertureDefinitions(this Building building, AdjacencyCluster adjacencyCluster, MaterialLibrary materialLibrary, out List<string> notes)
        {
            notes = [];

            if (building == null || adjacencyCluster == null)
            {
                notes.Add(NotePrefix_Issue + "Aperture definitions: no TBD building or SAM adjacency cluster, so no aperture definition was reused.");
                return false;
            }

            List<Aperture> apertures = adjacencyCluster.GetApertures();
            if (apertures == null || apertures.Count == 0)
            {
                notes.Add("Aperture definitions: the model states no apertures, so there was nothing to reuse.");
                return true;
            }

            List<Guid> apertureGuids = apertures.ConvertAll(x => x.Guid);

            List<buildingElement> buildingElements = building.BuildingElements() ?? new List<buildingElement>();
            List<TBD.Construction> constructions = building.Constructions() ?? new List<TBD.Construction>();

            //The name namespaces, so a definition this pass creates can never be given a name something else
            //in the building already holds - including the panel constructions and elements.
            Dictionary<string, buildingElement> buildingElementsByName = new Dictionary<string, buildingElement>(buildingElements.Count);
            foreach (buildingElement buildingElement_Temp in buildingElements)
            {
                if (!string.IsNullOrEmpty(buildingElement_Temp?.name))
                {
                    buildingElementsByName[buildingElement_Temp.name] = buildingElement_Temp;
                }
            }

            Dictionary<string, TBD.Construction> constructionsByName = new Dictionary<string, TBD.Construction>(constructions.Count);
            foreach (TBD.Construction construction_Temp in constructions)
            {
                if (!string.IsNullOrEmpty(construction_Temp?.name))
                {
                    constructionsByName[construction_Temp.name] = construction_Temp;
                }
            }

            BuildingReuseCache buildingReuseCache = new BuildingReuseCache(building);

            // -----------------------------------------------------------------------------------------------
            // Nothing named after a physical aperture may be ADOPTED as a reusable definition. TAS's own
            // per-aperture constructions and elements can pass every content gate, so without this the cache
            // would hand one over and twenty windows would end up sharing a definition named after whichever
            // one of them was enumerated first. Their NAMES stay reserved, so a definition created below
            // cannot collide with one.
            // -----------------------------------------------------------------------------------------------
            List<string> names_InstanceNamed = Query.NamesContainingApertureGuid(buildingReuseCache.ConstructionNames().Concat(buildingReuseCache.BuildingElementNames()), apertureGuids);
            int count_RefusedSeeds = buildingReuseCache.RefuseSeededDefinitions(names_InstanceNamed);

            Dictionary<ZoneSurfaceKey, TBD.IZoneSurface> surfaceIndex = building.ZoneSurfaceIndex();

            //A COM-free mirror of the current bindings, advanced after each rebind so a later aperture in this
            //same pass sees the current state - and the input Query.ApertureRebindKeys validates against.
            Dictionary<ZoneSurfaceKey, string> surfaceBindings = surfaceIndex.ToDictionary(x => x.Key, x => x.Value?.buildingElement?.GUID);

            AperturePhysicalIndex aperturePhysicalIndex = Query.AperturePhysicalIndex(apertures);

            List<KeyValuePair<ZoneSurfaceKey, string>> ambiguities = aperturePhysicalIndex.Ambiguities();
            foreach (KeyValuePair<ZoneSurfaceKey, string> ambiguity in ambiguities)
            {
                notes.Add(NotePrefix_Issue + "Aperture definitions: " + ambiguity.Value);
            }

            //Every element this pass resolved an aperture onto. The sweep below never removes one of these,
            //even if a refusal has momentarily left it carrying no surface.
            HashSet<string> canonicalGuids = new HashSet<string>();

            int count_Parts = 0;
            int count_Rebound = 0;
            int count_AlreadyCanonical = 0;
            int count_NoStamp = 0;
            int count_RefusedRebind = 0;
            int count_RefusedResolve = 0;

            foreach (Aperture aperture in apertures)
            {
                if (aperture == null)
                {
                    continue;
                }

                //Read ONCE, before either part is rebound: the stamps this pass is about to change are the
                //very ones it reads which element each part is currently on.
                AperturePhysicalIdentity aperturePhysicalIdentity = aperture.AperturePhysicalIdentity();
                if (aperturePhysicalIdentity == null)
                {
                    continue;
                }

                //Frame before pane, the direct export's own ordering, so a name budget is consumed in the
                //same order on both routes.
                foreach (AperturePart aperturePart in new AperturePart[] { AperturePart.Frame, AperturePart.Pane })
                {
                    string buildingElementGuid_From = aperturePhysicalIdentity.BuildingElementGuid(aperturePart);
                    if (string.IsNullOrWhiteSpace(buildingElementGuid_From))
                    {
                        //This aperture states no such part in the TBD at all, or UpdateIds could not match
                        //it. Either way there is no binding to move and nothing to create.
                        count_NoStamp++;
                        continue;
                    }

                    count_Parts++;

                    //The COMPLETE physical set, validated before any definition is created, reserved or
                    //written - so a refusal creates no orphan and moves no surface.
                    List<ZoneSurfaceKey> rebindKeys = Query.ApertureRebindKeys(
                        aperturePhysicalIdentity,
                        aperturePart,
                        aperturePhysicalIndex,
                        surfaceBindings,
                        buildingElementGuid_From,
                        out string refusal_Rebind);

                    if (rebindKeys == null)
                    {
                        notes.Add(NotePrefix_Issue + string.Format("Aperture definitions: SAM aperture '{0}' ({1}) could not have its {2} rebound onto a shared definition; no definition was created and none of its surfaces moved - {3}",
                            aperture.Name, aperture.Guid, aperturePart, refusal_Rebind));
                        count_RefusedRebind++;
                        continue;
                    }

                    if (!building.ResolveApertureDefinition(
                        buildingReuseCache,
                        aperture,
                        aperturePart,
                        materialLibrary,
                        constructionsByName,
                        buildingElementsByName,
                        out TBD.Construction _,
                        out buildingElement buildingElement_Target,
                        out ConstructionDefinition _,
                        out BuildingElementDefinition _,
                        out bool _,
                        out string refusal_Resolve) || buildingElement_Target == null)
                    {
                        notes.Add(NotePrefix_Issue + string.Format("Aperture definitions: SAM aperture '{0}' ({1}) resolved no shared {2} definition, so it was left on the element TAS created for it - {3}",
                            aperture.Name, aperture.Guid, aperturePart, refusal_Resolve ?? "no reason was reported."));
                        count_RefusedResolve++;
                        continue;
                    }

                    canonicalGuids.Add(buildingElement_Target.GUID);

                    if (string.Equals(buildingElement_Target.GUID, buildingElementGuid_From, StringComparison.Ordinal))
                    {
                        //Already on the definition it asks for - a repeated run, or the aperture whose own
                        //element became the canonical one. Zero writes, exactly as a shared definition
                        //requires.
                        count_AlreadyCanonical++;
                        continue;
                    }

                    List<TBD.IZoneSurface> zoneSurfaces_ToRebind = rebindKeys.ConvertAll(x => surfaceIndex[x]);

                    for (int index = 0; index < zoneSurfaces_ToRebind.Count; index++)
                    {
                        zoneSurfaces_ToRebind[index].buildingElement = buildingElement_Target;
                        surfaceBindings[rebindKeys[index]] = buildingElement_Target.GUID;
                    }

                    //The definition binding follows the surfaces. The physical ZoneSurfaceReference stamps are
                    //NOT touched - they name an instance, and this pass moves no instance anywhere.
                    Aperture aperture_ToRestamp = adjacencyCluster.GetAperture(aperture.Guid, out Panel panel_ToRestamp);
                    if (aperture_ToRestamp == null || panel_ToRestamp == null)
                    {
                        notes.Add(NotePrefix_Issue + string.Format("Aperture definitions: SAM aperture '{0}' ({1}) had its {2} surfaces rebound but could not be found in its panel to re-stamp, so its BuildingElementGuid still names the element TAS created for it.",
                            aperture.Name, aperture.Guid, aperturePart));
                    }
                    else
                    {
                        ApertureParameter apertureParameter = aperturePart == AperturePart.Frame ? ApertureParameter.FrameBuildingElementGuid : ApertureParameter.PaneBuildingElementGuid;

                        aperture_ToRestamp.SetValue(apertureParameter, buildingElement_Target.GUID);
                        panel_ToRestamp.RemoveAperture(aperture_ToRestamp.Guid);
                        panel_ToRestamp.AddAperture(aperture_ToRestamp);
                        adjacencyCluster.AddObject(panel_ToRestamp);
                    }

                    count_Rebound++;
                }
            }

            // -----------------------------------------------------------------------------------------------
            // THE SWEEP. What TAS created per aperture now holds no surface. TBD has no RemoveBuildingElement,
            // so an orphan is marked and DeleteMarkedBuildingElements does the removal.
            // -----------------------------------------------------------------------------------------------
            Dictionary<string, int> surfaceCounts = new Dictionary<string, int>();
            foreach (KeyValuePair<ZoneSurfaceKey, string> surfaceBinding in surfaceBindings)
            {
                if (string.IsNullOrWhiteSpace(surfaceBinding.Value))
                {
                    continue;
                }

                surfaceCounts.TryGetValue(surfaceBinding.Value, out int count);
                surfaceCounts[surfaceBinding.Value] = count + 1;
            }

            List<buildingElement> buildingElements_Now = building.BuildingElements() ?? new List<buildingElement>();

            List<ApertureBuildingElementUsage> apertureBuildingElementUsages = new List<ApertureBuildingElementUsage>(buildingElements_Now.Count);
            List<string> guids_MarkedAlready = new List<string>();
            foreach (buildingElement buildingElement_Temp in buildingElements_Now)
            {
                if (buildingElement_Temp == null)
                {
                    continue;
                }

                if (buildingElement_Temp.markDelete != 0)
                {
                    guids_MarkedAlready.Add(buildingElement_Temp.GUID);
                }

                surfaceCounts.TryGetValue(buildingElement_Temp.GUID ?? string.Empty, out int count);
                apertureBuildingElementUsages.Add(new ApertureBuildingElementUsage(buildingElement_Temp.GUID, buildingElement_Temp.name, buildingElement_Temp.BEType, count));
            }

            if (guids_MarkedAlready.Count != 0)
            {
                notes.Add(NotePrefix_Issue + string.Format("Aperture definitions: {0} building element(s) were ALREADY marked for deletion by something else before this pass, and the sweep below deletes those too: {1}.",
                    guids_MarkedAlready.Count, string.Join(", ", guids_MarkedAlready)));
            }

            List<string> guids_ToDelete = Query.UnusedApertureBuildingElementGuids(apertureBuildingElementUsages, canonicalGuids);

            int count_Deleted = 0;
            if (guids_ToDelete.Count != 0)
            {
                HashSet<string> guids_ToDelete_Set = new HashSet<string>(guids_ToDelete);
                foreach (buildingElement buildingElement_Temp in buildingElements_Now)
                {
                    if (buildingElement_Temp != null && buildingElement_Temp.GUID != null && guids_ToDelete_Set.Contains(buildingElement_Temp.GUID))
                    {
                        buildingElement_Temp.markDelete = 1;
                    }
                }

                count_Deleted = building.DeleteMarkedBuildingElements();
            }

            //Constructions last, because which ones are still referenced can only be read once the elements
            //that referenced them are gone.
            List<string> names_Referenced = new List<string>();
            foreach (buildingElement buildingElement_Temp in building.BuildingElements() ?? new List<buildingElement>())
            {
                string name_Construction = buildingElement_Temp?.GetConstruction()?.name;
                if (!string.IsNullOrWhiteSpace(name_Construction))
                {
                    names_Referenced.Add(name_Construction);
                }
            }

            List<string> names_Construction = (building.Constructions() ?? new List<TBD.Construction>()).ConvertAll(x => x?.name);

            List<string> names_Orphan = Query.OrphanApertureConstructionNames(names_Construction, names_Referenced, apertureGuids, out List<string> names_UnreferencedKept);

            List<string> names_Removed = names_Orphan.Count == 0 ? new List<string>() : (RemoveConstructions(building, names_Orphan) ?? new List<string>());

            // -----------------------------------------------------------------------------------------------
            // The summary, at the front, so a reader sees what the pass achieved before the individual lines.
            // -----------------------------------------------------------------------------------------------
            List<string> notes_Summary = [];
            notes_Summary.Add(string.Format("Aperture definitions: {0} aperture part(s) considered; {1} rebound onto a shared definition, {2} already on one. {3} aperture building element(s) and {4} per-aperture construction(s) removed afterwards.",
                count_Parts, count_Rebound, count_AlreadyCanonical, count_Deleted, names_Removed.Count));

            if (count_RefusedSeeds != 0)
            {
                notes_Summary.Add(string.Format("Aperture definitions: {0} definition(s) already in the TBD are named after a physical aperture and so were not reused as shared definitions; definition-named ones were created instead.", count_RefusedSeeds));
            }

            if (count_NoStamp != 0)
            {
                notes_Summary.Add(string.Format("Aperture definitions: {0} aperture part(s) carry no building element stamp - they state no such part in the TBD, or Updating Ids did not match them - and were left alone.", count_NoStamp));
            }

            if (count_RefusedRebind != 0)
            {
                notes_Summary.Add(NotePrefix_Issue + string.Format("Aperture definitions: {0} aperture part(s) could not be rebound safely and were left on the element TAS created for them; the lines above name each one.", count_RefusedRebind));
            }

            if (count_RefusedResolve != 0)
            {
                notes_Summary.Add(NotePrefix_Issue + string.Format("Aperture definitions: {0} aperture part(s) resolved no shared definition at all; the lines above name each one.", count_RefusedResolve));
            }

            if (ambiguities.Count != 0)
            {
                notes_Summary.Add(NotePrefix_Issue + string.Format("Aperture definitions: {0} physical surface(s) are claimed by more than one SAM aperture; those apertures were not rebound rather than one of them being picked.", ambiguities.Count));
            }

            if (names_UnreferencedKept.Count != 0)
            {
                notes_Summary.Add(string.Format("Aperture definitions: {0} aperture construction(s) are referenced by nothing but name no physical aperture, so they were KEPT as library definitions: {1}.",
                    names_UnreferencedKept.Count, string.Join(", ", names_UnreferencedKept)));
            }

            if (guids_ToDelete.Count != count_Deleted)
            {
                notes_Summary.Add(NotePrefix_Issue + string.Format("Aperture definitions: {0} orphaned aperture building element(s) were marked for deletion but TAS reported {1} deleted; the difference is still in the TBD.", guids_ToDelete.Count, count_Deleted));
            }

            notes.InsertRange(0, notes_Summary);

            return true;
        }

        /// <summary>
        /// The same pass, taking the model rather than its parts.
        /// </summary>
        public static bool UpdateApertureDefinitions(this Building building, AnalyticalModel analyticalModel, out List<string> notes)
        {
            if (analyticalModel == null)
            {
                notes = [NotePrefix_Issue + "Aperture definitions: no SAM analytical model, so no aperture definition was reused."];
                return false;
            }

            return UpdateApertureDefinitions(building, analyticalModel.AdjacencyCluster, analyticalModel.MaterialLibrary, out notes);
        }
    }
}
