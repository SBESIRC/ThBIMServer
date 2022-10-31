using System;
using System.Linq;
using System.Xml.Linq;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc2x3.GeometricConstraintResource;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.UtilityResource;

namespace ThBIMServer.Ifc2x3
{
    public static class ThIFC2x32IFC2x3Factory
    {
        public static IfcOpeningElement CreateHole(IfcStore model, IfcWall archWall, IfcWall struWall)
        {
            using (var txn = model.BeginTransaction("Create Hole"))
            {
                var ret = model.Instances.New<IfcOpeningElement>(d =>
                {
                    d.Name = "Wall Deduction";
                    d.GlobalId = IfcGloballyUniqueId.FromGuid(Guid.NewGuid());
                });

                //create representation
                var solid = model.ToIfcSolid(struWall);
                ret.Representation = model.CreateProductDefinitionShape(solid);

                //object placement
                ret.ObjectPlacement = ToIfcLocalPlacement(model, struWall.ObjectPlacement);

                txn.Commit();
                return ret;
            }
        }

        public static void BuildRelationship(this IfcStore model, IfcWall archWall, IfcWall struWall, IfcOpeningElement hole)
        {
            using (var txn = model.BeginTransaction())
            {
                //create relVoidsElement
                var relVoidsElement = model.Instances.New<IfcRelVoidsElement>();
                relVoidsElement.RelatedOpeningElement = hole;
                relVoidsElement.RelatingBuildingElement = archWall;

                //create relFillsElement
                var relFillsElement = model.Instances.New<IfcRelFillsElement>();
                relFillsElement.RelatingOpeningElement = hole;
                relFillsElement.RelatedBuildingElement = struWall;

                txn.Commit();
            }
        }

        private static IfcExtrudedAreaSolid ToIfcSolid(this IfcStore model, IfcWall struWall)
        {
            if (struWall.Representation.Representations.First().Items[0] is IfcExtrudedAreaSolid areaSolid)
            {
                return model.ToIfcExtrudedAreaSolid(areaSolid);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static IfcRectangleProfileDef ToIfcRectangleProfileDef(this IfcStore model, IfcRectangleProfileDef rectangleProfile)
        {
            return model.Instances.New<IfcRectangleProfileDef>(d =>
            {
                d.XDim = rectangleProfile.XDim;
                d.YDim = rectangleProfile.YDim;
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.Position = model.ToIfcAxis2Placement2D(XbimPoint3D.Zero);
            });
        }

        public static IfcExtrudedAreaSolid ToIfcExtrudedAreaSolid(this IfcStore model, IfcExtrudedAreaSolid areaSolid)
        {
            var newSolid = model.Instances.New<IfcExtrudedAreaSolid>(s =>
            {
                s.Depth = areaSolid.Depth;
                s.ExtrudedDirection = model.ToIfcDirection(new XbimVector3D(0, 0, 1));
                s.Position = model.ToIfcAxis2Placement3D(XbimPoint3D.Zero);
            });

            if (areaSolid.SweptArea is IfcArbitraryClosedProfileDef arbitraryClosedProfile)
            {
                newSolid.SweptArea = model.ToIfcArbitraryClosedProfileDef(arbitraryClosedProfile);
            }
            else if (areaSolid.SweptArea is IfcRectangleProfileDef rectangleProfile)
            {
                newSolid.SweptArea = model.ToIfcRectangleProfileDef(rectangleProfile);
            }

            return newSolid;
        }

        private static IfcArbitraryClosedProfileDef ToIfcArbitraryClosedProfileDef(this IfcStore model, IfcArbitraryClosedProfileDef arbitraryClosedProfile)
        {
            return model.Instances.New<IfcArbitraryClosedProfileDef>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = model.ToIfcCompositeCurve(arbitraryClosedProfile.OuterCurve);
            });
        }

        private static IfcCompositeCurve ToIfcCompositeCurve(this IfcStore model, IfcCurve curve)
        {
            if (curve is IfcPolyline polyline)
            {
                return model.ToIfcCompositeCurve(polyline);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        private static IfcCompositeCurve ToIfcCompositeCurve(this IfcStore model, IfcPolyline polyline)
        {
            var compositeCurve = ThIFC2x3Factory.CreateIfcCompositeCurve(model);
            var pts = polyline.Points;
            for (int k = 0; k < polyline.Points.Count() - 1; k++)
            {
                var curveSegement = ThIFC2x3Factory.CreateIfcCompositeCurveSegment(model);
                curveSegement.ParentCurve = model.ToIfcPolyline(pts[k], pts[k + 1]);
                compositeCurve.Segments.Add(curveSegement);
            }
            return compositeCurve;
        }

        private static IfcPolyline ToIfcPolyline(this IfcStore model, IfcCartesianPoint startPt, IfcCartesianPoint endPt)
        {
            var poly = model.Instances.New<IfcPolyline>();
            poly.Points.Add(model.ToIfcCartesianPoint(startPt));
            poly.Points.Add(model.ToIfcCartesianPoint(endPt));
            return poly;
        }

        private static IfcCartesianPoint ToIfcCartesianPoint(this IfcStore model, IfcCartesianPoint point)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z);
            });
        }

        private static IfcLocalPlacement ToIfcLocalPlacement(IfcStore model, IfcObjectPlacement relative_to)
        {
            return model.Instances.New<IfcLocalPlacement>(l =>
            {
                l.PlacementRelTo = relative_to;
                l.RelativePlacement = model.ToIfcAxis2Placement3D(((IfcLocalPlacement)relative_to).RelativePlacement);
            });
        }

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(this IfcStore model, IfcAxis2Placement placement)
        {
            if (placement is IfcAxis2Placement3D placement3D)
            {
                return model.Instances.New<IfcAxis2Placement3D>(p =>
                {
                    p.Axis = model.ToIfcDirection(placement3D.Axis);
                    p.RefDirection = model.ToIfcDirection(placement3D.RefDirection);
                    p.Location = model.ToIfcCartesianPoint(placement3D.Location);
                });
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static IfcDirection ToIfcDirection(this IfcStore model, IfcDirection vector)
        {
            return model.Instances.New<IfcDirection>(d =>
            {
                d.SetXYZ(vector.X, vector.Y, vector.Z);
            });
        }
    }
}
