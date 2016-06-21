using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors {
    public class Solution {
        public string Name { get; }

        public string Description { get; }

        public bool MultiAppliable { get; set; }

        [CanBeNull]
        private readonly Action _action;

        [CanBeNull]
        private readonly Func<Task> _asyncAction;

        public Solution(string name, Action action) {
            Name = name;
            Description = null;
            _action = action;
        }

        public Solution(string name, string description, Action action) {
            Name = name;
            Description = description;
            _action = action;
        }

        public Solution(string name, string description, Func<Task> action) {
            Name = name;
            Description = description;
            _asyncAction = action;
        }

        public Task Run() {
            if (_action != null) {
                _action.Invoke();
            } else if (_asyncAction != null) {
                return _asyncAction();
            }

            return Task.Delay(0);
        }

        public override string ToString() {
            return Name;
        }
    }
}
