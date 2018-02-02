using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Markup;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AcManager.Tools")]
[assembly: AssemblyDescription("Tools library for Content Manager")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("AcClub")]
[assembly: AssemblyProduct("AcManager.Tools")]
[assembly: AssemblyCopyright("Copyright © AcClub, 2015-2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("259338d6-cb86-4912-9834-d4d0848d6e58")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.1.4830")]
[assembly: AssemblyFileVersion("1.0.1.4830")]

[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.AcManagersNew")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.AcObjectsNew")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.ContentInstallation")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Data")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.GameProperties")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers.AcLog")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers.AcSettings")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers.DirectInput")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers.PresetsPerMode")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Managers")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Managers.Plugins")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Managers.Online")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Miscellaneous")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Objects")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Profile")]
[assembly: XmlnsPrefix("http://acstuff.ru/app/tools", "t")]

[assembly: NeutralResourcesLanguage("en-US")]

// For testing
[assembly: InternalsVisibleTo("AcManager.Tools.Tests")]
// Modified at: 18/02/02 23:43:08