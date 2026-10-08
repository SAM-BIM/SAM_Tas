// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Pr7aProbe
{
    /// <summary>
    /// Risk R1: how a glazing construction's g-value (GetGlazingValues()[5]) can be reached. Works on a COPY of a TBD;
    /// nothing is saved unless "save" is given.
    /// </summary>
    internal static class Glazing
    {
        public static void Run(string tbd, string constructionName, double target, bool save)
        {
            TBD.TBDDocument document = new TBD.TBDDocument();
            try
            {
                document.open(tbd);
                TBD.Building building = document.Building;

                // How many constructions does GetConstruction(i) reach, and is the named one among them?
                int reached = 0;
                bool listed = false;
                for (int i = 0; i < 200; i++)
                {
                    TBD.Construction c = building.GetConstruction(i);
                    if (c == null)
                    {
                        continue;
                    }

                    reached++;
                    listed |= c.name == constructionName;
                }

                TBD.Construction construction = building.GetConstructionByName(constructionName);
                Console.WriteLine(string.Format("GetConstruction(0..199) non-null={0} listed={1} GetConstructionByName={2}", reached, listed, construction != null));
                if (construction == null)
                {
                    return;
                }

                Console.WriteLine("type=" + construction.type + " before=" + Values(construction));

                List<TBD.material> layers = new List<TBD.material>();
                for (int j = 1; j < 50; j++)
                {
                    TBD.material material;
                    try
                    {
                        material = construction.materials(j);
                    }
                    catch
                    {
                        break;
                    }

                    if (material == null)
                    {
                        break;
                    }

                    layers.Add(material);
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "layer[{0}] name='{1}' type={2} width={3} tau={4} rhoExt={5} rhoInt={6} epsExt={7} epsInt={8} light={9}",
                        j, material.name, (TBD.MaterialTypes)material.type, Program.R(material.width), Program.R(material.solarTransmittance), Program.R(material.externalSolarReflectance),
                        Program.R(material.internalSolarReflectance), Program.R(material.externalEmissivity), Program.R(material.internalEmissivity), Program.R(material.lightTransmittance)));
                }

                List<int> panes = Enumerable.Range(0, layers.Count).Where(j => (TBD.MaterialTypes)layers[j].type == TBD.MaterialTypes.tcdTransparentLayer).ToList();

                // 1. Sensitivity and reachable range per transparent layer (the others unchanged), restored afterwards.
                foreach (int j in panes)
                {
                    TBD.material pane = layers[j];
                    float tau0 = pane.solarTransmittance;
                    float max = 1 - Math.Max(pane.externalSolarReflectance, pane.internalSolarReflectance);
                    List<string> cells = new List<string>();
                    foreach (float tau in new float[] { 0f, 0.25f * max, 0.5f * max, 0.75f * max, max })
                    {
                        pane.solarTransmittance = tau;
                        float[] v = Program.Floats(construction.GetGlazingValues());
                        cells.Add(string.Format(CultureInfo.InvariantCulture, "tau {0:0.000} -> g {1:0.0000} T {2:0.0000} LT {3:0.0000}", tau, v[5], v[2], v[0]));
                    }

                    pane.solarTransmittance = tau0;
                    Console.WriteLine(string.Format("scan layer[{0}] '{1}': {2}", j + 1, pane.name, string.Join("; ", cells)));
                }

                Console.WriteLine("restored=" + Values(construction));

                // 2. Bisection on each transparent layer's solar transmittance; twice, to show it repeats bit for bit.
                foreach (int j in panes)
                {
                    TBD.material pane = layers[j];
                    float tau0 = pane.solarTransmittance;
                    for (int repeat = 1; repeat <= 2; repeat++)
                    {
                        pane.solarTransmittance = tau0;
                        double low = 0;
                        double high = 1 - Math.Max(pane.externalSolarReflectance, pane.internalSolarReflectance);
                        double g = double.NaN;
                        int iterations = 0;
                        for (; iterations < 40; iterations++)
                        {
                            double mid = (low + high) / 2;
                            pane.solarTransmittance = (float)mid;
                            g = Program.Floats(construction.GetGlazingValues())[5];
                            if (Math.Abs(g - target) <= 1e-5)
                            {
                                break;
                            }

                            if (g < target)
                            {
                                low = mid;
                            }
                            else
                            {
                                high = mid;
                            }
                        }

                        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "bisect layer[{0}] repeat {1}: target={2} iterations={3} tau={4} g={5} values={6}",
                            j + 1, repeat, Program.R(target), iterations, Program.R(pane.solarTransmittance), Program.R(g), Values(construction)));
                    }

                    pane.solarTransmittance = tau0;
                }

                // 3. TBD's own search: UValueSearch(flowDirection, targetU, targetG, material, property, standard).
                foreach (int j in panes)
                {
                    TBD.material pane = layers[j];
                    float tau0 = pane.solarTransmittance;
                    foreach (int flow in new[] { 0, 1 })
                    {
                        foreach (int standard in new[] { 0, 1 })
                        {
                            pane.solarTransmittance = tau0;
                            try
                            {
                                int code = construction.UValueSearch(flow, 0f, (float)target, pane, (int)TBD.MaterialProperties.tcdSolarTransmittance, standard);
                                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "UValueSearch layer[{0}] flow={1} standard={2}: code={3} tau={4} values={5}",
                                    j + 1, flow, standard, code, Program.R(pane.solarTransmittance), Values(construction)));
                            }
                            catch (Exception exception)
                            {
                                Console.WriteLine(string.Format("UValueSearch layer[{0}] flow={1} standard={2}: {3}", j + 1, flow, standard, exception.Message));
                            }
                        }
                    }

                    pane.solarTransmittance = tau0;
                }

                Console.WriteLine("final=" + Values(construction));
                if (save)
                {
                    document.save();
                    Console.WriteLine("saved");
                }
            }
            finally
            {
                document.close();
                Program.Release(document);
            }
        }

        internal static string Values(TBD.Construction construction)
        {
            return "[" + string.Join(",", Program.Floats(construction.GetGlazingValues()).Select(x => Program.R(x))) + "]";
        }
    }
}
