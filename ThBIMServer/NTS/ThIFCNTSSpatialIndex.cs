using System;
using System.Linq;
using System.Collections.Generic;

using Xbim.Ifc2x3.ProfileResource;
using Xbim.Ifc2x3.GeometryResource;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Geometries.Prepared;

namespace ThBIMServer.NTS
{
    public class ThIFCNTSSpatialIndex : IDisposable
    {
        private STRtree<Geometry> Engine { get; set; }
        private Dictionary<Tuple<IfcProfileDef, IfcAxis2Placement>, Geometry> Geometries { get; set; }
        private Lookup<Geometry, Tuple<IfcProfileDef, IfcAxis2Placement>> GeometryLookup { get; set; }
        public bool AllowDuplicate { get; set; }
        public bool PrecisionReduce { get; set; }

        private ThIFCNTSSpatialIndex()
        {

        }

        public ThIFCNTSSpatialIndex(List<Tuple<IfcProfileDef, IfcAxis2Placement>> profiles, bool precisionReduce = false, bool allowDuplicate = false)
        {
            // 默认使用固定精度
            PrecisionReduce = precisionReduce;
            // 默认忽略重复图元
            AllowDuplicate = allowDuplicate;

            Reset(profiles);
        }

        public void Dispose()
        {
            Geometries.Clear();
            Geometries = null;
            GeometryLookup = null;
            Engine = null;
        }

        private List<Tuple<IfcProfileDef, IfcAxis2Placement>> CrossingFilter(List<Tuple<IfcProfileDef, IfcAxis2Placement>> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Intersects(preparedGeometry, o)).ToList();
        }

        private List<Tuple<IfcProfileDef, IfcAxis2Placement>> FenceFilter(List<Tuple<IfcProfileDef, IfcAxis2Placement>> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Intersects(preparedGeometry, o)).ToList();
        }

        private List<Tuple<IfcProfileDef, IfcAxis2Placement>> WindowFilter(List<Tuple<IfcProfileDef, IfcAxis2Placement>> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Contains(preparedGeometry, o)).ToList();
        }

        private bool Contains(IPreparedGeometry preparedGeometry, Tuple<IfcProfileDef, IfcAxis2Placement> entity)
        {
            return preparedGeometry.Contains(ToNTSGeometry(entity.Item1, entity.Item2));
        }

        public bool Intersects(Tuple<IfcProfileDef, IfcAxis2Placement> entity, bool precisely = false)
        {
            var geometry = ToNTSPolygonalGeometry(entity.Item1, entity.Item2);
            var queriedObjs = Query(geometry.EnvelopeInternal);

            if (precisely == false)
            {
                return queriedObjs.Count > 0;
            }

            var preparedGeometry = ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry);
            var hasIntersection = queriedObjs.Any(o => Intersects(preparedGeometry, o));
            return hasIntersection;
        }

        private bool Intersects(IPreparedGeometry preparedGeometry, Tuple<IfcProfileDef, IfcAxis2Placement> entity)
        {
            return preparedGeometry.Intersects(ToNTSGeometry(entity.Item1, entity.Item2));
        }

        private Geometry ToNTSGeometry(IfcProfileDef obj, IfcAxis2Placement placement)
        {
            using (var ov = new ThIFCNTSFixedPrecision(PrecisionReduce))
            {
                return obj.ToNTSGeometry(placement);
            }
        }

        private Polygon ToNTSPolygonalGeometry(IfcProfileDef obj, IfcAxis2Placement placement)
        {
            using (var ov = new ThIFCNTSFixedPrecision(PrecisionReduce))
            {
                return obj.ToNTSPolygon(placement);
            }
        }

        /// <summary>
        /// 更新索引
        /// </summary>
        /// <param name="adds"></param>
        /// <param name="removals"></param>
        public void Update(List<Tuple<IfcProfileDef, IfcAxis2Placement>> adds, List<Tuple<IfcProfileDef, IfcAxis2Placement>> removals)
        {
            // 添加新的对象
            adds.ForEach(o =>
            {
                if (!Geometries.ContainsKey(o))
                {
                    Geometries[o] = o.Item1.ToNTSPolygon(o.Item2);
                }
            });

            // 移除删除对象
            removals.ForEach(o =>
            {
                if (Geometries.ContainsKey(o))
                {
                    Geometries.Remove(o);
                }
            });

            // 创建新的索引
            Engine = new STRtree<Geometry>();
            GeometryLookup = (Lookup<Geometry, Tuple<IfcProfileDef, IfcAxis2Placement>>)Geometries.ToLookup(p => p.Value, p => p.Key);
            foreach (var item in GeometryLookup)
            {
                Engine.Insert(item.Key.EnvelopeInternal, item.Key);
            }
        }

        /// <summary>
        /// 重置索引
        /// </summary>
        public void Reset(List<Tuple<IfcProfileDef, IfcAxis2Placement>> profiles)
        {
            Geometries = new Dictionary<Tuple<IfcProfileDef, IfcAxis2Placement>, Geometry>();
            Update(profiles, new List<Tuple<IfcProfileDef, IfcAxis2Placement>>());
        }

        /// <summary>
        /// Crossing selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<Tuple<IfcProfileDef, IfcAxis2Placement>> SelectCrossingPolygon(Tuple<IfcProfileDef, IfcAxis2Placement> entity)
        {
            var geometry = ToNTSPolygonalGeometry(entity.Item1, entity.Item2);
            return CrossingFilter(
                Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        /// <summary>
        /// Window selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<Tuple<IfcProfileDef, IfcAxis2Placement>> SelectWindowPolygon(Tuple<IfcProfileDef, IfcAxis2Placement> entity)
        {
            var geometry = ToNTSPolygonalGeometry(entity.Item1, entity.Item2);
            return WindowFilter(Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        /// <summary>
        /// Fence Selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<Tuple<IfcProfileDef, IfcAxis2Placement>> SelectFence(Tuple<IfcProfileDef, IfcAxis2Placement> entity)
        {
            var geometry = ToNTSGeometry(entity.Item1, entity.Item2);
            return FenceFilter(Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        public List<Tuple<IfcProfileDef, IfcAxis2Placement>> SelectAll()
        {
            var objs = new List<Tuple<IfcProfileDef, IfcAxis2Placement>>();
            foreach (var item in GeometryLookup)
            {
                if (AllowDuplicate)
                {
                    foreach (var e in item)
                    {
                        objs.Add(e);
                    }
                }
                else
                {
                    objs.Add(item.First());
                }
            }

            return objs;
        }

        public List<Tuple<IfcProfileDef, IfcAxis2Placement>> Query(Envelope envelope)
        {
            var objs = new List<Tuple<IfcProfileDef, IfcAxis2Placement>>();
            var results = Engine.Query(envelope).ToList();
            foreach (var item in GeometryLookup.Where(o => results.Contains(o.Key)))
            {
                if (AllowDuplicate)
                {
                    foreach (var e in item)
                    {
                        objs.Add(e);
                    }
                }
                else
                {
                    objs.Add(item.First());
                }
            }
            return objs;
        }
    }
}
