using System;
using System.Collections.Generic;

namespace UJect.Init
{
    public interface ISourceGeneratedDiBindMethodCollection
    {
        IReadOnlyDictionary<Type, Action<UJect.DiContainer>> MethodLookup { get; }
        void Run(UJect.DiContainer diContainer);
    }
}