using System;
using UnityEngine;

namespace AI.Grid
{
    public class Grid2D<T> : Grid2DBase<T>
    {
        private T[,] m_Grids;
        public Grid2D(int row, int column, float gridWidth, float gridLength, Vector3 gridOrig)
            : base(row, column, gridWidth, gridLength, gridOrig)
        {
            m_Grids = new T[row, column];
        }
        protected override void SetInternal(int x, int y, T value)
        {
            m_Grids[x, y] = value;
        }
        protected override T GetInternal(int x, int y)
        {
            return m_Grids[x, y];
        }
        protected override void Clear()
        {
            Array.Clear(m_Grids, 0, m_Grids.Length);
        }
    }
}
