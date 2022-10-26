using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using Xbim.Ifc2x3.Extensions;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.ProfileResource;

namespace ThBIMServer.NTS
{
    public static class ThIFCNTSExtension
    {
        public static Geometry ToNTSGeometry(this IfcProfileDef profile)
        {
            if (profile is IfcArbitraryClosedProfileDef closedProfile)
            {
                return closedProfile.ToNTSPolygon();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static Polygon ToNTSPolygon(this IfcProfileDef profile)
        {
            if (profile is IfcArbitraryClosedProfileDef closedProfile)
            {
                return (closedProfile.OuterCurve as IfcPolyline).ToNTSPolygon();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static Polygon ToNTSPolygon(this IfcPolyline polyline)
        {
            //var pts = polyline.Points;
            if (polyline.Area() < 1e-6)
            {
                return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon();
            }
            var geometry = polyline.ToNTSLineString();
            if (geometry is LinearRing ring)
            {
                return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon(ring);
            }
            else
            {
                return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon();
            }
        }

        public static LineString ToNTSLineString(this IfcPolyline polyline)
        {
            var points = new List<Coordinate>();
            //var arcLength = ThCADCoreNTSService.Instance.ArcTessellationLength;
            //var polyLine = poly.HasBulges ? poly.TessellatePolylineWithArc(arcLength) : poly;
            for (int i = 0; i < polyline.Points.Count; i++)
            {
                points.Add(polyline.Points[i].ToNTSCoordinate());
            }

            // 支持真实闭合或视觉闭合
            // 对于处于“闭合”状态的多段线，要保证其首尾点一致
            if (points[0].Equals2D(points[points.Count - 1], ThIFCNTSService.Instance.AcadGlobalTolerance))
            {
                if (points.Count > 1)
                {
                    points.RemoveAt(points.Count - 1);
                    points.Add(points[0]);
                }
            }
            else
            {
                //if (polyline.Closed)
                //{
                //    points.Add(points[0]);
                //}
            }

            if (points[0].Equals(points[points.Count - 1]))
            {
                // 三个点，其中起点和终点重合
                // 多段线退化成一根线段
                if (points.Count == 3)
                {
                    return ThIFCNTSService.Instance.GeometryFactory.CreateLineString(points.ToArray());
                }

                // 二个点，其中起点和终点重合
                // 多段线退化成一个点
                if (points.Count == 2)
                {
                    return ThIFCNTSService.Instance.GeometryFactory.CreateLineString();
                }

                // 一个点
                // 多段线退化成一个点
                if (points.Count == 1)
                {
                    return ThIFCNTSService.Instance.GeometryFactory.CreateLineString();
                }

                // 首尾端点一致的情况
                // LinearRings are the fundamental building block for Polygons.
                // LinearRings may not be degenerate; that is, a LinearRing must have at least 3 points.
                // Other non-degeneracy criteria are implied by the requirement that LinearRings be simple. 
                // For instance, not all the points may be collinear, and the ring may not self - intersect.
                return ThIFCNTSService.Instance.GeometryFactory.CreateLinearRing(points.ToArray());
            }
            else
            {
                // 首尾端点不一致的情况
                return ThIFCNTSService.Instance.GeometryFactory.CreateLineString(points.ToArray());
            }
        }

        public static Coordinate ToNTSCoordinate(this IfcCartesianPoint point)
        {
            return new Coordinate(
                ThIFCNTSService.Instance.PrecisionModel.MakePrecise(point.X),
                ThIFCNTSService.Instance.PrecisionModel.MakePrecise(point.Y));
        }
    }
}
