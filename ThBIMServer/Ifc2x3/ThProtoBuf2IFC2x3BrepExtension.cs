using System;
using System.Linq;
using System.Collections.Generic;

using Xbim.Ifc;
using Xbim.Common.Geometry;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.TopologyResource;
using Xbim.Ifc2x3.GeometricModelResource;

namespace ThBIMServer.Ifc2x3
{
    public static class ThProtoBuf2IFC2x3BrepExtension
    {
        public static IfcFacetedBrep CreateIfcFacetedBrep(this IfcStore model, List<IXbimSolid> solids)
        {
            var NewBrep = model.Instances.New<IfcFacetedBrep>();
            var ifcClosedShell = model.Instances.New<IfcClosedShell>();
            foreach (var solid in solids)
            {
                foreach (var face in solid.Faces)
                {
                    var ifcface = model.Instances.New<IfcFace>();
                    var ifcFaceOuterBound = model.Instances.New<IfcFaceOuterBound>();
                    IfcPolyLoop ifcloop = model.Instances.New<IfcPolyLoop>();
                    foreach (var pt in face.OuterBound.Points)
                    {
                        var Newpt = model.Instances.New<IfcCartesianPoint>();
                        Newpt.SetXYZ(pt.X, pt.Y, pt.Z);
                        ifcloop.Polygon.Add(Newpt);
                    }
                    ifcFaceOuterBound.Bound = ifcloop;
                    ifcface.Bounds.Add(ifcFaceOuterBound);
                    var innerBounds = face.InnerBounds;
                    if (innerBounds != null && innerBounds.Count > 0)
                    {
                        foreach (var innerBound in innerBounds)
                        {
                            IfcPolyLoop ifcInnerloop = model.Instances.New<IfcPolyLoop>();
                            foreach (var pt in innerBound.Points)
                            {
                                var Newpt = model.Instances.New<IfcCartesianPoint>();
                                Newpt.SetXYZ(pt.X, pt.Y, pt.Z);
                                ifcInnerloop.Polygon.Add(Newpt);
                            }
                            var ifcFaceBound = model.Instances.New<IfcFaceBound>();
                            ifcFaceBound.Bound = ifcInnerloop;
                            ifcface.Bounds.Add(ifcFaceBound);
                        }
                    }

                    ifcClosedShell.CfsFaces.Add(ifcface);
                }
            }
            NewBrep.Outer = ifcClosedShell;

            return NewBrep;
        }

        public static IfcFaceBasedSurfaceModel ToIfcFaceBasedSurface(this IfcStore model, ThSUCompDefinitionData def)
        {
            var connectedFaceSet = model.Instances.New<IfcConnectedFaceSet>();
            var faceBasedSurface = model.Instances.New<IfcFaceBasedSurfaceModel>();
            foreach (var face in def.MeshFaces)
            {
                var mesh = face.Mesh;
                for (int i = 0; i < mesh.Polygons.Count; i++)
                {
                    var vertices = Vertices(mesh, mesh.Polygons[i]);
                    connectedFaceSet.CfsFaces.Add(ToIfcFace(model, vertices));
                }
            }
            faceBasedSurface.FbsmFaces.Add(connectedFaceSet);
            return faceBasedSurface;
        }

        public static IfcFacetedBrep ToIfcFacetedBrep(this IfcStore model, ThSUCompDefinitionData def)
        {
            var NewBrep = model.Instances.New<IfcFacetedBrep>();
            var ifcClosedShell = model.Instances.New<IfcClosedShell>();
            foreach (var face in def.BrepFaces)
            {
                var ifcface = model.Instances.New<IfcFace>();
                ifcface.Bounds.Add(model.ToIfcFaceOuterBound(face.OuterLoop.Points.ToList()));
                var innerBounds = face.InnerLoops;
                if (innerBounds != null && innerBounds.Count > 0)
                {
                    foreach (var innerBound in innerBounds)
                    {
                        ifcface.Bounds.Add(model.ToIfcFaceBound(innerBound.Points.ToList()));
                    }
                }

                ifcClosedShell.CfsFaces.Add(ifcface);
            }
            NewBrep.Outer = ifcClosedShell;

            return NewBrep;
        }

        private static IfcFaceOuterBound ToIfcFaceOuterBound(this IfcStore model, List<ThTCHPoint3d> vertices)
        {
            return model.Instances.New<IfcFaceOuterBound>(b =>
            {
                b.Bound = model.ToIfcPolyLoop(vertices);
            });
        }

        private static IfcFaceBound ToIfcFaceBound(this IfcStore model, List<ThTCHPoint3d> vertices)
        {
            return model.Instances.New<IfcFaceBound>(b =>
            {
                b.Bound = model.ToIfcPolyLoop(vertices);
            });
        }

        private static IfcPolyLoop ToIfcPolyLoop(this IfcStore model, List<ThTCHPoint3d> vertices)
        {
            var polyLoop = model.Instances.New<IfcPolyLoop>();
            foreach (ThTCHPoint3d v in vertices)
            {
                polyLoop.Polygon.Add(model.ToIfcCartesianPoint(v));
            }
            return polyLoop;
        }

        private static List<ThTCHPoint3d> Vertices(ThSUPolygonMesh mesh, ThSUPolygon polygon)
        {
            List<ThTCHPoint3d> vertices = new List<ThTCHPoint3d>();
            for (int i = 0; i < polygon.Indices.Count; i++)
            {
                vertices.Add(mesh.Points[Math.Abs(polygon.Indices[i]) - 1]);
            }
            return vertices;
        }

        private static IfcFace ToIfcFace(this IfcStore model, List<ThTCHPoint3d> vertices)
        {
            var ifcFace = model.Instances.New<IfcFace>();
            ifcFace.Bounds.Add(ToIfcFaceBound(model, vertices));
            return ifcFace;
        }
    }
}
