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
                    continue;

                TBD.BuildingElementType buildingElementType = (TBD.BuildingElementType)buildingElement.BEType;
                if(buildingElementType == TBD.BuildingElementType.GLAZING || buildingElementType == TBD.BuildingElementType.FRAMEELEMENT)
                {
                    Aperture aperture = null;
                    if(Query.UniqueNameDecomposition(buildingElement.name, out string prefix, out string name_Temp, out System.Guid? guid, out int id) && guid != null && guid.HasValue)
                    {
                        aperture = analyticalModel.AdjacencyCluster.GetAperture(guid.Value);
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
                            List<TBD.ApertureType> apertureTypes = SetApertureTypes(building, buildingElement, openingProperties);
                        }

                        if (aperture.TryGetValue(Analytical.ApertureParameter.FeatureShade, out FeatureShade featureShade))
                        {
                            List<TBD.FeatureShade> featureShades = SetFeatureShades(building, buildingElement, featureShade);
                        }
                    }
                }



                buildingElement.AssignConstruction(construction);
            }

            return true;
        }
    }
}