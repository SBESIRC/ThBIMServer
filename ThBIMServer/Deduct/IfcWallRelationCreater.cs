using System;
using System.Linq;
using System.Collections.Generic;

using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.GeometricConstraintResource;

using ThBIMServer.NTS;
using ThBIMServer.Ifc2x3;

namespace ThBIMServer.Deduct
{
    public class IfcWallRelationCreater
    {
        public void CreateRelation(IfcStore model)
        {
            var project = model.Instances.OfType<IfcProject>().FirstOrDefault();
            var buildingStoreys = project.Sites.First().Buildings.First().BuildingStoreys.ToList();
            foreach (var storey in buildingStoreys)
            {
                var walls = new List<IfcWall>();
                foreach (var r in storey.ContainsElements)
                {
                    walls.AddRange(r.RelatedElements.OfType<IfcWall>());
                }
                var types = model.Instances.OfType<IfcRelDefinesByType>().ToList();
                //var archType = model.Instances.Where((IfcRelDefinesByType r) => r.RelatingType == IfcWallTypeEnum.STANDARD).FirstOrDefault();
                var struType = types.Where(r => ((IfcWallType)r.RelatingType).PredefinedType == IfcWallTypeEnum.SHEAR).FirstOrDefault();
                if (struType == null)
                {
                    return;
                }

                var struWalls = walls.Where(o => struType.RelatedObjects.Contains(o)).ToList();
                var archWalls = walls.Except(struWalls).ToList();
                var archProfileInfos = new List<Tuple<IfcProfileDef, IfcAxis2Placement>>();
                archWalls.ForEach(o =>
                {
                    var profile = ((IfcSweptAreaSolid)o.Representation.Representations[0].Items[0]).SweptArea;
                    var placement = ((IfcLocalPlacement)o.ObjectPlacement).RelativePlacement;
                    archProfileInfos.Add(Tuple.Create(profile, placement));
                });

                var spatialIndex = new ThIFCNTSSpatialIndex(archProfileInfos);
                struWalls.ForEach(struWall =>
                {
                    var profile = ((IfcSweptAreaSolid)struWall.Representation.Representations[0].Items[0]).SweptArea;
                    var placement = ((IfcLocalPlacement)struWall.ObjectPlacement).RelativePlacement;
                    var filter = spatialIndex.SelectCrossingPolygon(Tuple.Create(profile, placement));
                    filter.ForEach(o =>
                    {
                        var crossWall = archWalls.Where(archWall => (((IfcSweptAreaSolid)archWall.Representation.Representations[0].Items[0]).SweptArea).Equals(o.Item1)).FirstOrDefault();
                        if (crossWall != null)
                        {
                            CreateRelation(model, crossWall, struWall);
                        }
                    });
                });
            }
        }

        public void CreateRelationInSites(IfcStore model)
        {
            var project = model.Instances.OfType<IfcProject>().FirstOrDefault();
            var struStorey = project.Sites.First().Buildings.First().BuildingStoreys.ToList().First();
            var archStorey = project.Sites.Last().Buildings.First().BuildingStoreys.ToList().First();
            var struWalls = new List<IfcWall>();
            foreach (var r in struStorey.ContainsElements)
            {
                struWalls.AddRange(r.RelatedElements.OfType<IfcWall>());
            }
            var archWalls = new List<IfcWall>();
            foreach (var r in archStorey.ContainsElements)
            {
                archWalls.AddRange(r.RelatedElements.OfType<IfcWall>());
            }

            var archProfileInfos = new List<Tuple<IfcProfileDef, IfcAxis2Placement>>();
            archWalls.ForEach(o =>
            {
                var profile = ((IfcSweptAreaSolid)o.Representation.Representations[0].Items[0]).SweptArea;
                var placement = ((IfcLocalPlacement)o.ObjectPlacement).RelativePlacement;
                archProfileInfos.Add(Tuple.Create(profile, placement));
            });

            var spatialIndex = new ThIFCNTSSpatialIndex(archProfileInfos);
            struWalls.ForEach(struWall =>
            {
                var solid = struWall.Representation.Representations[0].Items[0];
                var profile = GetIfcProfileDef(solid);
                var placement = ((IfcLocalPlacement)struWall.ObjectPlacement).RelativePlacement;
                var filter = spatialIndex.SelectCrossingPolygon(Tuple.Create(profile, placement));
                filter.ForEach(o =>
                {
                    var crossWall = archWalls.Where(archWall => ((IfcSweptAreaSolid)archWall.Representation.Representations[0].Items[0]).SweptArea.Equals(o.Item1)).FirstOrDefault();
                    if (crossWall != null)
                    {
                        CreateRelation(model, crossWall, struWall);
                    }
                });
            });
        }

        private IfcProfileDef GetIfcProfileDef(IfcRepresentationItem solid)
        {
            if (solid is IfcSweptAreaSolid sweptArea)
            {
                return sweptArea.SweptArea;
            }
            else if (solid is IfcBooleanClippingResult clippingResult)
            {
                if (clippingResult.FirstOperand is IfcSweptAreaSolid e)
                {
                    return e.SweptArea;
                }
                else
                {
                    return GetIfcProfileDef(clippingResult.FirstOperand as IfcRepresentationItem);
                }
            }
            else
            {
                return null;
            }
        }

        private void CreateRelation(IfcStore model, IfcWall archWall, IfcWall struWall)
        {
            var ifcHole = ThIFC2x32IFC2x3Factory.CreateHole(model, struWall);
            ThIFC2x32IFC2x3Factory.BuildRelationship(model, archWall, struWall, ifcHole);
        }
    }
}
