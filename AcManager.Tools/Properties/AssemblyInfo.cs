using System.Reflection;
using System.Resources;
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
[assembly: AssemblyCopyright("Copyright © AcClub, 2015-2016")]
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
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.AcManagersNew")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.AcObjectsNew")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Data")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Managers")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Helpers")]
[assembly: XmlnsDefinition("http://acstuff.ru/app/tools", "AcManager.Tools.Objects")]
[assembly: XmlnsPrefix("http://acstuff.ru/app/tools", "t")]

[assembly: NeutralResourcesLanguage("en-US")]