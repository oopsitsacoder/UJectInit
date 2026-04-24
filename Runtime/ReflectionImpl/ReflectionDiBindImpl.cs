// Copyright (c) 2026 OopsItsACoder
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using UJect.Exceptions;
using UJect.Utilities;
using UnityEngine.Scripting;

namespace UJect.Init
{
    /// <summary>
    /// Helper class for running DiBind methods
    /// </summary>
    public class ReflectionDiBindImpl
    {
        private static readonly Type diContainerType = typeof(DiContainer);
        public ReflectionDiBindImpl()
        {
        }

        [Flags]
        public enum DiBindValidations : byte
        {
            /// <summary>
            /// Do no validation for BindMethods. Normally you might use this in a build for performance reasons.
            /// </summary>
            DoNothing = 0,
            /// <summary>
            /// Check that the method signature is as expected
            /// </summary>
            CheckMethodSignature = 1<<0,
            /// <summary>
            /// Check that the method being called is marked Preserve so code stripping doesn't remove it
            /// </summary>
            CheckForPreserveAttribute = 1<<1,
            All = 0xF
        }
        
        /// <summary>
        /// Collect all possible Bind methods in the current AppDoman. This can be slow on large games, and another solution might be better.
        /// </summary>
        /// <param name="bindValidations">Which Di Bind methods to run against collected methods. Defaults to <see cref="DiBindValidations.All"/>.</param>
        /// <returns>All valid DIBind method infos</returns>
        /// <exception cref="BindException">If a single Bind Exception is found during validation</exception>
        /// <exception cref="AggregateException">If multiple Bind Exceptions are found during validation</exception>
        [LibraryEntryPoint]
        public ICollection<MethodInfo> CollectBindMethods(DiBindValidations bindValidations = DiBindValidations.All)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var bindMethods = new List<MethodInfo>();

            List<BindException>? bindExceptions = null;

            foreach (var assembly in assemblies)
            {
                try
                {
                    var allTypesInAssembly = assembly.GetTypes();
                    foreach (var assemType in allTypesInAssembly)
                    {
                        var methods = assemType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                        foreach (var method in methods.OrderByDescending(m => m.GetParameters().Length))
                        {
                            var diBindAttribute = method.GetCustomAttribute<DiBindAttribute>();
                            if (diBindAttribute == null) continue; // Not a DI Bind attribute

                            if (bindValidations != DiBindValidations.DoNothing)
                            {
                                try
                                {
                                    ValidateDiBindMethod(method, bindValidations);
                                }
                                catch (BindException ex)
                                {
                                    bindExceptions ??= new();
                                    bindExceptions.Add(ex);
                                    continue;
                                }
                            }
                            bindMethods.Add(method);
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new BindException($"Failed to process assembly {assembly.FullName}", ex);
                }
            }

            if (bindExceptions?.Count > 0)
            {
                if (bindExceptions.Count == 1) throw bindExceptions[0];
                if (bindExceptions.Count > 1) throw new AggregateException(bindExceptions);
            }
            return bindMethods;
        }

        /// <summary>
        /// Execute all the method infos in <paramref name="diBindMethodInfos"/>
        /// </summary>
        /// <param name="diBindMethodInfos">Methods to execute</param>
        /// <param name="diContainer">The container to bind to</param>
        /// <param name="bindValidations">
        /// Which Di Bind methods to run against each of the <paramref name="diBindMethodInfos"/>. Defaults to <see cref="DiBindValidations.All"/>.
        /// If you used <see cref="CollectBindMethods"/> with validation, and are passing the results of that here, this can be <see cref="DiBindValidations.DoNothing"/>.
        /// </param>
        [LibraryEntryPoint]
        public void RunBindMethods(ICollection<MethodInfo> diBindMethodInfos, DiContainer diContainer, DiBindValidations bindValidations = DiBindValidations.All)
        {
            var diContainerArgArray = new object[] { diContainer };
            foreach (var bindMethodInfo in diBindMethodInfos)
            {
                if (bindValidations != DiBindValidations.DoNothing) ValidateDiBindMethod(bindMethodInfo, bindValidations);
                bindMethodInfo.Invoke(null, diContainerArgArray);
            }
        }
        
        /// <summary>
        /// Shortcut for calling <see cref="CollectBindMethods"/> and <see cref="RunBindMethods"/> in succession.
        /// </summary>
        /// <param name="diContainer">Container to bind to</param>
        [LibraryEntryPoint]
        public void CollectAndRunBindMethods(DiContainer diContainer)
        {
            var bindMethods = CollectBindMethods(DiBindValidations.All);
            
            // We can skip validations here because we already did them during CollectBindMethods
            RunBindMethods(bindMethods, diContainer, DiBindValidations.DoNothing); 
        }

        /// <summary>
        /// Validate that a given method info is a viable DI Bind method.
        /// </summary>
        /// <param name="method">The method to check</param>
        /// <param name="bindValidations"></param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="method"/> is null</exception>
        /// <exception cref="BindException">Thrown if validation fails</exception>
        [LibraryEntryPoint]
        public void ValidateDiBindMethod(MethodInfo method, DiBindValidations bindValidations) => ValidateDiBindMethodInternal(method, bindValidations);
        
        private void ValidateDiBindMethodInternal(MethodInfo method, DiBindValidations bindValidations)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            // If we're doing signature checking:
            if ((bindValidations & DiBindValidations.CheckMethodSignature) == DiBindValidations.CheckMethodSignature)
            {
                if (!method.IsStatic)
                {
                    throw new BindException($"DIBind method {method.Name} on type {method.DeclaringType?.FullName} must be static!");
                }

                var methodParams = method.GetParameters();
                if (methodParams.Length == 0) return;
                if (methodParams.Length > 1)
                {
                    throw new BindException($"DIBind method {method.Name} on type {method.DeclaringType?.FullName} has too many parameters. It should have a single {nameof(DiContainer)} parameter");
                }

                var candidateParam = methodParams[0];
                if (candidateParam.ParameterType != diContainerType)
                {
                    throw new BindException($"DIBind method {method.Name} on type {method.DeclaringType?.FullName} has too many parameters. It should have a single {nameof(DiContainer)} parameter");
                }
            }
            
            // If we're checking for a PreserveAttribute
            if ((bindValidations & DiBindValidations.CheckForPreserveAttribute) == DiBindValidations.CheckForPreserveAttribute)
            {
                bool foundPreserveAttribute = DoesMethodOrDeclaringTypeHavePreserveAttribute(method);
                if (!foundPreserveAttribute)
                    throw new BindException($"DIBind method {method.Name} on type {method.DeclaringType?.FullName} is not marked with a Preserve attribute. It may be stripped from builds!");
            }
        }

        private bool DoesMethodOrDeclaringTypeHavePreserveAttribute(MethodInfo method)
        {
            var declaringType = method.DeclaringType;
            if (declaringType != null)
            {
                var typeIsPreserved = HasPreserveAttribute(declaringType);
                if (typeIsPreserved) return true;
            }
            return HasPreserveAttribute(method);
        }
        
        private bool HasPreserveAttribute(MemberInfo? memberInfo)
        {
            if (memberInfo == null) return false;
            
            // Try well known types first, it's quicker
            if (memberInfo.GetCustomAttribute<UnityEngine.Scripting.PreserveAttribute>() != null) return true;
            if (memberInfo.GetCustomAttribute<UJect.Utilities.PreserveAttribute>() != null) return true;
            
            // Otherwise fall back to slower string comparison. Unity treats any attribute named "PreserveAttribute" as the same
            var methodAttributes = memberInfo.GetCustomAttributes(true);
            foreach (var attribute in methodAttributes)
            {
                if (string.Equals("PreserveAttribute", attribute.GetType().Name, StringComparison.Ordinal)) return true;
            }
            return false;

        }
    }
}