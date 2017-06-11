using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle ("RIS")]
[assembly: AssemblyDescription ("Race Into Space")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("")]
[assembly: AssemblyProduct ("KSPRIS")]
[assembly: AssemblyCopyright ("Copyright © 2017")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]

[assembly: Guid("aaa18268-304c-4d1a-9cee-93d1223ed52c")]

[assembly: AssemblyVersion("0.1")]
[assembly: AssemblyFileVersion("0.1.3")]

// Use KSPAssembly to allow other DLLs to make this DLL a dependency in a
// non-hacky way in KSP.  Format is (AssemblyProduct, major, minor), and it
// does not appear to have a hard requirement to match the assembly version.
[assembly: KSPAssembly("RIS", 0, 1)]
[assembly: KSPAssemblyDependency("ContractConfigurator", 1, 0)]
