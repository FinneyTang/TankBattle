using System.Collections;
using System.Collections.Generic;
using ZJQ;


namespace decisionTree {
    public enum nodeResult {
        SUCCESS,
        FAIILURE
    }


    public class dtNode
    {
        protected nodeResult Res;

        public dtNode parent;
        protected List<dtNode> children = new List<dtNode>();

        public dtNode() {
            parent = null;
        }

        public dtNode(List<dtNode> childNodes)
        {
            foreach (dtNode item in childNodes)
            {
                attach(item);
            }

        }

        public void attach(dtNode node)
        {
            node.parent = this;
            children.Add(node);
        }

        

        public virtual nodeResult evaluate() => nodeResult.FAIILURE;




    }
}
