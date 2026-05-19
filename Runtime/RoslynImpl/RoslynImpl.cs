using System;
using System.Collections.Generic;
using System.Reflection;
using UJect.Init.CommonImpl;
using UJect.Utilities;

namespace UJect.Init.Roslyn
{
    /// <summary>
    /// Helper class for running DiBind methods via Roslyn generated classes (one per assembly).
    ///
    /// UJectAnalyzers.dll will generate a class named __DiBindMethodCollection per-assembly, instances of which are then collected via reflection.
    /// Once all instances are collected, they're used to create a standard IBindMethodCollection lookup.
    /// 
    /// This can be quicker at runtime for a large number of methods, at the cost of compile time.
    ///
    /// This class assumes you have added the UJect Roslyn Analyzer to the project, AND WILL NOT WORK OTHERWISE
    /// </summary>
    public class RoslynImpl : IUJectInitImpl
    {
        [Preserve]
        public static readonly RoslynImpl Instance = new();

        private bool hasCachedMethods = false;
        private readonly List<ISourceGeneratedDiBindMethodCollection> cachedMethodCollections = new();
        private readonly Dictionary<Type, IBindMethodCollection> bindMethodCollectionsByAttributeType = new();

        private void TryInit()
        {
            if (hasCachedMethods) return;
            hasCachedMethods = true;
            
            cachedMethodCollections.Clear();
            bindMethodCollectionsByAttributeType.Clear();

            var methodListLookup = new Dictionary<Type, List<Action<DiContainer>>>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                // Try to fetch the Roslyn-generated method collection type from the assembly
                Type methodCollectionType;
                try
                {
                    methodCollectionType =  assembly.GetType("UJect.SourceGen.__DiBindMethodCollection");
                } catch
                {
                    // Couldn't access this assembly
                    continue;
                }

                // No type generated for this assembly, i.e. no Bind methods
                if (methodCollectionType == null) continue;

                // Grab the generated Instance for this assembly from the type
                var instanceField = methodCollectionType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceField == null)
                {
                    throw new InvalidOperationException("Unexpected null Instance field method");
                }

                var instanceObj = instanceField.GetValue(null);
                if (instanceObj is not ISourceGeneratedDiBindMethodCollection diBindMethodCollection)
                {
                    throw new InvalidOperationException($"Instance object does not implement interface {nameof(ISourceGeneratedDiBindMethodCollection)}");
                }
                cachedMethodCollections.Add(diBindMethodCollection);
                foreach (var byAttribute in diBindMethodCollection.MethodLookup)
                {
                    var attributeType = byAttribute.Key;
                    var assemblyAction = byAttribute.Value;

                    if (!methodListLookup.TryGetValue(attributeType, out var methodList))
                    {
                        methodList = new List<Action<DiContainer>>();
                        methodListLookup[attributeType] = methodList;
                    }
                    methodList.Add(assemblyAction);
                }
            }

            foreach (var kvp in methodListLookup)
            {
                bindMethodCollectionsByAttributeType[kvp.Key] = new ActionListBindMethodCollection(kvp.Value);
            }
        }
        
        public void RunBindMethods(DiContainer diContainer)
        {
            TryInit();
            foreach (var diBindMethodCollection in cachedMethodCollections)
            {
                diBindMethodCollection.Run(diContainer);
            }
        }

        public IReadOnlyDictionary<Type, IBindMethodCollection> CollectBindMethodsByAttributeType()
        {
            TryInit();
            return bindMethodCollectionsByAttributeType;
        }

        private class ActionListBindMethodCollection : IBindMethodCollection
        {
            private readonly List<Action<DiContainer>> actionsToRun;
            public ActionListBindMethodCollection(List<Action<DiContainer>> actionsToRun) => this.actionsToRun = actionsToRun;

            public void RunBindMethods(DiContainer diContainer)
            {
                foreach (var action in actionsToRun)
                {
                    action(diContainer);
                }
            }
        }
    }
}