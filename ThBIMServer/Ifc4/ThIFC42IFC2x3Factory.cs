using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThBIMServer.Ifc2x3;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.SharedBldgElements;

namespace ThBIMServer.Ifc4
{
    public static class ThIFC42IFC2x3Factory
    {
        public static Xbim.Ifc2x3.SharedBldgElements.IfcWall CloneAndCreateNew(this IfcWall sourceWall, IfcStore model)
        {
            using (var txn = model.BeginTransaction("Create Wall"))
            {
                var ret = model.Instances.New<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                ret.Name = sourceWall.Name.ToString();
                ret.Description = sourceWall.Description.ToString();

                //model as a swept area solid
                var body = model.ToIfcSolid(sourceWall);

                //Create a Definition shape to hold the geometry
                var modelContext = model.Instances.OfType<Xbim.Ifc2x3.RepresentationResource.IfcGeometricRepresentationContext>().FirstOrDefault();
                var shape = model.Instances.New<Xbim.Ifc2x3.RepresentationResource.IfcShapeRepresentation>();
                shape.ContextOfItems = modelContext;
                shape.RepresentationType = "SurfaceModel";
                shape.RepresentationIdentifier = "Body";
                //shape.Items.Add(body);

                ////Create a Product Definition and add the model geometry to the wall
                //var rep = model.Instances.New<Xbim.Ifc2x3.RepresentationResource.IfcProductDefinitionShape>();
                //rep.Representations.Add(shape);
                //ret.Representation = rep;

                ////now place the wall into the model
                //var lp = model.Instances.New<IfcLocalPlacement>();
                //var ax3D = model.ToIfcAxis2Placement3D((sourceWall.ObjectPlacement as IfcLocalPlacement).RelativePlacement);
                //lp.RelativePlacement = ax3D;
                //ret.ObjectPlacement = lp;

                //// add properties
                //var property = sourceWall.Model.Instances.OfType<IfcRelDefinesByProperties>().FirstOrDefault(o => o.RelatedObjects.Contains(sourceWall));
                //if (property != null)
                //{
                //    var ifcRelDefinesByProperties = property.CloneAndCreateNew(model);
                //    ifcRelDefinesByProperties.RelatedObjects.Add(ret);
                //}
                txn.Commit();
                return ret;
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

        private static IfcExtrudedAreaSolid ToIfcExtrudedAreaSolid(this IfcStore model, IfcExtrudedAreaSolid areaSolid)
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
            var compositeCurve = ThIFC4Factory.CreateIfcCompositeCurve(model);
            var pts = polyline.Points;
            for (int k = 0; k < polyline.Points.Count() - 1; k++)
            {
                var curveSegement = ThIFC4Factory.CreateIfcCompositeCurveSegment(model);
                //curveSegement.ParentCurve = model.ToIfcPolyline(pts[k], pts[k + 1]);
                compositeCurve.Segments.Add(curveSegement);
            }
            return compositeCurve;
        }

        private static IfcDirection ToIfcDirection(this IfcStore model, XbimVector3D vector)
        {
            return model.Instances.New<IfcDirection>(d =>
            {
                d.SetXYZ(vector.X, vector.Y, vector.Z);
            });
        }

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(this IfcStore model, XbimPoint3D point)
        {
            return model.Instances.New<IfcAxis2Placement3D>(p =>
            {
                p.Location = model.ToIfcCartesianPoint(point);
            });
        }

        private static IfcAxis2Placement2D ToIfcAxis2Placement2D(this IfcStore model,
            XbimPoint3D point)
        {
            return model.Instances.New<IfcAxis2Placement2D>(p =>
            {
                p.Location = model.ToIfcCartesianPoint(point);
            });
        }

        private static IfcCartesianPoint ToIfcCartesianPoint(this IfcStore model, IfcCartesianPoint point)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z);
            });
        }

        private static IfcCartesianPoint ToIfcCartesianPoint(this IfcStore model, XbimPoint3D point)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z);
            });
        }
    }
}
