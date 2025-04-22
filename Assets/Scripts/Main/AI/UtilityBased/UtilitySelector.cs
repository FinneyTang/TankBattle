using System;
using AI.Base;
using UnityEngine;

namespace AI.UtilityBased
{
    public enum ESelResult
    {
        First, Second
    }
    public abstract class Selector
    {
        public float Sel(float a, float b, out ESelResult ret)
        {
            if (DoCompare(a, b))
            {
                ret = ESelResult.First;
                return a;
            }
            else
            {
                ret = ESelResult.Second;
                return b;
            }
        }
        protected abstract bool DoCompare(float a, float b);
    }
    public class MaxSelector : Selector
    {
        protected override bool DoCompare(float a, float b)
        {
            return a > b ? true : false;
        }
    }
    public static class UtilitySelector
    {
        public static int Select(IAgent agent, Selector sel, params Utility[] us)
        {
            if(us.Length == 0)
            {
                return -1;
            }
            var finalValue = us[0].CalcU(agent);
            var selIndex = 0;
            for(int i = 1; i < us.Length; ++i)
            {
                finalValue = sel.Sel(finalValue, us[i].CalcU(agent), out var ret);
                if(ret == ESelResult.Second)
                {
                    selIndex = i;
                }
            }
            if(Mathf.Abs(finalValue) <= Mathf.Epsilon)
            {
                return -1;
            }
            return selIndex;
        }
    }
}
