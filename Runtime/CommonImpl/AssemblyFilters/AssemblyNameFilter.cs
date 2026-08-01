using System;
using System.Collections.Generic;
using System.Reflection;

namespace UJect.Init.Reflection
{
    public class AssemblyNameFilter : IAssemblyFilter
    {
        private readonly HashSet<string> assemblyNamesToSkip;
        private readonly HashSet<string> assemblyNamePrefixesToSkip;

        public AssemblyNameFilter(HashSet<string> assemblyNamesToSkip, HashSet<string> assemblyNamePrefixesToSkip)
        {
            this.assemblyNamesToSkip = assemblyNamesToSkip;
            this.assemblyNamePrefixesToSkip = assemblyNamePrefixesToSkip;
        }
        
        public bool ShouldProcessAssembly(Assembly assembly)
        {
            var assemblyName = assembly.GetName().Name;
            if (assemblyNamesToSkip.Contains(assemblyName)) return false;
            foreach (var assemblyNamePrefix in assemblyNamePrefixesToSkip)
            {
                if (assemblyName.StartsWith(assemblyNamePrefix, StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }
}