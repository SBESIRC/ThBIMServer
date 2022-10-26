using System;
using System.Linq;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Geometries.Prepared;
using Xbim.Ifc2x3.ProfileResource;

namespace ThBIMServer.NTS
{
    public class ThIFCNTSSpatialIndex : IDisposable
    {
        private STRtree<Geometry> Engine { get; set; }
        private Dictionary<IfcProfileDef, Geometry> Geometries { get; set; }
        private Lookup<Geometry, IfcProfileDef> GeometryLookup { get; set; }
        public bool AllowDuplicate { get; set; }
        public bool PrecisionReduce { get; set; }

        private ThIFCNTSSpatialIndex()
        {

        }

        public ThIFCNTSSpatialIndex(List<IfcProfileDef> profiles, bool precisionReduce = false, bool allowDuplicate = false)
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

        private List<IfcProfileDef> CrossingFilter(List<IfcProfileDef> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Intersects(preparedGeometry, o)).ToList();
        }

        private List<IfcProfileDef> FenceFilter(List<IfcProfileDef> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Intersects(preparedGeometry, o)).ToList();
        }

        private List<IfcProfileDef> WindowFilter(List<IfcProfileDef> objs, IPreparedGeometry preparedGeometry)
        {
            return objs.Where(o => Contains(preparedGeometry, o)).ToList();
        }

        private bool Contains(IPreparedGeometry preparedGeometry, IfcProfileDef entity)
        {
            return preparedGeometry.Contains(ToNTSGeometry(entity));
        }

        public bool Intersects(IfcProfileDef entity, bool precisely = false)
        {
            var geometry = ToNTSPolygonalGeometry(entity);
            var queriedObjs = Query(geometry.EnvelopeInternal);

            if (precisely == false)
            {
                return queriedObjs.Count > 0;
            }

            var preparedGeometry = ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry);
            var hasIntersection = queriedObjs.Any(o => Intersects(preparedGeometry, o));
            return hasIntersection;
        }

        private bool Intersects(IPreparedGeometry preparedGeometry, IfcProfileDef entity)
        {
            return preparedGeometry.Intersects(ToNTSGeometry(entity));
        }

        private Geometry ToNTSGeometry(IfcProfileDef obj)
        {
            using (var ov = new ThIFCNTSFixedPrecision(PrecisionReduce))
            {
                return obj.ToNTSGeometry();
            }
        }

        private Polygon ToNTSPolygonalGeometry(IfcProfileDef obj)
        {
            using (var ov = new ThIFCNTSFixedPrecision(PrecisionReduce))
            {
                return obj.ToNTSPolygon();
            }
        }

        /// <summary>
        /// 更新索引
        /// </summary>
        /// <param name="adds"></param>
        /// <param name="removals"></param>
        public void Update(List<IfcProfileDef> adds, List<IfcProfileDef> removals)
        {
            // 添加新的对象
            adds.ForEach(o =>
            {
                if (!Geometries.ContainsKey(o))
                {
                    Geometries[o] = o.ToNTSPolygon();
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
            GeometryLookup = (Lookup<Geometry, IfcProfileDef>)Geometries.ToLookup(p => p.Value, p => p.Key);
            foreach (var item in GeometryLookup)
            {
                Engine.Insert(item.Key.EnvelopeInternal, item.Key);
            }
        }

        /// <summary>
        /// 重置索引
        /// </summary>
        public void Reset(List<IfcProfileDef> profiles)
        {
            Geometries = new Dictionary<IfcProfileDef, Geometry>();
            Update(profiles, new List<IfcProfileDef>());
        }

        /// <summary>
        /// Crossing selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<IfcProfileDef> SelectCrossingPolygon(IfcProfileDef entity)
        {
            var geometry = ToNTSPolygonalGeometry(entity);
            return CrossingFilter(
                Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        /// <summary>
        /// Window selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<IfcProfileDef> SelectWindowPolygon(IfcProfileDef entity)
        {
            var geometry = ToNTSPolygonalGeometry(entity);
            return WindowFilter(Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        /// <summary>
        /// Fence Selection
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public List<IfcProfileDef> SelectFence(IfcProfileDef entity)
        {
            var geometry = ToNTSGeometry(entity);
            return FenceFilter(Query(geometry.EnvelopeInternal),
                ThIFCNTSService.Instance.PreparedGeometryFactory.Create(geometry));
        }

        public List<IfcProfileDef> SelectAll()
        {
            var objs = new List<IfcProfileDef>();
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

        public List<IfcProfileDef> Query(Envelope envelope)
        {
            var objs = new List<IfcProfileDef>();
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
