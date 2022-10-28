using System;
using System.Linq;
using System.Collections.Generic;

using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometricConstraintResource;

namespace ThBIMServer.Ifc4
{
    public static class ThIFC42ProtoBufFactory
    {
        /// <summary>
        /// 根据ifc中墙的数据创建TCHProjectData
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ThTCHProjectData CreateTCHProject(this IfcProject project)
        {
            var prjId = "";
            var prjName = "测试项目";
            var thPrj = new ThTCHProjectData();
            thPrj.Root = new ThTCHRootData()
            {
                GlobalId = prjId,
                Name = prjName,
                Description = "ThTCHProjectData"
            };
            var thSite = new ThTCHSiteData();
            thSite.Root = new ThTCHRootData();
            thSite.Root.GlobalId = prjId + "site";
            var thTCHBuildingData = new ThTCHBuildingData();
            thTCHBuildingData.Root = new ThTCHRootData();
            thTCHBuildingData.Root.GlobalId = prjId + "Building";

            var buildingStoreys = project.Sites.First().Buildings.First().BuildingStoreys.ToList();
            foreach (var storey in buildingStoreys)
            {
                var floorName = ((IfcRoot)storey).Name.Value;
                var floorNum = ((string)floorName);
                var elevation = (double)storey.Elevation;
                var levelHeight = 0.0;

                var buildingStorey = new ThTCHBuildingStoreyData();
                buildingStorey.BuildElement = new ThTCHBuiltElementData();
                buildingStorey.BuildElement.Root = new ThTCHRootData();
                buildingStorey.BuildElement.Root = new ThTCHRootData();
                buildingStorey.BuildElement.Root.GlobalId = prjId + floorNum.ToString();
                buildingStorey.BuildElement.Root.Name = floorNum.ToString();
                buildingStorey.BuildElement.Root.Description = "ThDefinition" + floorNum;
                buildingStorey.Number = floorNum.ToString();
                buildingStorey.Height = levelHeight;
                buildingStorey.Elevation = elevation;
                buildingStorey.Usage = floorName;
                buildingStorey.Origin = new ThTCHPoint3d() { X = 0, Y = 0, Z = elevation };
                buildingStorey.BuildElement.Properties.Add(new ThTCHProperty { Key = "FloorNo", Value = floorNum.ToString() });
                buildingStorey.BuildElement.Properties.Add(new ThTCHProperty { Key = "Height", Value = levelHeight.ToString() });
                buildingStorey.BuildElement.Properties.Add(new ThTCHProperty { Key = "StdFlrNo", Value = floorNum.ToString() });

                var ifcWalls = new List<IfcWall>();
                foreach (var r in storey.ContainsElements)
                {
                    ifcWalls.AddRange(r.RelatedElements.OfType<IfcWall>());
                }
                ifcWalls.ForEach(wall =>
                {
                    var copyItem = wall.WallDataEntityToTCHWall();
                    buildingStorey.Walls.Add(copyItem);
                });

                thTCHBuildingData.Storeys.Add(buildingStorey);
            }
            thSite.Buildings.Add(thTCHBuildingData);
            thPrj.Site = thSite;
            return thPrj;
        }

        private static ThTCHWallData WallDataEntityToTCHWall(this IfcWall ifcWall)
        {
            var newWall = new ThTCHWallData();
            newWall.WallType = WallTypeEnum.Shear;
            newWall.BuildElement = new ThTCHBuiltElementData
            {
                Origin = new ThTCHPoint3d() { X = 0, Y = 0, Z = 0 },
                XVector = new ThTCHVector3d() { X = 1, Y = 0, Z = 0 }
            };
            var material = (Xbim.Ifc4.MaterialResource.IfcMaterialLayerSetUsage)ifcWall.Material;
            if (material != null)
            {
                newWall.BuildElement.EnumMaterial = material.ForLayerSet.LayerSetName;
            }

            if (ifcWall.Representation.Representations.First().Items[0] is IfcExtrudedAreaSolid areaSolid)
            {
                newWall.BuildElement.Height = areaSolid.Depth;
                if (areaSolid.SweptArea is IfcArbitraryClosedProfileDef arbitraryClosedProfile)
                {
                    newWall.BuildElement.Outline = arbitraryClosedProfile.OuterCurve.ToTCHMPolygon();
                }
                else if (areaSolid.SweptArea is IfcRectangleProfileDef rectangleProfile)
                {
                    // 其余属性Location,P是否需要赋值存疑
                    if (rectangleProfile.Position.Location.X != 0)
                    {
                        throw new NotImplementedException();
                    }
                    newWall.BuildElement.Length = rectangleProfile.XDim;
                    newWall.BuildElement.Width = rectangleProfile.YDim;
                    newWall.BuildElement.Origin = new ThTCHPoint3d 
                    { 
                        X = ((IfcPlacement)((IfcLocalPlacement)ifcWall.ObjectPlacement).RelativePlacement).Location.X,
                        Y = ((IfcPlacement)((IfcLocalPlacement)ifcWall.ObjectPlacement).RelativePlacement).Location.Y,
                        Z = ((IfcPlacement)((IfcLocalPlacement)ifcWall.ObjectPlacement).RelativePlacement).Location.Z,
                    };
                }
            }

            return newWall;
        }

        private static ThTCHMPolygon ToTCHMPolygon(this IfcCurve polyline)
        {
            var tchPolygon = new ThTCHMPolygon();
            if (polyline is IfcPolyline p)
            {
                tchPolygon.Shell = p.ToTCHPolyline();
            }
            return tchPolygon;
        }

        private static ThTCHPolyline ToTCHPolyline(this IfcPolyline polyline)
        {
            var tchPolyline = new ThTCHPolyline();
            tchPolyline.Points.Add(polyline.Points[0].ToTCHPoint3d());
            uint ptIndex = 0;
            for (int k = 0; k < polyline.Points.Count() - 1; k++)
            {
                if (polyline.Points[k].DistanceSquared(polyline.Points[k + 1]) > 10)
                {
                    var tchSegment = new ThTCHSegment();
                    tchSegment.Index.Add(ptIndex);

                    // 直线段
                    tchPolyline.Points.Add(polyline.Points[k + 1].ToTCHPoint3d());
                    tchSegment.Index.Add(++ptIndex);
                    tchPolyline.Segments.Add(tchSegment);
                }
            }
            return tchPolyline;
        }

        private static ThTCHPoint3d ToTCHPoint3d(this IfcCartesianPoint point)
        {
            return new ThTCHPoint3d { X = point.X, Y = point.Y, Z = 0.0 };
        }
    }
}
