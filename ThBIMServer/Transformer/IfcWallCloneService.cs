using System;
using System.Collections.Generic;
using System.Linq;
using ThMEPIFC.Ifc2x3;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc2x3.GeometricConstraintResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.UtilityResource;

namespace ThBIMServer.Transformer
{
    public static class IfcWallCloneService
    {
        private static XbimVector3D ZAxis => new XbimVector3D(0, 0, 1);

        public static List<IfcWall> CreateWall(IfcStore model, List<IfcWall> srcWall)
        {
            return srcWall.Select(o => CreateWall(model, o)).ToList();
        }

        private static IfcWall CreateWall(IfcStore model, IfcWall wall)
        {
            using (var txn = model.BeginTransaction("Create Wall"))
            {
                var ret = model.Instances.New<IfcWall>(d =>
                {
                    d.Name = "Wall";
                    d.GlobalId = IfcGloballyUniqueId.FromGuid(Guid.NewGuid());
                });

                //create representation
                var profile = GetProfile(model, wall);
                var solid = model.ToIfcExtrudedAreaSolid(profile, ZAxis, 5400.0);
                ret.Representation = ThProtoBuf2IFC2x3Factory.CreateProductDefinitionShape(model, solid);

                //object placement
                ret.ObjectPlacement = ToIfcLocalPlacement(model, wall.ObjectPlacement);

                // add properties
                model.Instances.New<IfcRelDefinesByProperties>(rel =>
                {
                    rel.Name = "THifc properties";
                    rel.RelatedObjects.Add(ret);
                    rel.RelatingPropertyDefinition = model.Instances.New<IfcPropertySet>(pset =>
                    {
                        pset.Name = "Basic set of THifc properties";
                        //foreach (var item in wall.BuildElement.Properties)
                        //{
                        //    pset.HasProperties.Add(model.Instances.New<IfcPropertySingleValue>(p =>
                        //    {
                        //        p.Name = item.Key;
                        //        p.NominalValue = new IfcText(item.Value.ToString());
                        //    }));
                        //}
                    });
                });

                txn.Commit();
                return ret;
            }
        }

        private static IfcProfileDef GetProfile(IfcStore model, IfcWall wall)
        {
            return model.ToIfcArbitraryClosedProfileDef(((Xbim.Ifc2x3.ProfileResource.IfcArbitraryClosedProfileDef)((Xbim.Ifc2x3.GeometricModelResource.IfcSweptAreaSolid)wall.Representation.Representations[0].Items[0]).SweptArea).OuterCurve as IfcPolyline);
        }

        private static IfcArbitraryClosedProfileDef ToIfcArbitraryClosedProfileDef(this IfcStore model, IfcPolyline e)
        {
            return model.Instances.New<IfcArbitraryClosedProfileDef>(d =>
            {
                d.ProfileType = IfcProfileTypeEnum.AREA;
                d.OuterCurve = model.ToIfcCompositeCurve(e);
            });
        }

        public static IfcCompositeCurve ToIfcCompositeCurve(this IfcStore model, IfcPolyline e)
        {
            var compositeCurve = ThIFC2x3Factory.CreateIfcCompositeCurve(model);
            var pts = e.Points;
            for (var i = 1; i < pts.Count; i++)
            {
                var curveSegement = ThIFC2x3Factory.CreateIfcCompositeCurveSegment(model);
                //直线段
                curveSegement.ParentCurve = model.ToIfcPolyline(pts[i - 1], pts[i]);
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
                c.SetXYZ(point.X, point.Y, 0.0);
            });
        }

        public static IfcLocalPlacement ToIfcLocalPlacement(this IfcStore model, IfcObjectPlacement matrix, IfcObjectPlacement relative_to = null)
        {
            return model.Instances.New<IfcLocalPlacement>(l =>
            {
                l.PlacementRelTo = relative_to;
                l.RelativePlacement = model.ToIfcAxis2Placement3D(matrix);
            });
        }

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(this IfcStore model, IfcObjectPlacement m)
        {
            return model.Instances.New<IfcAxis2Placement3D>(p =>
            {
                p.Axis = model.ToIfcDirection(((Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D)((Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement)m).RelativePlacement).Axis);
                p.RefDirection = model.ToIfcDirection(((Xbim.Ifc2x3.GeometryResource.IfcAxis2Placement3D)((Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement)m).RelativePlacement).RefDirection);
                p.Location = model.ToIfcCartesianPoint(((Xbim.Ifc2x3.GeometryResource.IfcPlacement)((Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement)m).RelativePlacement).Location);
            });
        }

        private static IfcDirection ToIfcDirection(this IfcStore model, IfcDirection vector)
        {
            return model.Instances.New<IfcDirection>(d =>
            {
                d.SetXYZ(vector.X, vector.Y, vector.Z);
            });
        }
    }
}
