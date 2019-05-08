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
        public void AddInfluenceSource(Vector3 pos, float centralInfluenceValue = 100, float attenuation = 5)
        {
            Vector3 centerPos = pos;
            centerPos.y = 0;
            int rowPropagation = m_Row;
            int colPropagation = m_Column;
            //recalculate
            if(Mathf.Abs(attenuation) > Mathf.Epsilon)
            {
                rowPropagation = (int)(centralInfluenceValue / attenuation / m_GridLength) + 1;
                colPropagation = (int)(centralInfluenceValue / attenuation / m_GridWidth)  + 1;
            }
            IteratorGrid(centerPos, colPropagation, rowPropagation, (float value, int centerX, int centerY, int curX, int curY) =>
            {
                Vector3 gridPos = Vector3.zero;
                if(GridCoordToPos(curX, curY, ref gridPos) == false)
                {
                    return;
                }
                float dist = Vector3.Distance(centerPos, gridPos);
                float infValue = centralInfluenceValue - dist * attenuation;
                Set(curX, curY, Mathf.Max(0, value + infValue));
            });
        }
    }
}
