using System;

using Xbim.Ifc;
using Xbim.Ifc2x3.UtilityResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;

namespace ThBIMServer.Deduct
{
    public static class ThDeductWallRelationCreater
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
                var body = ThDeductFactory.ToIfcRepresentationItem(model, struWall);
                ret.Representation = ThDeductFactory.CreateProductDefinitionShape(model, body);

                //object placement
                ret.ObjectPlacement = ThDeductFactory.ToIfcLocalPlacement(model, struWall.ObjectPlacement, measure);

                txn.Commit();
                return ret;
            }
        }

        public static void BuildRelationship(this IfcStore model, IfcWall archWall, IfcWall struWall, IfcOpeningElement hole)
        {
            using (var txn = model.BeginTransaction("Create Hole Relation"))
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
    }
}
