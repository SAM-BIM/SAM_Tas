// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Spatial;
using System.Collections.Generic;

namespace SAM.Analytical.InputEquivalence.Tests
{
    internal static class TestData
    {
        public const string Fixture = "fixture-1";

        /// <summary>Six polygons of an axis-aligned box. Polygons are built from points only, so no SAM settings are touched.</summary>
        public static List<Polygon3D> Box(double x, double y, double z, double ox = 0, double oy = 0, double oz = 0)
        {
            Point3D p000 = new Point3D(ox, oy, oz);
            Point3D p100 = new Point3D(ox + x, oy, oz);
            Point3D p110 = new Point3D(ox + x, oy + y, oz);
            Point3D p010 = new Point3D(ox, oy + y, oz);
            Point3D p001 = new Point3D(ox, oy, oz + z);
            Point3D p101 = new Point3D(ox + x, oy, oz + z);
            Point3D p111 = new Point3D(ox + x, oy + y, oz + z);
            Point3D p011 = new Point3D(ox, oy + y, oz + z);

            return new List<Polygon3D>
            {
                new Polygon3D(new[] { p000, p100, p110, p010 }), // floor
                new Polygon3D(new[] { p001, p101, p111, p011 }), // roof
                new Polygon3D(new[] { p000, p100, p101, p001 }), // y = 0
                new Polygon3D(new[] { p010, p110, p111, p011 }), // y = max
                new Polygon3D(new[] { p000, p010, p011, p001 }), // x = 0
                new Polygon3D(new[] { p100, p110, p111, p101 })  // x = max
            };
        }

        public static Polygon3D Rectangle(double x, double y, double ox = 0, double oy = 0, double oz = 0)
        {
            return new Polygon3D(new[] { new Point3D(ox, oy, oz), new Point3D(ox + x, oy, oz), new Point3D(ox + x, oy + y, oz), new Point3D(ox, oy + y, oz) });
        }

        public static QuantityInput Input(string quantityId, ExpectedValue expected, ReaderResult native, IdentityStatus identity = IdentityStatus.Verified, AlignmentStatus alignment = AlignmentStatus.Verified)
        {
            return new QuantityInput
            {
                Definition = QuantityCatalog.Get(quantityId),
                FixtureId = Fixture,
                ObjectKey = "object",
                Expected = expected,
                Native = native,
                Identity = identity,
                Alignment = alignment
            };
        }
    }
}
