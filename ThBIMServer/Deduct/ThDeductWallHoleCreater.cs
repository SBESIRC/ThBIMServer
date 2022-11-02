﻿using System;
using System.Linq;

using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.UtilityResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.GeometricConstraintResource;
using ThBIMServer.Ifc2x3;

namespace ThBIMServer.Deduct
{
    public static class ThDeductWallHoleCreater
    {
        public static IfcOpeningElement CreateHole(IfcStore model, IfcWall struWall, IfcLengthMeasure measure)
        {
            using (var txn = model.BeginTransaction("Create Hole"))
            {
                var ret = model.Instances.New<IfcOpeningElement>(d =>
                {
                    d.Name = "Wall Deduction";
                    d.GlobalId = IfcGloballyUniqueId.FromGuid(Guid.NewGuid());
                });

                //create representation
                var body = model.ToIfcRepresentationItem(struWall);
                ret.Representation = model.CreateProductDefinitionShape(body);

                //object placement
                ret.ObjectPlacement = ToIfcLocalPlacement(model, struWall.ObjectPlacement, measure);

                txn.Commit();
                return ret;
            }
        }

        private static IfcProductDefinitionShape CreateProductDefinitionShape(this IfcStore model, IfcRepresentationItem solid)
        {
            var shape = ThIFC2x3Factory.CreateSolidClippingBody(model, solid);
            return ThIFC2x3Factory.CreateProductDefinitionShape(model, shape);
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

        private static IfcRepresentationItem ToIfcRepresentationItem(this IfcStore model, IfcWall struWall)
        {
            var solid = struWall.Representation.Representations.First().Items[0];
            return model.ToIfcRepresentationItem(solid);
        }

        private static IfcRepresentationItem ToIfcRepresentationItem(this IfcStore model, IfcRepresentationItem solid)
        {
            if (solid is IfcExtrudedAreaSolid areaSolid)
            {
                return model.ToIfcExtrudedAreaSolid(areaSolid);
            }
            else if (solid is IfcBooleanClippingResult clippingResult)
            {
                return model.ToIfcBooleanClippingResult(clippingResult);
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

        private static IfcBooleanClippingResult ToIfcBooleanClippingResult(this IfcStore model, IfcBooleanClippingResult clippingResult)
        {
            var newSolid = model.Instances.New<IfcBooleanClippingResult>(s =>
            {
                s.Operator = clippingResult.Operator;
            });

            if (clippingResult.FirstOperand is IfcExtrudedAreaSolid extrudedAreaSolid)
            {
                newSolid.FirstOperand = model.ToIfcExtrudedAreaSolid(extrudedAreaSolid);
            }
            else if (clippingResult.FirstOperand is IfcBooleanClippingResult result)
            {
                newSolid.FirstOperand = model.ToIfcBooleanClippingResult(result);
            }
            if (clippingResult.SecondOperand is IfcHalfSpaceSolid halfSpaceSolid)
            {
                newSolid.SecondOperand = model.ToIfcHalfSpaceSolid(halfSpaceSolid);
            }

            return newSolid;
        }

        private static IfcHalfSpaceSolid ToIfcHalfSpaceSolid(this IfcStore model, IfcHalfSpaceSolid halfSpaceSolid)
        {
            var newSolid = model.Instances.New<IfcHalfSpaceSolid>(s =>
            {
            });

            if (halfSpaceSolid.BaseSurface is IfcPlane plane)
            {
                newSolid.BaseSurface = model.ToIfcPlane(plane.Position);
            }

            return newSolid;
        }

        private static IfcPlane ToIfcPlane(this IfcStore model, IfcAxis2Placement placement)
        {
            var newSolid = model.Instances.New<IfcPlane>(p =>
            {
                p.Position = model.ToIfcAxis2Placement3D(placement);
            });

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

        private static IfcCartesianPoint ToIfcCartesianPoint(this IfcStore model, IfcCartesianPoint point, IfcLengthMeasure measure)
        {
            return model.Instances.New<IfcCartesianPoint>(c =>
            {
                c.SetXYZ(point.X, point.Y, point.Z + (double)measure.Value);
            });
        }

        private static IfcLocalPlacement ToIfcLocalPlacement(IfcStore model, IfcObjectPlacement relative_to, IfcLengthMeasure measure)
        {
            return model.Instances.New<IfcLocalPlacement>(l =>
            {
                //l.PlacementRelTo = relative_to;
                l.RelativePlacement = model.ToIfcAxis2Placement3D(((IfcLocalPlacement)relative_to).RelativePlacement, measure);
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

        private static IfcAxis2Placement3D ToIfcAxis2Placement3D(this IfcStore model, IfcAxis2Placement placement, IfcLengthMeasure measure)
        {
            if (placement is IfcAxis2Placement3D placement3D)
            {
                return model.Instances.New<IfcAxis2Placement3D>(p =>
                {
                    p.Axis = model.ToIfcDirection(placement3D.Axis);
                    p.RefDirection = model.ToIfcDirection(placement3D.RefDirection);
                    p.Location = model.ToIfcCartesianPoint(placement3D.Location, measure);
                });
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static IfcDirection ToIfcDirection(this IfcStore model, IfcDirection vector)
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

        private static IfcRelDefinesByProperties CloneAndCreateNew(this IfcRelDefinesByProperties property, IfcStore model)
        {
            var result = model.Instances.New<IfcRelDefinesByProperties>(rel =>
            {
                rel.Name = property.Name.ToString();
                rel.RelatingPropertyDefinition = property.RelatingPropertyDefinition.CloneAndCreateNew(model);
            });
            return result;
        }

        private static IfcPropertySetDefinition CloneAndCreateNew(this IfcPropertySetDefinition propertySetDefinition, IfcStore model)
        {
            IfcPropertySetDefinition result;
            if (propertySetDefinition is IfcPropertySet propertySet)
            {
                result = model.Instances.New<IfcPropertySet>(pset =>
                {
                    pset.Name = propertySet.Name.ToString();
                    foreach (var item in propertySet.HasProperties)
                    {
                        pset.HasProperties.Add(item.CloneAndCreateNew(model));
                    }
                });
            }
            else
            {
                throw new NotSupportedException();
            }
            return result;
        }

        private static IfcProperty CloneAndCreateNew(this IfcProperty property, IfcStore model)
        {
            IfcProperty result;
            if (property is IfcPropertySingleValue propertySingleValue)
            {
                result = model.Instances.New<IfcPropertySingleValue>(p =>
                {
                    p.Name = propertySingleValue.Name.ToString();
                    p.NominalValue = propertySingleValue.NominalValue.CloneAndCreateNew();
                });
            }
            else
            {
                throw new NotSupportedException();
            }
            return result;
        }

        private static IfcValue CloneAndCreateNew(this IfcValue value)
        {
            if (value is IfcText ifcText)
            {
                return new IfcText(ifcText.ToString());
            }
            else if (value is IfcLengthMeasure ifcLengthMeasure)
            {
                return new IfcLengthMeasure(double.Parse(ifcLengthMeasure.Value.ToString()));
            }
            else
            {
                return new IfcText(value.Value.ToString());
            }
        }
    }
}
