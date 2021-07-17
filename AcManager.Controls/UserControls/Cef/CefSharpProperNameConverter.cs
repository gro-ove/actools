using System.Reflection;
using CefSharp.JavascriptBinding;

namespace AcManager.Controls.UserControls.Cef {
    internal class CefSharpProperNameConverter : IJavascriptNameConverter {
        public string ConvertToJavascript(MemberInfo memberInfo) {
            if (memberInfo.Name == "ToString" || memberInfo.Name == "GetHashCode") return null;
            return memberInfo.Name;
        }

        public string ConvertReturnedObjectPropertyAndFieldToNameJavascript(MemberInfo memberInfo) {
            if (memberInfo.Name == "ToString" || memberInfo.Name == "GetHashCode") return null;
            return memberInfo.Name;
        }
    }
}