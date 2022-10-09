using NetTopologySuite;
using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace ThBIMServer.Geometry
{
    public static class ThNTSExtension
    {
        private static PrecisionModel PM = NtsGeometryServices.Instance.DefaultPrecisionModel;
        private static GeometryFactory GF = NtsGeometryServices.Instance.CreateGeometryFactory();

        private static Coordinate ToCoordinate(this ThTCHPoint3d point)
        {
            return new Coordinate(PM.MakePrecise(point.X), PM.MakePrecise(point.Y));
        }

        public static LineString ToNTSLineString(this ThTCHPolyline polyline)
        {
            var points = new List<Coordinate>();
            var pts = polyline.Points;
            foreach (var segment in polyline.Segments)
            {
                if (segment.Index.Count == 2)
                {
                    //直线段
                    var startPt = pts[(int)segment.Index[0]];
                    var endPt = pts[(int)segment.Index[1]];
                    points.Add(ToCoordinate(startPt));
                    points.Add(ToCoordinate(endPt));
                }
                else
                {
                    //圆弧段
                    var startPt = pts[(int)segment.Index[0]];
                    var midPt = pts[(int)segment.Index[1]];
                    var endPt = pts[(int)segment.Index[2]];
                    points.Add(ToCoordinate(startPt));
                    points.Add(ToCoordinate(midPt));
                    points.Add(ToCoordinate(endPt));
                }
            }

            // 支持真实闭合或视觉闭合
            // 对于处于“闭合”状态的多段线，要保证其首尾点一致
            if (points[0].Equals2D(points[points.Count - 1], 1e-8))
            {
                if (points.Count > 1)
                {
                    points.RemoveAt(points.Count - 1);
                    points.Add(points[0]);
                }
            }
            else
            {
                if (polyline.IsClosed)
                {
                    points.Add(points[0]);
                }
            }

            if (points[0].Equals(points[points.Count - 1]))
            {
                // 三个点，其中起点和终点重合
                // 多段线退化成一根线段
                if (points.Count == 3)
                {
                    return GF.CreateLineString(points.ToArray());
                }

                // 二个点，其中起点和终点重合
                // 多段线退化成一个点
                if (points.Count == 2)
                {
                    return GF.CreateLineString();
                }

                // 一个点
                // 多段线退化成一个点
                if (points.Count == 1)
                {
                    return GF.CreateLineString();
                }

                // 首尾端点一致的情况
                // LinearRings are the fundamental building block for Polygons.
                // LinearRings may not be degenerate; that is, a LinearRing must have at least 3 points.
                // Other non-degeneracy criteria are implied by the requirement that LinearRings be simple. 
                // For instance, not all the points may be collinear, and the ring may not self - intersect.
                return GF.CreateLinearRing(points.ToArray());
            }
            else
            {
                // 首尾端点不一致的情况
                return GF.CreateLineString(points.ToArray());
            }
        }
    }
}
