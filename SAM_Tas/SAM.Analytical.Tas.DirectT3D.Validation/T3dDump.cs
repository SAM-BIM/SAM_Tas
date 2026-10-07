// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TAS3D;

namespace SAM.Analytical.Tas.DirectT3D.Validation
{
    /// <summary>
    /// Every property of the building, elements, windows and zones of a .t3d, as text - what TAS actually holds, read back
    /// through the COM accessors - so two routes' T3D files can be compared attribute by attribute.
    /// </summary>
    public static class T3dDump
    {
        private static string Safe<T>(Func<T> read)
        {
            try { return System.Convert.ToString(read(), CultureInfo.InvariantCulture); }
            catch (Exception) { return "?"; }
        }

        public static int Run(string[] args)
        {
            // t3d <file.t3d> [out.txt]
            CultureInfo ci = CultureInfo.InvariantCulture;
            StringBuilder sb = new StringBuilder();

            using (SAMT3DDocument sAMT3DDocument = new SAMT3DDocument(args[1]))
            {
                TAS3D.Building b = sAMT3DDocument.T3DDocument.Building;
                sb.AppendLine(string.Format(ci, "BUILDING name='{0}' north={1} lat={2} lon={3} tz={4} year={5}", b.name, b.northAngle, b.latitude, b.longitude, b.timeZone, b.year));

                for (int i = 1; ; i++)
                {
                    TAS3D.Element e = b.GetElement(i);
                    if (e == null) break;
                    sb.AppendLine(string.Format(ci, "ELEMENT '{0}' width={1:F4} colour={2:X6} BEType={3} ground={4} ghost={5} transparent={6} internalShadows={7} zoneFloorArea={8} preset={9} secondaryProportion={10} used={11}",
                        e.name, e.width, e.colour, e.BEType, e.ground, e.ghost, e.transparent, e.internalShadows, e.zoneFloorArea, e.isPreset, e.secondaryProportion, e.isUsed));
                }

                List<string> windows = new List<string>();
                for (int i = 1; ; i++)
                {
                    TAS3D.window w = b.GetWindow(i);
                    if (w == null) break;
                    windows.Add(string.Format(ci, "WINDOW '{0}' positionType={1} colour={2} transparent={3} internalShadows={4} framePerc={5} isPercFrame={6} frameWidth={7} frameDepth={8} paneDepth={9} paneOffset={10} height={11} width={12} level={13} used={14}",
                        w.name, Safe(() => w.positionType), Safe(() => w.colour.ToString("X6")), Safe(() => w.transparent), Safe(() => w.internalShadows), Safe(() => w.framePerc.ToString("F4", ci)), Safe(() => w.isPercFrame), Safe(() => w.frameWidth.ToString("F4", ci)), Safe(() => w.frameDepth.ToString("F4", ci)), Safe(() => w.paneDepth.ToString("F4", ci)), Safe(() => w.paneOffset.ToString("F4", ci)), Safe(() => w.height.ToString("F3", ci)), Safe(() => w.width.ToString("F3", ci)), Safe(() => w.level.ToString("F3", ci)), Safe(() => w.isUsed)));
                }

                // Names carry the aperture GUID on one route and a counter on the other; the attributes are what is compared.
                foreach (string line in windows) sb.AppendLine(line);

                for (int i = 1; ; i++)
                {
                    TAS3D.Zone z = b.GetZone(i);
                    if (z == null) break;
                    if (z.isUsed == 0) continue;
                    sb.AppendLine(string.Format(ci, "ZONE '{0}' external={1} colour={2:X6} floorArea={3:F4} volume={4:F4} desc='{5}'", z.name, z.external, z.colour, z.floorArea, z.volume, z.description));
                }
            }

            Console.Write(sb.ToString());
            if (args.Length > 2) File.WriteAllText(args[2], sb.ToString());
            return 0;
        }
    }
}
