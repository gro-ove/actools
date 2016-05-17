using System;
using System.Collections.Generic;
using System.Windows;

namespace FirstFloor.ModernUI.Presentation {
    public class SharedResourceDictionary : ResourceDictionary {
        /// <summary>
        /// Internal cache of loaded dictionaries 
        /// </summary>
        public static Dictionary<Uri, ResourceDictionary> SharedDictionaries =
                new Dictionary<Uri, ResourceDictionary>();

        /// <summary>
        /// Local member of the source uri
        /// </summary>
        private Uri _sourceUri;

        /// <summary>
        /// Gets or sets the uniform resource identifier (URI) to load resources from.
        /// </summary>
        public new Uri Source {
            get { return _sourceUri; }
            set {
                _sourceUri = value;

                ResourceDictionary result;
                if (SharedDictionaries.TryGetValue(value, out result)) {
                    // If the dictionary is already loaded, get it from the cache
                    MergedDictionaries.Add(result);
                } else {
                    // If the dictionary is not yet loaded, load it by setting
                    // the source of the base class
                    base.Source = value;

                    // add it to the cache
                    SharedDictionaries.Add(value, this);
                }
            }
        }
    }
}
