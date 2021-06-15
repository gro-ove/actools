// https://www.nuget.org/packages/UkooLabs.FbxSharpie/
// https://github.com/UkooLabs/FBXSharpie
// License: MIT

using System.Collections.Generic;
using System.Linq;
using AcTools.ExtraKn5Utils.FbxUtils.Tokens.Value;

namespace AcTools.ExtraKn5Utils.FbxUtils {
    /// <summary>
    /// Base class for nodes and documents
    /// </summary>
    public abstract class FbxNodeList {
        public readonly List<FbxNode> Nodes = new List<FbxNode>();

        /// <summary>
        /// Add node to node array
        /// </summary>
        /// <param name="node"></param>
        public void AddNode(FbxNode node) {
            Nodes.Add(node);
        }

        /// <summary>
        /// Gets a named child node
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The child node, or null</returns>
        public FbxNode[] this[string name] {
            get { return Nodes.Where(n => n != null && n.Identifier.Value == name).ToArray(); }
        }

        /// <summary>
        /// Gets a children nodes, using name
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The children nodes array</returns>
        public FbxNode[] GetChildren(string name) {
            return this[name];
        }

        /// <summary>
        /// Gets a child node, using a '/' separated path
        /// </summary>
        /// <param name="path"></param>
        /// <returns>The child node, or null</returns>
        public FbxNode GetRelative(string path) {
            var tokens = path.Split('/');
            FbxNodeList n = this;
            foreach (var t in tokens) {
                if (t == "") {
                    continue;
                }

                n = n[t].FirstOrDefault();
                if (n == null) {
                    break;
                }
            }
            return n as FbxNode;
        }

        public FbxNode GetNode(string type, long id) {
            return (from node in Nodes
                where node != null
                select IsRequiredNode(node) ? node : node.GetNode(type, id))
                    .FirstOrDefault(found => found != null);

            bool IsRequiredNode(FbxNode node) {
                if (node.Identifier.Value == type) {
                    foreach (var property in node.Properties) {
                        if (property is LongToken longToken && id == longToken.Value) {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}