namespace AI.BehaviourTree
{
    public class DecoratorNode : Node
    {
        public Node Child
        {
            get
            {
                return m_Children[0];
            }
        }
        public DecoratorNode(Node child)
        {
            AddChild(child);
        }
    }
}
