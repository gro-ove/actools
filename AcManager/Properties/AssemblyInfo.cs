using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Markup;
using System.Windows.Media;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Content Manager")]
[assembly: AssemblyDescription("Custom launcher and content manager for Assetto Corsa")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("AcClub")]
[assembly: AssemblyProduct("Content Manager")]
[assembly: AssemblyCopyright("Copyright © AcClub, 2015-2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("ab00175b-c2d3-49bb-9f90-2ddb27d1bac5")]

// required to support per-monitor DPI awareness in Windows 8.1+
// see also https://mui.codeplex.com/wikipage?title=Per-monitor%20DPI%20awareness
[assembly: DisableDpiAwareness]

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
[assembly: AssemblyVersion("0.7.428.9565")]
[assembly: AssemblyFileVersion("0.7.428.9565")]

[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.About")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.About")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.AcSettings")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Dialogs")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Drive")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Lists")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Miscellaneous")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Selected")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.ServerPreset")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Settings")]
[assembly: XmlnsDefinition("http://acstuff.ru/app", "AcManager.Pages.Windows")]
[assembly: XmlnsPrefix("http://acstuff.ru/app", "g")]

[assembly: NeutralResourcesLanguage("en-US")]