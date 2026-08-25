using System;
using System.Collections.Generic;

namespace UJect.Init
{
    public delegate void RunDiBindMethod(DiContainer DiContainer);
    
    public interface IDiBindMethodCollection
    {
        /// <summary>
        /// Lookup of Attribute type to a <see cref="Action"/>
        /// </summary>
        IReadOnlyDictionary<Type, RunDiBindMethod> MethodLookup { get; }
        void Run(UJect.DiContainer diContainer);
    }
}