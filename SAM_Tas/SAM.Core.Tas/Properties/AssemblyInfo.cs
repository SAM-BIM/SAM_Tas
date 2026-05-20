using System.Reflection;

// AssemblyVersion declarations live here because the project sets
// <GenerateAssemblyInfo>false</GenerateAssemblyInfo>, which makes MSBuild ignore the
// csproj's <AssemblyVersion> property. Without these attributes the resulting .dll
// reports version 0.0.0.0, which breaks compile-time assembly identity checks
// (e.g. error CS1705 in consumers that reference the dll with SpecificVersion=False).
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.*")]
