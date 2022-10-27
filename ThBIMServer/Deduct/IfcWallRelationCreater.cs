using System.Linq;
using System.Security.AccessControl;
using ThBIMServer.NTS;
using Xbim.Ifc;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer.Deduct
{
    public class IfcWallRelationCreater
    {
        public void CreateRelation(IfcStore model)
        {
            var walls = model.Instances.OfType<IfcWall>().ToList();
            var types = model.Instances.OfType<IfcRelDefinesByType>().ToList();
            //var archType = model.Instances.Where((IfcRelDefinesByType r) => r.RelatingType == IfcWallTypeEnum.STANDARD).FirstOrDefault();
            var struType = types.Where(r => ((IfcWallType)r.RelatingType).PredefinedType == IfcWallTypeEnum.SHEAR).FirstOrDefault();
            if (struType == null)
            {
                return;
            }

            var struWalls = walls.Where(o => struType.RelatedObjects.Contains(o)).ToList();
            var archWalls = walls.Except(struWalls).ToList();
            var archProfiles = archWalls.Select(o => (((IfcSweptAreaSolid)o.Representation.Representations[0].Items[0]).SweptArea)).ToList();
            var spatialIndex = new ThIFCNTSSpatialIndex(archProfiles);
            struWalls.ForEach(struWall =>
            {
                var profile = ((IfcSweptAreaSolid)struWall.Representation.Representations[0].Items[0]).SweptArea;
                var filter = spatialIndex.SelectCrossingPolygon(profile);
                filter.ForEach(o =>
                {
                    var crossWall = archWalls.Where(archWall => (((IfcSweptAreaSolid)archWall.Representation.Representations[0].Items[0]).SweptArea).Equals(o)).FirstOrDefault();
                    if (crossWall != null)
                    {
                        CreateRelation(model, crossWall, struWall);
                    }
                });
            });
        }

        private void CreateRelation(IfcStore model, IfcWall archWall, IfcWall struWall)
        {
            //
        }
    }
}
