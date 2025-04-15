using System.Collections.Generic;
using AI.Base;
using UnityEngine;

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
            public WorldState GoalState;
            public float Cost;
            public GOAPAction Action;

            public PlanNode(PlanNode parent, float cost, WorldState goalState, GOAPAction action)
            {
                this.Parent = parent;
                this.Cost = cost;
                this.GoalState = goalState;
                this.Action = action;
            }
        }
        
        private readonly Stack<PlanNode> m_OpenNodes = new Stack<PlanNode>();
        private readonly List<PlanNode> m_CandidatePlans = new List<PlanNode>();
            
        public List<GOAPAction> Plan(WorldState currentState, WorldState goalState, List<GOAPAction> plan = null)
        {
            plan ??= new List<GOAPAction>();
            plan.Clear();
            
            m_OpenNodes.Clear();
            m_CandidatePlans.Clear();
            
            //add the goal state to the open nodes
            m_OpenNodes.Push(new PlanNode(null, 0, goalState, null));
            
            //use dfs to find a plan
            while (m_OpenNodes.Count > 0)
            {
                var node = m_OpenNodes.Pop();
                if (currentState.IsSatisfied(node.GoalState))
                { 
                    m_CandidatePlans.Add(node); //plan found
                    continue;
                }
                
                //check all available actions to see if they can satisfy the preconditions of the node
                foreach (var action in m_AvailableActions)
                {
                    //check if the action is already in the plan
                    if (IsActionExisted(node, action))
                    {
                        continue;
                    }
                    var newCurrentState = currentState.Clone().Merge(action.Effects);
                    if (newCurrentState.IsSatisfied(node.GoalState))
                    {
                        m_OpenNodes.Push(
                            new PlanNode(node, node.Cost + action.Cost, action.Preconditions, action));
                    }
                }
            }
            return SelectCheapestPlan(plan, m_CandidatePlans);
        }

        private List<GOAPAction> SelectCheapestPlan(List<GOAPAction> plan, List<PlanNode> candidatePlans)
        {
            //find the cheapest plan
            PlanNode cheapestNode = null;
            var cheapestCost = float.MaxValue;
            foreach (var candidate in candidatePlans)
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
        private bool IsActionExisted(PlanNode node, GOAPAction action)
        {
            var actionExisted = false;
            while (node != null)
            {
                if (node.Action != action)
                {
                    node = node.Parent;
                }
                else
                {
                    actionExisted = true;
                    break;
                }
            }
            return actionExisted;
        }
    }
}