using System;

namespace AcManager.Tools.AcErrors {
    public class Solution {
        public string Name { get; }

        public string Description { get; }

        public Action Action { get; }

        public Solution(string name, Action action) {
            Name = name;
            Description = null;
            Action = action;
        }

        public Solution(string name, string description, Action action) {
            Name = name;
            Description = description;
            Action = action;
        }

        public override string ToString() {
            return Name;
        }
    }
}
