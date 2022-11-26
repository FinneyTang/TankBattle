using System.Collections;
using System.Collections.Generic;

namespace decisionTree {
    public class orNode : dtNode
    {
        public orNode() : base() { }
        public orNode(List<dtNode> children) : base(children) { }

        public override nodeResult evaluate()
        {
            foreach (var item in children)
            {
                switch (item.evaluate()) {
                    case nodeResult.SUCCESS:
                        Res = nodeResult.SUCCESS;
                        return Res;
                    case nodeResult.FAIILURE:
                        continue;
                    default:
                        continue;
                }


            }

            Res = nodeResult.FAIILURE;
            return Res;
        }
    }
}
