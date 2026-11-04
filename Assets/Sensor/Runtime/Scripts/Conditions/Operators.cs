using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>An operator over a list of conditions, evaluated top to bottom.</summary>
    [Serializable]
    public abstract class MultiCondition : Condition
    {
        [SerializeReference] public List<Condition> Children = new List<Condition>();

        protected override bool IsOperator => true;

        public override string Summary => Children == null || Children.Count == 0 ? "Empty" : $"{Children.Count} conditions";

        public override void CollectChildren(List<Condition> children)
        {
            if (Children == null) return;

            foreach (Condition child in Children)
            {
                if (child != null) children.Add(child);
            }
        }
    }

    /// <summary>Passes when every child passes. Stops at the first failure. Empty is false.</summary>
    [Serializable, ConditionMenu("Operators/And")]
    public class And : MultiCondition
    {
        public override string DisplayName => "AND";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            bool hasChild = false;
            foreach (Condition child in Children)
            {
                if (child == null) continue;

                hasChild = true;
                if (!Run(child, target, context)) return false;
            }
            return hasChild;
        }
    }

    /// <summary>Passes when any child passes. Stops at the first pass. Empty is false.</summary>
    [Serializable, ConditionMenu("Operators/Or")]
    public class Or : MultiCondition
    {
        public override string DisplayName => "OR";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            foreach (Condition child in Children)
            {
                if (child != null && Run(child, target, context)) return true;
            }
            return false;
        }
    }

    /// <summary>Passes when its child fails. Empty is false.</summary>
    [Serializable, ConditionMenu("Operators/Not")]
    public class Not : Condition
    {
        [SerializeReference] public Condition Child;

        public override string DisplayName => "NOT";

        protected override bool IsOperator => true;

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            return Child != null && !Run(Child, target, context);
        }

        public override void CollectChildren(List<Condition> children)
        {
            if (Child != null) children.Add(Child);
        }
    }
}
