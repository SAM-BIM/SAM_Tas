// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.InputEquivalence
{
    /// <summary>
    /// Independent geometric references of S07 section 4 and the T-FRAME writer rule of section 5. They are computed
    /// from raw polygons with SAM geometry; none of them calls a converter, a SAM_Tas writer or a stored
    /// <c>SpaceParameter</c>/<c>Calculated*</c> value (S07 section 3, independence rule).
    /// </summary>
    public static partial class Query
    {
        /// <summary>
        /// S07 1a: the sum of the authored floor-panel polygon areas. The caller selects the floor polygons from the
        /// frozen model; an empty set, a missing polygon or a degenerate one is S-INVALID, never a zero.
        /// </summary>
        public static ExpectedValue ExpectedFloorArea(IEnumerable<Polygon3D> floorPolygons)
        {
            List<Polygon3D> list = floorPolygons?.ToList();
            if (list == null || list.Count == 0)
            {
                return ExpectedValue.Invalid("no floor polygons");
            }

            double sum = 0;
            foreach (Polygon3D polygon in list)
            {
                double area = GrossArea(polygon);
                if (double.IsNaN(area) || area <= 0)
                {
                    return ExpectedValue.Invalid("a floor polygon is missing or degenerate");
                }

                sum += area;
            }

            return ExpectedValue.FromValue(sum);
        }

        /// <summary>S07 4 / 1b: the area of one planar polygon, NaN when it is missing or has fewer than three points.</summary>
        public static double GrossArea(Polygon3D polygon)
        {
            List<Point3D> points = polygon?.GetPoints();
            if (points == null || points.Count < 3)
            {
                return double.NaN;
            }

            return polygon.GetArea();
        }

        /// <summary>
        /// S07 1b: the area of a panel (or aperture) face, outer edge minus its holes. Whether TAS reports the gross
        /// or the net area is an unresolved native question (N-1), so both are computed and the caller picks the one
        /// fixed by the synthetic calibration, never by the fixture.
        /// </summary>
        public static ExpectedValue ExpectedNetArea(Polygon3D outer, IEnumerable<Polygon3D> holes)
        {
            double area = GrossArea(outer);
            if (double.IsNaN(area) || area <= 0)
            {
                return ExpectedValue.Invalid("the outer polygon is missing or degenerate");
            }

            foreach (Polygon3D hole in holes ?? Enumerable.Empty<Polygon3D>())
            {
                double holeArea = GrossArea(hole);
                if (double.IsNaN(holeArea))
                {
                    return ExpectedValue.Invalid("a hole polygon is missing or degenerate");
                }

                area -= holeArea;
            }

            return area > 0 ? ExpectedValue.FromValue(area) : ExpectedValue.Invalid("the holes remove the whole face");
        }

        /// <summary>
        /// S07 2: the volume of the closed shell of the space's authored panel polygons (<c>shell.Volume()</c>), not the
        /// stored <c>SpaceParameter.Volume</c>. An open or degenerate shell is S-INVALID, never a zero.
        /// </summary>
        public static ExpectedValue ExpectedShellVolume(IEnumerable<Polygon3D> panelPolygons)
        {
            List<Polygon3D> list = panelPolygons?.ToList();
            if (list == null || list.Count < 4)
            {
                return ExpectedValue.Invalid("a closed shell needs at least four polygons");
            }

            List<Face3D> faces = new List<Face3D>();
            foreach (Polygon3D polygon in list)
            {
                if (polygon == null || polygon.GetPoints() == null)
                {
                    return ExpectedValue.Invalid("a panel polygon is missing or degenerate");
                }

                faces.Add(new Face3D(polygon));
            }

            Shell shell = new Shell(faces);
            if (!shell.IsClosed(Core.Tolerance.MacroDistance))
            {
                return ExpectedValue.Invalid("the shell of the authored panel polygons is not closed");
            }

            double volume = shell.Volume();
            if (double.IsNaN(volume) || double.IsInfinity(volume) || volume <= 0)
            {
                return ExpectedValue.Invalid("the shell volume is not positive");
            }

            return ExpectedValue.FromValue(volume);
        }

        /// <summary>
        /// T-FRAME, writer rule (S07 section 5): the frame percentage is 100 x A_frame / (A_frame + A_pane) from the
        /// net face areas; 0 when the frame area is 0 or NaN, otherwise 100 when the pane area is 0 or NaN (the order of
        /// <c>Aperture.GetFrameFactor</c>, which is a cross-check only). The rounding to one decimal happens only with
        /// <c>SharedWindowTypes</c>, which the contract route does not set.
        /// </summary>
        public static ExpectedValue ExpectedFramePercentage(double frameArea, double paneArea)
        {
            if (double.IsInfinity(frameArea) || double.IsInfinity(paneArea) || frameArea < 0 || paneArea < 0)
            {
                return ExpectedValue.Invalid("a frame or pane area is negative or infinite");
            }

            if (double.IsNaN(frameArea) || frameArea == 0)
            {
                return ExpectedValue.Zero();
            }

            if (double.IsNaN(paneArea) || paneArea == 0)
            {
                return ExpectedValue.FromValue(100);
            }

            return ExpectedValue.FromValue(100 * frameArea / (frameArea + paneArea));
        }

        /// <summary>
        /// T-FRAME, writer rule: the written frame width is the frame layer thickness sum when it is not NaN (0 when the
        /// aperture has no frame layers), else <c>DefaultFrameWidth</c> when not NaN, else none (S-UNSPEC).
        /// </summary>
        public static ExpectedValue ExpectedFrameWidth(double frameLayerThicknessSum, double defaultFrameWidth)
        {
            double width = !double.IsNaN(frameLayerThicknessSum) ? frameLayerThicknessSum : defaultFrameWidth;
            if (double.IsNaN(width))
            {
                return ExpectedValue.Unspecified("neither a frame thickness nor a default frame width is stated");
            }

            return width < 0 || double.IsInfinity(width) ? ExpectedValue.Invalid("the frame width is negative or infinite") : ExpectedValue.FromValue(width);
        }
    }
}
