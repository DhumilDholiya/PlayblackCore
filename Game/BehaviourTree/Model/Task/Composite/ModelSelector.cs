﻿using Playblack.BehaviourTree.Execution.Core;
using Playblack.BehaviourTree.Execution.Task.Composite;
using Playblack.BehaviourTree.Model.Core;

namespace Playblack.BehaviourTree.Model.Task.Composite {
    public class ModelSelector : ModelComposite {

        public ModelSelector(ModelTask guard, params ModelTask[] children) : base(guard, children) { }
        // reflection ctor
        public ModelSelector() : base() { }

        public override ExecutionTask CreateExecutor(IBTExecutor btExecutor, ExecutionTask parent) {
            return new ExecutionSelector(this, btExecutor, parent);
        }
    }
}
