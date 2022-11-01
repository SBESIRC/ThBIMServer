using System;
using System.Linq;
using System.Collections.Generic;

using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc4.MeasureResource;

using ThBIMServer.Ifc2x3;
using ThBIMServer.Ifc4;

namespace ThBIMServer.ModelMerge
{
    public class ThModelMergeService
    {
        public IfcStore ModelMerge2x3(IfcStore archModel, IfcProject struProject)
        {
            var archProject = archModel.Instances.FirstOrDefault<Xbim.Ifc2x3.Kernel.IfcProject>();

            var archBuildings = archProject.Sites.FirstOrDefault()?.Buildings.FirstOrDefault() as Xbim.Ifc2x3.ProductExtension.IfcBuilding;
            var struBuildings = struProject.Sites.FirstOrDefault()?.Buildings.FirstOrDefault();

            //处理95%
            var StoreyDic = new List<Tuple<int, double, double>>();
            foreach (Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey BuildingStorey in archBuildings.BuildingStoreys)
            {
                double Storey_Elevation = BuildingStorey.Elevation.Value;
                double Storey_Height = double.Parse(((BuildingStorey.PropertySets.FirstOrDefault().PropertySetDefinitions.FirstOrDefault() as Xbim.Ifc2x3.Kernel.IfcPropertySet).HasProperties.FirstOrDefault(o => o.Name == "Height") as Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue).NominalValue.Value.ToString());
                StoreyDic.Add((int.Parse(BuildingStorey.Name.ToString()), Storey_Elevation, Storey_Height).ToTuple());
            }
            StoreyDic = StoreyDic.OrderBy(x => x.Item1).ToList();

            //处理5%
            foreach (Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey buildingStorey in struBuildings.BuildingStoreys)
            {
                var bigStorey = StoreyDic.FirstOrDefault(o => o.Item1.ToString() == buildingStorey.Name);
                if (bigStorey == null)
                {
                    var Storey_z = ((buildingStorey.ObjectPlacement as Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc2x3.GeometryResource.IfcPlacement).Location.Z;
                    var relatedElements = buildingStorey.ContainsElements.SelectMany(o => o.RelatedElements).Where(o =>
                    o is Xbim.Ifc2x3.SharedBldgElements.IfcWall || o is Xbim.Ifc2x3.SharedBldgElements.IfcBeam || o is Xbim.Ifc2x3.SharedBldgElements.IfcSlab || o is Xbim.Ifc2x3.SharedBldgElements.IfcColumn || o is Xbim.Ifc2x3.SharedBldgElements.IfcWindow || o is Xbim.Ifc2x3.SharedBldgElements.IfcDoor);
                    if (relatedElements.Any())
                    {
                        //找到该楼层的所有构建，找到最低的Location.Z
                        var relatedElement_z = relatedElements.Min(o => ((o.ObjectPlacement as Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc2x3.GeometryResource.IfcPlacement).Location.Z);
                        Storey_z += relatedElement_z;
                    }
                    bigStorey = StoreyDic.FirstOrDefault(o => Math.Abs(o.Item2 - Storey_z) <= 200);
                    if (bigStorey == null)
                    {
                        if (Math.Abs(Storey_z - (StoreyDic.Last().Item2 + StoreyDic.Last().Item3)) <= 200)
                        {
                            //楼层高度 = 最顶层的标高 + 最顶层的层高，说明这个是新的一层
                            var storeyNo = StoreyDic.Last().Item1 + 1;
                            StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                            bigStorey = StoreyDic.Last();
                        }
                        else if (Storey_z < StoreyDic.First().Item2)
                        {
                            var storeyNo = StoreyDic.First().Item1 - 1;
                            if (storeyNo == 0)
                            {
                                storeyNo--;
                            }
                            StoreyDic.Insert(0, (storeyNo, Storey_z, StoreyDic.First().Item2 - Storey_z).ToTuple());
                            bigStorey = StoreyDic.First();
                        }
                        else if (Storey_z > (StoreyDic.Last().Item2 + StoreyDic.Last().Item3))
                        {
                            var storeyNo = StoreyDic.Last().Item1 + 1;
                            StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                            bigStorey = StoreyDic.Last();
                        }
                        else
                        {
                            bigStorey = StoreyDic.FirstOrDefault(o => Storey_z - o.Item2 > -200);
                        }
                    }
                }
                var storeyName = bigStorey.Item1.ToString().Replace('-', 'B');
                var storey = archBuildings.BuildingStoreys.FirstOrDefault(o => o.Name == storeyName) as Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey;
                if (storey == null)
                {
                    //storey = buildingStorey.CloneAndCreateNew(archModel, archBuildings, storeyName);
                    continue;
                }
                var CreatWalls = new List<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                foreach (var spatialStructure in buildingStorey.ContainsElements)
                {
                    {
                        //var elements = spatialStructure.RelatedElements;
                        //var walls = elements.OfType<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                        //var wall = walls.FirstOrDefault();
                        ////示例： 一个墙最终表达到Viewer的坐标。 是自己的坐标 + wall_Location + Storey_Location
                        //var wall_z = ((wall.ObjectPlacement as Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc2x3.GeometryResource.IfcPlacement).Location.Z;
                    }
                    var elements = spatialStructure.RelatedElements;
                    var walls = elements.OfType<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                    foreach (var wall in walls)
                    {
                        var newWall = wall.CloneAndCreateNew(archModel);
                        CreatWalls.Add(newWall);
                    }
                }
                using (var txn = archModel.BeginTransaction("relContainEntitys2Storey"))
                {
                    //for ifc2x3
                    var relContainedIn = archModel.Instances.New<Xbim.Ifc2x3.ProductExtension.IfcRelContainedInSpatialStructure>();
                    storey.ContainsElements.Append<Xbim.Ifc2x3.Interfaces.IIfcRelContainedInSpatialStructure>(relContainedIn);

                    relContainedIn.RelatingStructure = storey;
                    relContainedIn.RelatedElements.AddRange(CreatWalls);
                    txn.Commit();
                }
            }

            //返回
            return archModel;
        }

        public IfcStore ModelMerge4(IfcStore archModel, Xbim.Ifc4.Kernel.IfcProject struProject)
        {
            var archProject = archModel.Instances.FirstOrDefault<Xbim.Ifc2x3.Kernel.IfcProject>();

            var archBuildings = archProject.Sites.FirstOrDefault()?.Buildings.FirstOrDefault() as Xbim.Ifc2x3.ProductExtension.IfcBuilding;
            var struBuildings = struProject.Sites.FirstOrDefault()?.Buildings.FirstOrDefault();

            //处理95%
            var StoreyDic = new List<Tuple<int, double, double>>();
            foreach (Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey BuildingStorey in archBuildings.BuildingStoreys)
            {
                double Storey_Elevation = BuildingStorey.Elevation.Value;
                double Storey_Height = double.Parse(((BuildingStorey.PropertySets.FirstOrDefault().PropertySetDefinitions.FirstOrDefault() as Xbim.Ifc2x3.Kernel.IfcPropertySet).HasProperties.FirstOrDefault(o => o.Name == "Height") as Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue).NominalValue.Value.ToString());
                StoreyDic.Add((int.Parse(KeepNum(BuildingStorey.Name.ToString())), Storey_Elevation, Storey_Height).ToTuple());
            }
            StoreyDic = StoreyDic.OrderBy(x => x.Item1).ToList();

            //处理5%
            foreach (Xbim.Ifc4.ProductExtension.IfcBuildingStorey buildingStorey in struBuildings.BuildingStoreys)
            {
                var bigStorey = StoreyDic.FirstOrDefault(o => o.Item1.ToString() == buildingStorey.Name);
                if (bigStorey == null)
                {
                    var Storey_z = ((buildingStorey.ObjectPlacement as Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc4.GeometryResource.IfcPlacement).Location.Z;
                    var relatedElements = buildingStorey.ContainsElements.SelectMany(o => o.RelatedElements).Where(o =>
                    o is Xbim.Ifc4.SharedBldgElements.IfcWall || o is Xbim.Ifc4.SharedBldgElements.IfcBeam || o is Xbim.Ifc4.SharedBldgElements.IfcSlab || o is Xbim.Ifc4.SharedBldgElements.IfcColumn || o is Xbim.Ifc4.SharedBldgElements.IfcWindow || o is Xbim.Ifc4.SharedBldgElements.IfcDoor);
                    if (relatedElements.Any())
                    {
                        //找到该楼层的所有构建，找到最低的Location.Z
                        var relatedElement_z = relatedElements.Min(o => ((o.ObjectPlacement as Xbim.Ifc4.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc4.GeometryResource.IfcPlacement).Location.Z);
                        Storey_z += relatedElement_z;
                    }
                    bigStorey = StoreyDic.FirstOrDefault(o => Math.Abs(o.Item2 - Storey_z) <= 200);
                    if (bigStorey == null)
                    {
                        if (Math.Abs(Storey_z - (StoreyDic.Last().Item2 + StoreyDic.Last().Item3)) <= 200)
                        {
                            //楼层高度 = 最顶层的标高 + 最顶层的层高，说明这个是新的一层
                            var storeyNo = StoreyDic.Last().Item1 + 1;
                            StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                            bigStorey = StoreyDic.Last();
                        }
                        else if (Storey_z < StoreyDic.First().Item2)
                        {
                            var storeyNo = StoreyDic.First().Item1 - 1;
                            if (storeyNo == 0)
                            {
                                storeyNo--;
                            }
                            StoreyDic.Insert(0, (storeyNo, Storey_z, StoreyDic.First().Item2 - Storey_z).ToTuple());
                            bigStorey = StoreyDic.First();
                        }
                        else if (Storey_z > (StoreyDic.Last().Item2 + StoreyDic.Last().Item3))
                        {
                            var storeyNo = StoreyDic.Last().Item1 + 1;
                            StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                            bigStorey = StoreyDic.Last();
                        }
                        else
                        {
                            bigStorey = StoreyDic.FirstOrDefault(o => Storey_z - o.Item2 > -200);
                        }
                    }
                }
                var storeyName = bigStorey.Item1.ToString().Replace('-', 'B');
                var storey = archBuildings.BuildingStoreys.FirstOrDefault(o => StoreyCompare(o.Name, storeyName)) as Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey;
                if (storey == null)
                {
                    //storey = buildingStorey.CloneAndCreateNew(archModel, archBuildings, storeyName);
                    continue;
                }
                var CreatWalls = new List<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                foreach (var spatialStructure in buildingStorey.ContainsElements)
                {
                    {
                        //var elements = spatialStructure.RelatedElements;
                        //var walls = elements.OfType<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                        //var wall = walls.FirstOrDefault();
                        ////示例： 一个墙最终表达到Viewer的坐标。 是自己的坐标 + wall_Location + Storey_Location
                        //var wall_z = ((wall.ObjectPlacement as Xbim.Ifc2x3.GeometricConstraintResource.IfcLocalPlacement).RelativePlacement as Xbim.Ifc2x3.GeometryResource.IfcPlacement).Location.Z;
                    }
                    var elements = spatialStructure.RelatedElements;
                    var walls = elements.OfType<Xbim.Ifc4.SharedBldgElements.IfcWall>();
                    foreach (var wall in walls)
                    {
                        var newWall = ThIFC42IFC2x3Factory.CloneAndCreateNew(wall, archModel);
                        CreatWalls.Add(newWall);
                    }
                }
                using (var txn = archModel.BeginTransaction("relContainEntitys2Storey"))
                {
                    //for ifc2x3
                    var relContainedIn = archModel.Instances.New<Xbim.Ifc2x3.ProductExtension.IfcRelContainedInSpatialStructure>();
                    storey.ContainsElements.Append<Xbim.Ifc2x3.Interfaces.IIfcRelContainedInSpatialStructure>(relContainedIn);

                    relContainedIn.RelatingStructure = storey;
                    relContainedIn.RelatedElements.AddRange(CreatWalls);
                    txn.Commit();
                }
            }

            //返回
            return archModel;
        }

        public IfcStore ModelMerge(IfcStore model, ThTCHProjectData tchProject)
        {
            var bigProject = model.Instances.FirstOrDefault<Xbim.Ifc2x3.Kernel.IfcProject>();
            var bigBuildings = bigProject.Sites.FirstOrDefault()?.Buildings.FirstOrDefault() as Xbim.Ifc2x3.ProductExtension.IfcBuilding;
            //处理95%
            List<Tuple<int, double, double>> StoreyDic = new List<Tuple<int, double, double>>();
            foreach (Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey BuildingStorey in bigBuildings.BuildingStoreys)
            {
                double Storey_Elevation = BuildingStorey.Elevation.Value;
                double Storey_Height = double.Parse(((BuildingStorey.PropertySets.FirstOrDefault().PropertySetDefinitions.FirstOrDefault() as Xbim.Ifc2x3.Kernel.IfcPropertySet).HasProperties.FirstOrDefault(o => o.Name == "Height") as Xbim.Ifc2x3.PropertyResource.IfcPropertySingleValue).NominalValue.Value.ToString());
                StoreyDic.Add((int.Parse(KeepNum(BuildingStorey.Name.ToString())), Storey_Elevation, Storey_Height).ToTuple());
            }
            StoreyDic = StoreyDic.OrderBy(x => x.Item1).ToList();
            //处理5%
            foreach (var buildingStorey in tchProject.Site.Buildings.First().Storeys)
            {
                var Storey_z = buildingStorey.Elevation;
                var bigStorey = StoreyDic.FirstOrDefault(o => Math.Abs(o.Item2 - Storey_z) <= 200);
                if (bigStorey == null)
                {
                    if (Math.Abs(Storey_z - (StoreyDic.Last().Item2 + StoreyDic.Last().Item3)) <= 200)
                    {
                        //楼层高度 = 最顶层的标高 + 最顶层的层高，说明这个是新的一层
                        var storeyNo = StoreyDic.Last().Item1 + 1;
                        StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                        bigStorey = StoreyDic.Last();
                    }
                    else if (Storey_z < StoreyDic.First().Item2)
                    {
                        var storeyNo = StoreyDic.First().Item1 - 1;
                        if (storeyNo == 0)
                        {
                            storeyNo--;
                        }
                        StoreyDic.Insert(0, (storeyNo, Storey_z, StoreyDic.First().Item2 - Storey_z).ToTuple());
                        bigStorey = StoreyDic.First();
                    }
                    else if (Storey_z > (StoreyDic.Last().Item2 + StoreyDic.Last().Item3))
                    {
                        var storeyNo = StoreyDic.Last().Item1 + 1;
                        StoreyDic.Add((storeyNo, Storey_z, 0.0).ToTuple());
                        bigStorey = StoreyDic.Last();
                    }
                    else
                    {
                        bigStorey = StoreyDic.FirstOrDefault(o => Storey_z - o.Item2 > -200);
                    }
                }
                var storeyName = bigStorey.Item1.ToString().Replace('-', 'B');
                var storey = bigBuildings.BuildingStoreys.FirstOrDefault(o => StoreyCompare(o.Name, storeyName)) as Xbim.Ifc2x3.ProductExtension.IfcBuildingStorey;
                if (storey == null)
                {
                    buildingStorey.Number = storeyName;
                    //BuildingStorey.Properties["FloorNo"] = storeyName;
                    //BuildingStorey.Properties["StdFlrNo"] = storeyName;
                    //storey = ThTGL2IFC2x3Factory.CreateStorey(model, bigBuildings, BuildingStorey);
                }
                var CreatWalls = new List<Xbim.Ifc2x3.SharedBldgElements.IfcWall>();
                var CreatSlabs = new List<Xbim.Ifc2x3.SharedBldgElements.IfcSlab>();
                var CreatBeams = new List<Xbim.Ifc2x3.SharedBldgElements.IfcBeam>();
                var CreatColumns = new List<Xbim.Ifc2x3.SharedBldgElements.IfcColumn>();
                var floor_origin = buildingStorey.Origin;
                foreach (var thtchwall in buildingStorey.Walls)
                {
                    var wall = ThProtoBuf2IFC2x3Factory.CreateWall(model, thtchwall, buildingStorey);
                    CreatWalls.Add(wall);
                }

                using (var txn = model.BeginTransaction("relContainEntitys2Storey"))
                {
                    //for ifc2x3
                    var relContainedIn = model.Instances.New<Xbim.Ifc2x3.ProductExtension.IfcRelContainedInSpatialStructure>();
                    storey.ContainsElements.Append<Xbim.Ifc2x3.Interfaces.IIfcRelContainedInSpatialStructure>(relContainedIn);

                    relContainedIn.RelatingStructure = storey;
                    relContainedIn.RelatedElements.AddRange(CreatWalls);
                    relContainedIn.RelatedElements.AddRange(CreatSlabs);
                    relContainedIn.RelatedElements.AddRange(CreatBeams);
                    relContainedIn.RelatedElements.AddRange(CreatColumns);
                    txn.Commit();
                }
            }

            //返回
            return model;
        }

        private string KeepNum(string str)
        {
            return str.Replace("F", "");
        }

        private bool StoreyCompare(IfcLabel? str1, string str2)
        {
            return str1 == str2 || str1 == str2 + "F";
        }
    }
}
