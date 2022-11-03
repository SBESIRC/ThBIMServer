using System;
using System.Linq;

using Xbim.Ifc;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.GeometricConstraintResource;

using ThBIMServer.Ifc2x3;

namespace ThBIMServer.Deduct
{
    public static class ThDeductFactory
    {
        public static IfcProductDefinitionShape CreateProductDefinitionShape(IfcStore model, IfcRepresentationItem solid)
        {
            var shape = ThIFC2x3Factory.CreateSolidClippingBody(model, solid);
            return ThIFC2x3Factory.CreateProductDefinitionShape(model, shape);
        }

        public static IfcRepresentationItem ToIfcRepresentationItem(IfcStore model, IfcWall struWall, bool withPlacement = false)
        {
            var solid = struWall.Representation.Representations.First().Items[0];
            if (!withPlacement)
            {
                return ToIfcRepresentationItem(model, solid);
            }
            else
            {
                var placement = ((IfcLocalPlacement)struWall.ObjectPlacement).RelativePlacement;
                return ToIfcRepresentationItem(model, solid, placement);
            }
        }

        public static IfcLocalPlacement ToIfcLocalPlacement(IfcStore model, IfcObjectPlacement relative_to, IfcLengthMeasure measure)
        {
            return model.Instances.New<IfcLocalPlacement>(l =>
            {
                //l.PlacementRelTo = relative_to;
                l.RelativePlacement = ToIfcAxis2Placement3D(model, ((IfcLocalPlacement)relative_to).RelativePlacement, measure);
            });
        }

        public static IfcProductDefinitionShape CreateIfcBooleanClippingResult(IfcStore model, IfcRepresentationItem minuend, IfcRepresentationItem subtractor)
        {
            var solid = ToIfcBooleanClippingResult(model, minuend, subtractor);
            var shape = ThIFC2x3Factory.CreateSolidClippingBody(model, solid);
            return ThIFC2x3Factory.CreateProductDefinitionShape(model, shape);
        }

        private static IfcRepresentationItem ToIfcRepresentationItem(IfcStore model, IfcRepresentationItem solid, IfcAxis2Placement placement)
        {
            if (solid is IfcExtrudedAreaSolid areaSolid)
            {
                return ToIfcExtrudedAreaSolid(model, areaSolid, placement);
            }
            else if (solid is IfcBooleanClippingResult clippingResult)
            {
                return ToIfcBooleanClippingResult(model, clippingResult, placement);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static IfcRepresentationItem ToIfcRepresentationItem(IfcStore model, IfcRepresentationItem solid)
        {
            if (solid is IfcExtrudedAreaSolid areaSolid)
            {
                return ToIfcExtrudedAreaSolid(model, areaSolid);
            }
            else if (solid is IfcBooleanClippingResult clippingResult)
            {
                return ToIfcBooleanClippingResult(model, clippingResult);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static IfcRectangleProfileDef ToIfcRectangleProfileDef(IfcStore model, IfcRectangleProfileDef rectangleProfile)
        {
            return model.Instances.New<IfcRectangleProfileDef>(d =>
            {
                d.XDim = rectangleProfile.XDim;
                d.YDim = rectangleProfile.YDim;
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.Position = model.ToIfcAxis2Placement2D(XbimPoint3D.Zero);
            });
        }

        private static IfcExtrudedAreaSolid ToIfcExtrudedAreaSolid(IfcStore model, IfcExtrudedAreaSolid areaSolid, IfcAxis2Placement placement)
        {
            var newSolid = model.Instances.New<IfcExtrudedAreaSolid>(s =>
            {
                s.Depth = areaSolid.Depth;
                s.ExtrudedDirection = model.ToIfcDirection(new XbimVector3D(0, 0, 1));
                s.Position = ToIfcAxis2Placement3D(model, placement);
            });

            if (areaSolid.SweptArea is IfcArbitraryClosedProfileDef arbitraryClosedProfile)
            {
                newSolid.SweptArea = ToIfcArbitraryClosedProfileDef(model, arbitraryClosedProfile);
            }
            else if (areaSolid.SweptArea is IfcRectangleProfileDef rectangleProfile)
            {
                newSolid.SweptArea = ToIfcRectangleProfileDef(model, rectangleProfile);
            }

            return newSolid;
        }

        private static IfcExtrudedAreaSolid ToIfcExtrudedAreaSolid(IfcStore model, IfcExtrudedAreaSolid areaSolid)
        {
            var newSolid = model.Instances.New<IfcExtrudedAreaSolid>(s =>
            {
                s.Depth = areaSolid.Depth;
                s.ExtrudedDirection = model.ToIfcDirection(new XbimVector3D(0, 0, 1));
                s.Position = model.ToIfcAxis2Placement3D(XbimPoint3D.Zero);
            });

            if (areaSolid.SweptArea is IfcArbitraryClosedProfileDef arbitraryClosedProfile)
            {
                newSolid.SweptArea = ToIfcArbitraryClosedProfileDef(model, arbitraryClosedProfile);
            }
            else if (areaSolid.SweptArea is IfcRectangleProfileDef rectangleProfile)
            {
                newSolid.SweptArea = ToIfcRectangleProfileDef(model, rectangleProfile);
            }

            return newSolid;
        }

        private static IfcBooleanClippingResult ToIfcBooleanClippingResult(IfcStore model, IfcBooleanClippingResult clippingResult, IfcAxis2Placement placement)
        {
            var newSolid = model.Instances.New<IfcBooleanClippingResult>(s =>
            {
                s.Operator = clippingResult.Operator;
            });

            if (clippingResult.FirstOperand is IfcExtrudedAreaSolid extrudedAreaSolid)
            {
                newSolid.FirstOperand = ToIfcExtrudedAreaSolid(model, extrudedAreaSolid, placement);
            }
            else if (clippingResult.FirstOperand is IfcBooleanClippingResult result)
            {
                newSolid.FirstOperand = ToIfcBooleanClippingResult(model, result, placement);
            }
            if (clippingResult.SecondOperand is IfcHalfSpaceSolid halfSpaceSolid)
            {
                newSolid.SecondOperand = ToIfcHalfSpaceSolid(model, halfSpaceSolid);
            }

            return newSolid;
        }

        private static IfcBooleanClippingResult ToIfcBooleanClippingResult(IfcStore model, IfcBooleanClippingResult clippingResult)
        {
            var newSolid = model.Instances.New<IfcBooleanClippingResult>(s =>
            {
                s.Operator = clippingResult.Operator;
            });

            if (clippingResult.FirstOperand is IfcExtrudedAreaSolid extrudedAreaSolid1)
            {
                newSolid.FirstOperand = ToIfcExtrudedAreaSolid(model, extrudedAreaSolid1);
            }
            else if (clippingResult.FirstOperand is IfcBooleanClippingResult result)
            {
                newSolid.FirstOperand = ToIfcBooleanClippingResult(model, result);
            }
            if (clippingResult.SecondOperand is IfcHalfSpaceSolid halfSpaceSolid)
            {
                newSolid.SecondOperand = ToIfcHalfSpaceSolid(model, halfSpaceSolid);
            }
            else if(clippingResult.SecondOperand is IfcExtrudedAreaSolid extrudedAreaSolid2)
            {
                newSolid.SecondOperand = ToIfcExtrudedAreaSolid(model, extrudedAreaSolid2);
            }

            return newSolid;
        }

        private static IfcBooleanClippingResult ToIfcBooleanClippingResult(IfcStore model, IfcRepresentationItem minuend, IfcRepresentationItem subtractor)
        {
            var newSolid = model.Instances.New<IfcBooleanClippingResult>(s =>
            {
                s.Operator = IfcBooleanOperator.DIFFERENCE;
            });

            newSolid.FirstOperand = (IfcBooleanOperand)minuend;
            newSolid.SecondOperand = (IfcBooleanOperand)subtractor;

            return newSolid;
        }

        private static IfcHalfSpaceSolid ToIfcHalfSpaceSolid(IfcStore model, IfcHalfSpaceSolid halfSpaceSolid)
        {
            var newSolid = model.Instances.New<IfcHalfSpaceSolid>(s =>
            {
            });

            if (halfSpaceSolid.BaseSurface is IfcPlane plane)
            {
                newSolid.BaseSurface = ToIfcPlane(model, plane.Position);
            }

            return newSolid;
        }

        private static IfcPlane ToIfcPlane(IfcStore model, IfcAxis2Placement placement)
        {
            var newSolid = model.Instances.New<IfcPlane>(p =>
            {
                p.Position = ToIfcAxis2Placement3D(model, placement);
            });

            return newSolid;
        }

        private static IfcArbitraryClosedProfileDef ToIfcArbitraryClosedProfileDef(IfcStore model, IfcArbitraryClosedProfileDef arbitraryClosedProfile)
        {
            return model.Instances.New<IfcArbitraryClosedProfileDef>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = ToIfcCompositeCurve(model, arbitraryClosedProfile.OuterCurve);
            });
        }

        private static IfcCompositeCurve ToIfcCompositeCurve(IfcStore model, IfcCurve curve)
        {
            if (curve is IfcPolyline polyline)
            {
                return ToIfcCompositeCurve(model, polyline);
            }
            else if (curve is IfcCompositeCurve compositeCurve)
            {
                return ToIfcCompositeCurve(model, compositeCurve);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static IfcCompositeCurve ToIfcCompositeCurve(IfcStore model, IfcPolyline polyline)
        {
            var compositeCurve = ThIFC2x3Factory.CreateIfcCompositeCurve(model);
            var pts = polyline.Points;
            for (int k = 0; k < polyline.Points.Count() - 1; k++)
            {
                var curveSegement = ThIFC2x3Factory.CreateIfcCompositeCurveSegment(model);
                curveSegement.ParentCurve = ToIfcPolyline(model, pts[k], pts[k + 1]);
                compositeCurve.Segments.Add(curveSegement);
            }
            return compositeCurve;
        }

        private static IfcCompositeCurve ToIfcCompositeCurve(IfcStore model, IfcCompositeCurve compositeCurve)
        {
            var curve = ThIFC2x3Factory.CreateIfcCompositeCurve(model);
            foreach (var segment in compositeCurve.Segments)
            {
                var curveSegement = ThIFC2x3Factory.CreateIfcCompositeCurveSegment(model);
                if (segment.ParentCurve is IfcPolyline polyline)
                {
                    if (polyline.Points.Count == 2)
                    {
                        curveSegement.ParentCurve = ToIfcPolyline(model, polyline.Points[0], polyline.Points[1]);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                curve.Segments.Add(curveSegement);
            }

            return curve;
        }

        private static IfcPolyline ToIfcPolyline(IfcStore model, IfcCartesianPoint startPt, IfcCartesianPoint endPt)
        {
            var poly = model.Instances.New<IfcPolyline>();
            poly.Points.Add(ToIfcCartesianPoint(model, startPt));
            poly.Points.Add(ToIfcCartesianPoint(model, endPt));
            return poly;
        }

        private static IfcCartesianPoint ToIfcCartesianPoint(IfcStore model, IfcCartesianPoint point)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z);
            });
        }

        private static IfcCartesianPoint ToIfcCartesianPoint(IfcStore model, IfcCartesianPoint point, IfcLengthMeasure measure)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z + (double)measure.Value);
            });
        }

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(IfcStore model, IfcAxis2Placement placement)
        {
            if (placement is IfcAxis2Placement3D placement3D)
            {
                return model.Instances.New<IfcAxis2Placement3D>(p =>
                {
                    p.Axis = ToIfcDirection(model, placement3D.Axis);
                    p.RefDirection = ToIfcDirection(model, placement3D.RefDirection);
                    p.Location = ToIfcCartesianPoint(model, placement3D.Location);
                });
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(IfcStore model, IfcAxis2Placement placement, IfcLengthMeasure measure)
        {
            if (placement is IfcAxis2Placement3D placement3D)
            {
                return model.Instances.New<IfcAxis2Placement3D>(p =>
                {
                    p.Axis = ToIfcDirection(model, placement3D.Axis);
                    p.RefDirection = ToIfcDirection(model, placement3D.RefDirection);
                    p.Location = ToIfcCartesianPoint(model, placement3D.Location, measure);
                });
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static IfcDirection ToIfcDirection(IfcStore model, IfcDirection vector)
        {
            if (vector == null)
            {
                return null;
            }
            else
            {
                return model.Instances.New<IfcDirection>(d =>
                {
                    d.SetXYZ(vector.X, vector.Y, vector.Z);
                });
            }
        }
    }
}
