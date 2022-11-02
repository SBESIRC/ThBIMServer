using System;
using System.Collections.Generic;

using NetTopologySuite.Geometries;
using NetTopologySuite.Mathematics;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.ProfileResource;

namespace ThBIMServer.NTS
{
    public static class ThIFCNTSExtension
    {
        public static Geometry ToNTSGeometry(this IfcProfileDef profile, IfcAxis2Placement placement)
        {
            if (profile is IfcArbitraryClosedProfileDef closedProfile)
            {
                return closedProfile.ToNTSPolygon(placement);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static Polygon ToNTSPolygon(this IfcProfileDef profile, IfcAxis2Placement placement)
        {
            if (profile is IfcArbitraryClosedProfileDef closedProfile)
            {
                return closedProfile.OuterCurve.ToNTSPolygon(placement);
            }
            else if (profile is IfcRectangleProfileDef rectangleProfile)
            {
                return rectangleProfile.ToNTSPolygon(placement);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static Polygon ToNTSPolygon(this IfcCurve curve, IfcAxis2Placement placement)
        {
            //if (curve.Area() < 1e-6)
            //{
            //    return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon();
            //}
            var geometry = curve.ToNTSLineString(placement);
            return geometry.CreatePolygon();
        }

        public static Polygon ToNTSPolygon(this IfcRectangleProfileDef rectangleProfile, IfcAxis2Placement placement)
        {
            //if (curve.Area() < 1e-6)
            //{
            //    return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon();
            //}
            var geometry = rectangleProfile.ToNTSLineString(placement);
            return geometry.CreatePolygon();
        }

        private static Polygon CreatePolygon(this LineString geometry)
        {
            if (geometry is LinearRing ring)
            {
                return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon(ring);
            }
            else
            {
                return ThIFCNTSService.Instance.GeometryFactory.CreatePolygon();
            }
        }

        public static LineString ToNTSLineString(this IfcCurve curve, IfcAxis2Placement placement)
        {
            if (curve is IfcPolyline polyline)
            {
                return polyline.ToNTSLineString(placement);
            }
            else if (curve is IfcCompositeCurve compositeCurve)
            {
                return compositeCurve.ToNTSLineString(placement);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public static LineString ToNTSLineString(this IfcPolyline polyline, IfcAxis2Placement placement)
        {
            var points = new List<Coordinate>();
            var offset = (placement as IfcPlacement).Location.ToNTSCoordinate();
            for (int i = 0; i < polyline.Points.Count; i++)
            {
                points.Add(polyline.Points[i].ToNTSCoordinate().Offset(offset));
            }
            points.Add(polyline.Points[0].ToNTSCoordinate().Offset(offset));

            return points.CreateLineString();
        }

        public static LineString ToNTSLineString(this IfcCompositeCurve compositeCurve, IfcAxis2Placement placement)
        {
            var points = new List<Coordinate>();
            var offset = (placement as IfcPlacement).Location.ToNTSCoordinate();
            for (int i = 0; i < compositeCurve.Segments.Count; i++)
            {
                points.Add((compositeCurve.Segments[i].ParentCurve as IfcPolyline).Points[0].ToNTSCoordinate().Offset(offset));
            }
            points.Add((compositeCurve.Segments[0].ParentCurve as IfcPolyline).Points[0].ToNTSCoordinate().Offset(offset));

            return points.CreateLineString();
        }

        public static LineString ToNTSLineString(this IfcRectangleProfileDef rectangleProfile, IfcAxis2Placement placement)
        {
            var points = new List<Coordinate>();
            var location = new Coordinate(rectangleProfile.Position.Location.X.MakePrecise(), rectangleProfile.Position.Location.Y.MakePrecise());
            var vector1 = new Vector2D(rectangleProfile.Position.P[0].X.MakePrecise(), rectangleProfile.Position.P[0].Y.MakePrecise());
            var vector2 = new Vector2D(rectangleProfile.Position.P[1].X.MakePrecise(), rectangleProfile.Position.P[1].Y.MakePrecise());
            var xDim = ((double)rectangleProfile.XDim.Value).MakePrecise();
            var yDim = ((double)rectangleProfile.YDim.Value).MakePrecise();
            var offset = (placement as IfcPlacement).Location.ToNTSCoordinate();
            points.Add(ThNTSOperation.Addition(location, vector1, vector2, xDim, yDim).Offset(offset));
            points.Add(ThNTSOperation.Addition(location, -vector1, vector2, xDim, yDim).Offset(offset));
            points.Add(ThNTSOperation.Addition(location, -vector1, -vector2, xDim, yDim).Offset(offset));
            points.Add(ThNTSOperation.Addition(location, vector1, -vector2, xDim, yDim).Offset(offset));
            points.Add(ThNTSOperation.Addition(location, vector1, vector2, xDim, yDim).Offset(offset));

            return points.CreateLineString();
        }

        public static Coordinate ToNTSCoordinate(this IfcCartesianPoint point)
        {
            return new Coordinate(point.X.MakePrecise(), point.Y.MakePrecise());
        }

        private static double MakePrecise(this double value)
        {
            return ThIFCNTSService.Instance.PrecisionModel.MakePrecise(value);
        }

        private static LineString CreateLineString(this List<Coordinate> points)
        {
            if (points.Count == 0)
            {
                throw new NotSupportedException();
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

        private static Coordinate Offset(this Coordinate first, Coordinate second)
        {
            return new Coordinate(first.X + second.X, first.Y + second.Y);
        }
    }
}
