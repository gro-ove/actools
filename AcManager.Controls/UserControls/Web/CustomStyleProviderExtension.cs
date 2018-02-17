using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Controls.UserControls.Web {
    internal static class CustomStyleProviderExtension {
        [CanBeNull, ContractAnnotation(@"provider:null => null; url:null => null")]
        public static string ToScript([CanBeNull] this ICustomStyleProvider provider, [CanBeNull] string url) {
            if (provider == null || url == null) return null;

            var style = provider.GetStyle(url);
            if (style == null) return null;

            return $@"
var s = document.getElementById('__cm_style');
if (s && s.parentNode) s.parentNode.removeChild(s);
s = document.createElement('style');
s.id = '__cm_style';
s.innerHTML = {JsonConvert.SerializeObject(style)};
if (document.body){{
    document.body.appendChild(s);
}} else if (document.head){{
    var p = document.createElement('style');
    p.innerHTML = 'body{{display:none!important}}html{{background:black!important}}'
    document.head.appendChild(p);

    function onload(){{
        if (s.parentNode == document.head){{
            document.head.removeChild(p);
            document.head.removeChild(s);
            document.body.appendChild(s);
        }}
    }}

    document.head.appendChild(s);
    document.addEventListener('DOMContentLoaded', onload, false);
    window.addEventListener('load', onload, false);
}}";
        }
    }
}