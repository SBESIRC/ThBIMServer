using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer
{
    public class IfcInsertCopyService
    {
        private const string inserted = "D:\\项目\\三维平台\\测试图\\ifc\\建筑结构合并.ifc";
        public void InsertCopy()
        {
            PropertyTranformDelegate semanticFilter = (property, parentObject) =>
            {
                ////leave out geometry and placement
                //if (parentObject is IfcProduct &&
                //    (property.PropertyInfo.Name == nameof(IfcProduct.Representation) ||
                //    property.PropertyInfo.Name == nameof(IfcProduct.ObjectPlacement)))
                //    return null;

                ////leave out mapped geometry
                //if (parentObject is IfcTypeProduct &&
                //     property.PropertyInfo.Name == nameof(IfcTypeProduct.RepresentationMaps))
                //    return null;

                ////only bring over IsDefinedBy and IsTypedBy inverse relationships which will take over all properties and types
                //if (property.EntityAttribute.Order < 0 && !(
                //    property.PropertyInfo.Name == nameof(IfcProduct.IsDefinedBy) ||
                //    property.PropertyInfo.Name == nameof(IfcProduct.IsTypedBy)))
                //    return null;

                return property.PropertyInfo.GetValue(parentObject, null);
            };

            //Console.WriteLine("请输入ifc文件路径：");
            var archPath = "D:\\项目\\三维平台\\测试图\\ifc\\建筑1012.ifc";
            var struPath = "D:\\项目\\三维平台\\测试图\\ifc\\TH000000_标准测试项目_1#楼_结构_IFC模型.ifc";
            //using (var archModel = IfcStore.Open(archPath))
            //using (var struModel = IfcStore.Open(struPath))
            //{
            //    var service = new ThModelMergeService();
            //    var iModel = service.ModelMerge(archModel, struModel);
            //    iModel.SaveAs(inserted);
            //}

            using (var iModel = IfcStore.Create(IfcSchemaVersion.Ifc2X3, XbimStoreType.InMemoryModel))
            {
                using (var txn = iModel.BeginTransaction("Insert copy"))
                {
                    using (var model = IfcStore.Open(archPath))
                    {
                        var walls = model.Instances.OfType<IfcWall>();
                        //single map should be used for all insertions between two models
                        var map = new XbimInstanceHandleMap(model, iModel);

                        foreach (var wall in walls)
                        {
                            iModel.InsertCopy(wall, map, semanticFilter, true, false);
                        }
                    }

                    using (var model = IfcStore.Open(struPath))
                    {
                        var walls = model.Instances.OfType<IfcWall>();
                        //single map should be used for all insertions between two models
                        var map = new XbimInstanceHandleMap(model, iModel);

                        foreach (var wall in walls)
                        {
                            iModel.InsertCopy(wall, map, semanticFilter, false, false);
                        }
                    }

                    txn.Commit();
                }

                iModel.SaveAs(inserted);
            }
        }
    }
}
