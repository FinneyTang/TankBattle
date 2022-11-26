using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using decisionTree;

public class rootNode :dtNode
{
    public rootNode() : base() { }

    public dtNode childNode = null;

    public override nodeResult evaluate()
    {
        return childNode.evaluate();
    }
}
