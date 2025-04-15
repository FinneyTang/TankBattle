using System.Collections.Generic;
using AI.Base;

namespace AI.GOAP
{
    public class Planner
    {
        private readonly IAgent m_Agent;
        private readonly List<GOAPAction> m_AvailableActions = new List<GOAPAction>();

        public Planner(IAgent agent)
        {
            m_Agent = agent;
        }
        public Planner AddAction(GOAPAction action)
        {
            action.Agent = m_Agent;
            m_AvailableActions.Add(action);
            return this;
        }
        
        private class PlanNode
        {
            public PlanNode Parent;
            public WorldState State;
            public float Cost;
            public GOAPAction Action;

            public PlanNode(PlanNode parent, float cost, WorldState state, GOAPAction action)
            {
                this.Parent = parent;
                this.Cost = cost;
                this.State = state;
                this.Action = action;
            }
        }
        
        private readonly List<GOAPAction> m_WorkingAvailableActions = new List<GOAPAction>();
        private readonly Queue<PlanNode> m_OpenNodes = new Queue<PlanNode>();
        private readonly List<PlanNode> m_CandidatePlans = new List<PlanNode>();
            
        public List<GOAPAction> Plan(WorldState currentState, WorldState goalState, List<GOAPAction> plan = null)
        {
            plan ??= new List<GOAPAction>();
            plan.Clear();
            
            m_WorkingAvailableActions.Clear();
            m_WorkingAvailableActions.AddRange(m_AvailableActions);
            m_OpenNodes.Clear();
            m_CandidatePlans.Clear();
            
            //add the goal state to the open nodes
            m_OpenNodes.Enqueue(new PlanNode(null, 0, goalState, null));
            
            //use dfs to find a plan
            while (m_OpenNodes.Count > 0)
            {
                var node = m_OpenNodes.Dequeue();
                if (currentState.IsContains(node.State))
                { 
                    m_CandidatePlans.Add(node); //plan found
                    continue;
                }
                //check all available actions to see if they can satisfy the preconditions of the node
                for (int i = m_WorkingAvailableActions.Count - 1; i >= 0; i--)
                {
                    var action = m_WorkingAvailableActions[i];
                    var newCurrentState = currentState.Clone().Merge(action.Effects);
                    if (newCurrentState.IsContains(node.State))
                    {
                        m_OpenNodes.Enqueue(
                            new PlanNode(node, node.Cost + action.Cost, action.Preconditions, action));
                        m_WorkingAvailableActions.Remove(action);
                    }
                }
            }
            
            //find the cheapest plan
            PlanNode cheapestNode = null;
            float cheapestCost = float.MaxValue;
            foreach (var candidate in m_CandidatePlans)
            {
                if (candidate.Cost < cheapestCost)
                {
                    cheapestCost = candidate.Cost;
                    cheapestNode = candidate;
                }
            }
            
            //extract the plan from the node
            while (cheapestNode != null)
            {
                if (cheapestNode.Action != null)
                {
                    plan.Add(cheapestNode.Action);
                }
                cheapestNode = cheapestNode.Parent;
            }
            return plan;
        }
    }
}