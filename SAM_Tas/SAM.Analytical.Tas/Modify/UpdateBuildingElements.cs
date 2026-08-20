// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System.Collections.Generic;
using System.Linq;
using TBD;

namespace SAM.Analytical.Tas
{
    public static partial class Modify
    {
        public static bool UpdateBuildingElements(this string path_TBD, AnalyticalModel analyticalModel)
        {
            if (string.IsNullOrWhiteSpace(path_TBD))
                return false;

            bool result = false;

            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
            {
                result = UpdateBuildingElements(sAMTBDDocument, analyticalModel);
                if (result)
                    sAMTBDDocument.Save();
            }

            return result;
        }

        public static bool UpdateBuildingElements(this SAMTBDDocument sAMTBDDocument, AnalyticalModel analyticalModel)
        {
            if (sAMTBDDocument == null)
                return false;

            return UpdateBuildingElements(sAMTBDDocument.TBDDocument, analyticalModel);
        }

        public static bool UpdateBuildingElements(this TBDDocument tBDDocument, AnalyticalModel analyticalModel)
        {
            return UpdateBuildingElements(tBDDocument, analyticalModel, out List<string> _);
        }

        /// <summary>
        /// The same update, reporting the aperture-type refusals the write would otherwise drop silently.
        /// <para>
        /// <b>This is the FIRST of the two aperture-control write paths</b>, and the one that matters most
        /// to a Part O diagnosis: it identifies the SAM aperture from the GUID encoded in the TBD building
        /// element's own name, not from geometry, so it reaches apertures the later geometric
        /// <see cref="SetApertureTypes(Building, AdjacencyCluster, out List{string}, double)"/> step can
        /// miss entirely. A schedule written here is already on the aperture type by the time that step
        /// runs, which then reuses it by value. Knowing which of the two paths wrote a schedule - or that
        /// neither did - is the point of reporting from both.
        /// </para>
        /// </summary>
        public static bool UpdateBuildingElements(this TBDDocument tBDDocument, AnalyticalModel analyticalModel, out List<string> notes)
        {
            notes = [];

            int count_ScheduleRequested = 0;
            int count_ScheduleWritten = 0;
            int count_GlazingWithoutConstruction = 0;
            int count_GlazingWithoutAperture = 0;

            if (tBDDocument == null || analyticalModel == null)
                return false;

            Building building = tBDDocument.Building;
            if (building == null)
                return false;

            //RemoveConstructions(building); //Added 05.06.2024 -> Requested By Michal D. to clean existing consructions from TBD file

            UpdateConstructions(tBDDocument, analyticalModel);

            List<buildingElement> buildingElements = building.BuildingElements();
            if (buildingElements == null || buildingElements.Count == 0)
                return false;

            List<TBD.Construction> constructions = building.Constructions();
            if (constructions == null || constructions.Count == 0)
                return false;

            // Index constructions once so the per-element loop avoids three O(N) scans + a worst-case O(N*W) word match.
            Dictionary<string, TBD.Construction> constructionByName = new Dictionary<string, TBD.Construction>(constructions.Count);
            List<KeyValuePair<TBD.Construction, HashSet<string>>> constructionWordSets = new List<KeyValuePair<TBD.Construction, HashSet<string>>>(constructions.Count);
            foreach (TBD.Construction c in constructions)
            {
                if (string.IsNullOrWhiteSpace(c?.name))
                    continue;
                constructionByName[c.name] = c;
                HashSet<string> wordSet = new HashSet<string>();
                foreach (string w in c.name.Split(' '))
                {
                    if (!string.IsNullOrWhiteSpace(w))
                        wordSet.Add(w);
                }
                if (wordSet.Count > 0)
                    constructionWordSets.Add(new KeyValuePair<TBD.Construction, HashSet<string>>(c, wordSet));
            }

            foreach (buildingElement buildingElement in buildingElements)
            {
                string name = Query.Name(buildingElement);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                TBD.Construction construction;
                if (!constructionByName.TryGetValue(name, out construction))
                {
                    List<TBD.Construction> constructions_Temp = constructions.FindAll(x => !string.IsNullOrWhiteSpace(x?.name) && name.EndsWith(x.name));
                    if(constructions_Temp == null || constructions_Temp.Count == 0)
                    {
                        constructions_Temp = constructions.FindAll(x => !string.IsNullOrWhiteSpace(x?.name) && x.name.EndsWith(name));
                    }

                    if(constructions_Temp != null && constructions_Temp.Count != 0)
                    {
                        constructions_Temp.Sort((x, y) => System.Math.Abs(x.name.Length - name.Length).CompareTo(System.Math.Abs(y.name.Length - name.Length)));
                        construction = constructions_Temp.First();
                    }
                }

                if(construction == null)
                {
                    HashSet<string> elementWords = new HashSet<string>();
                    foreach (string w in name.Split(' '))
                    {
                        if (!string.IsNullOrWhiteSpace(w))
                            elementWords.Add(w);
                    }

                    if (elementWords.Count != 0)
                    {
                        foreach (KeyValuePair<TBD.Construction, HashSet<string>> entry in constructionWordSets)
                        {
                            // Original behaviour: construction matches when every word of the construction's name
                            // appears in the element's name (i.e. constructionWords is a subset of elementWords).
                            if (entry.Value.IsSubsetOf(elementWords))
                            {
                                construction = entry.Key;
                                break;
                            }
                        }
                    }

                }
                    
                if (construction == null)
                {
                    //A glazing element that never finds a construction leaves this loop before the aperture
                    //block below, so it silently receives no aperture control either - a Part O schedule on
                    //that aperture would never be written on this path at all.
                    if ((TBD.BuildingElementType)buildingElement.BEType == TBD.BuildingElementType.GLAZING)
                    {
                        count_GlazingWithoutConstruction++;
                    }

                    continue;
                }

                TBD.BuildingElementType buildingElementType = (TBD.BuildingElementType)buildingElement.BEType;
                if(buildingElementType == TBD.BuildingElementType.GLAZING || buildingElementType == TBD.BuildingElementType.FRAMEELEMENT)
                {
                    Aperture aperture = null;
                    if(Query.UniqueNameDecomposition(buildingElement.name, out string prefix, out string name_Temp, out System.Guid? guid, out int id) && guid != null && guid.HasValue)
                    {
                        aperture = analyticalModel.AdjacencyCluster.GetAperture(guid.Value);
                    }

                    if (aperture == null && buildingElementType == TBD.BuildingElementType.GLAZING)
                    {
                        //The name did not decode to a GUID, or it did and the SAM model has no such
                        //aperture. Either way this pane gets no aperture control from this path.
                        count_GlazingWithoutAperture++;
                    }

                    AperturePart aperturePart = AperturePart.Undefined;
                    switch (buildingElementType)
                    {
                        case TBD.BuildingElementType.GLAZING:
                            aperturePart = AperturePart.Pane;
                            break;

                        case TBD.BuildingElementType.FRAMEELEMENT:
                            aperturePart = AperturePart.Frame;
                            break;
                    }

                    if(aperturePart != AperturePart.Undefined)
                    {
                        if (aperture != null)
                        {
                            buildingElement.SetColor(aperture, aperturePart);
                        }
                        else
                        {
                            buildingElement.colour = Core.Convert.ToUint(Analytical.Query.Color(ApertureType.Window, aperturePart));
                        }
                    }

                    if(aperture != null && aperturePart == AperturePart.Pane)
                    {
                        if(aperture.TryGetValue(Analytical.ApertureParameter.OpeningProperties, out IOpeningProperties openingProperties))
                        {
                            List<TBD.ApertureType> apertureTypes = SetApertureTypes(building, buildingElement, openingProperties, out List<string> notes_Temp);
                            notes.AddRange(notes_Temp ?? []);

                            //Only an aperture that states an availability schedule is tracked here, and only
                            //one that did NOT end up carrying a schedule is narrated. A successful write
                            //contributes to the counters and says nothing, so an ordinary run does not put
                            //one remark on the canvas per window.
                            if (TryDescribeScheduleRequest(openingProperties, out string description_Request))
                            {
                                count_ScheduleRequested++;

                                TBD.ApertureType apertureType = apertureTypes == null || apertureTypes.Count == 0 ? null : apertureTypes[0];
                                if (ApertureTypeSchedule(apertureType) != null)
                                {
                                    count_ScheduleWritten++;
                                }
                                else
                                {
                                    notes.Add(Modify.NotePrefix_Issue + string.Format("Building elements: TBD building element '{0}' ({1}) resolved SAM aperture '{2}' ({3}) by GUID; {4}; requested {5}; but {6}",
                                        buildingElement.name,
                                        buildingElement.GUID,
                                        aperture.Name,
                                        aperture.Guid,
                                        DescribeOpeningProperties(openingProperties),
                                        description_Request,
                                        apertureType == null ? "NO aperture type came back from the write - the refusal reported alongside names the reason." : string.Format("aperture type '{0}' carries no schedule afterwards - it read back as {1}", apertureType.name, DescribeApertureTypeProfile(apertureType) ?? "NO PROFILE - the aperture type came back without a TBD profile to read.")));
                                }
                            }
                        }

                        if (aperture.TryGetValue(Analytical.ApertureParameter.FeatureShade, out FeatureShade featureShade))
                        {
                            List<TBD.FeatureShade> featureShades = SetFeatureShades(building, buildingElement, featureShade);
                        }
                    }
                }



                buildingElement.AssignConstruction(construction);
            }

            //Summarised at the front, so a reader sees what this path achieved before the individual lines.
            List<string> notes_Summary = [];
            notes_Summary.Add(string.Format("Building elements: {0} aperture(s) requested an availability schedule on the GUID-matched path, {1} of those read a schedule back off the TBD profile.", count_ScheduleRequested, count_ScheduleWritten));

            if (count_ScheduleRequested != count_ScheduleWritten)
            {
                notes_Summary.Add(Modify.NotePrefix_Issue + string.Format("Building elements: {0} of {1} apertures that requested an availability schedule did NOT read one back off the TBD on this path.", count_ScheduleRequested - count_ScheduleWritten, count_ScheduleRequested));
            }

            if (count_GlazingWithoutConstruction != 0)
            {
                notes_Summary.Add(Modify.NotePrefix_Issue + string.Format("Building elements: {0} GLAZING element(s) matched no construction and were skipped before any aperture control was written for them.", count_GlazingWithoutConstruction));
            }

            if (count_GlazingWithoutAperture != 0)
            {
                notes_Summary.Add(Modify.NotePrefix_Issue + string.Format("Building elements: {0} GLAZING element(s) did not resolve to a SAM aperture from their own name, so no aperture control was written for them on this path.", count_GlazingWithoutAperture));
            }

            notes.InsertRange(0, notes_Summary);

            return true;
        }
    }
}