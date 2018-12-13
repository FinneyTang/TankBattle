using AI.Grid;
using UnityEngine;

namespace AI.InfluenceMap
{
    public class InfluenceMap2D : Grid2D<float>
    {
        public InfluenceMap2D(int row, int column, float gridWidth, float gridLength, Vector3 gridOrig)
            : base(row, column, gridWidth, gridLength, gridOrig)
        {
        }
    }
}
