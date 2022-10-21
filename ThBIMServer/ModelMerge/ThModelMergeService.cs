using System;
using System.Linq;
using System.Collections.Generic;

using ThMEPIFC.Ifc2x3;

using Xbim.Ifc;
using Xbim.Ifc4.MeasureResource;

namespace ThBIMServer.ModelMerge
{
    public class ThModelMergeService
    {
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
