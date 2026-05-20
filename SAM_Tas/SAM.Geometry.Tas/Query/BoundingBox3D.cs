using SAM.Geometry.Spatial;

namespace SAM.Geometry.Tas
{
    public static partial class Query
    {
        // Walks the outer TBD polygon's points and returns the axis-aligned bounding box. Cheaper than
        // Convert.ToSAM(perimeter) when callers only need a spatial pre-filter and not the full Face3D
        // (no plane construction, no Face2D, holes are skipped because they cannot extend the AABB).
        public static SAM.Geometry.Spatial.BoundingBox3D BoundingBox3D(this TBD.Perimeter perimeter)
        {
            if (perimeter == null)
            {
                return null;
            }

            TBD.Polygon polygon = perimeter.GetFace();
            if (polygon == null)
            {
                return null;
            }

            return BoundingBox3D(polygon);
        }

        public static SAM.Geometry.Spatial.BoundingBox3D BoundingBox3D(this TBD.Polygon polygon)
        {
            if (polygon == null)
            {
                return null;
            }

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            bool hasAny = false;

            int index = 0;
            TBD.TasPoint tasPoint = polygon.GetPoint(index);
            while (tasPoint != null)
            {
                double x = tasPoint.x;
                double y = tasPoint.y;
                double z = tasPoint.z;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (z < minZ) minZ = z;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
                if (z > maxZ) maxZ = z;

                hasAny = true;
                index++;
                tasPoint = polygon.GetPoint(index);
            }

            if (!hasAny)
            {
                return null;
            }

            return new SAM.Geometry.Spatial.BoundingBox3D(new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ));
        }
    }
}
