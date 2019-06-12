using System.Collections.Generic;
using UnityEngine;

namespace AI.Grid
{
    class Grid2DDict<T> : Grid2DBase<T>
    {
        private Dictionary<int, T> m_Grids;
        public Grid2DDict(int row, int column, float gridWidth, float gridLength, Vector3 gridOrig)
            : base(row, column, gridWidth, gridLength, gridOrig)
        {
            m_Grids = new Dictionary<int, T>();
        }
        protected override void SetInternal(int x, int y, T value)
        {
            m_Grids[CoordKey(x, y)] = value;
        }
        protected override T GetInternal(int x, int y)
        {
            T v;
            if (m_Grids.TryGetValue(CoordKey(x, y), out v))
            {
                return v;
            }
            return default(T);
        }
        private int CoordKey(int x, int y)
        {
            return y * m_Column + x;
        }
        public override void Clear()
        {
            m_Grids.Clear();
        }
    }
}
