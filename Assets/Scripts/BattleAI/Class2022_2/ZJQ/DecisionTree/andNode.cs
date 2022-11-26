using System.Collections;
using System.Collections.Generic;

namespace decisionTree {
    public class andNode : dtNode
    {
        public andNode() : base() { }
        public andNode(List<dtNode> children) : base(children) { }

        public override nodeResult evaluate()
        {
            foreach (var item in children)
            {
                switch (item.evaluate())
                {
                    case nodeResult.SUCCESS:
                        continue;
                    case nodeResult.FAIILURE:
                        Res = nodeResult.FAIILURE;
                        return Res;
                    default:
                        Res = nodeResult.SUCCESS;
                        return Res;
                }


            }

            Res = nodeResult.SUCCESS;
            return Res;
        }
    } 
}
