// Copyright (c) 2026 OopsItsACoder
using System;
using UJect.Utilities;

namespace UJect.Init
{
    /// <summary>
    /// Attribute denoting a DI Bind method
    /// </summary>
    [LibraryEntryPoint]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class DiBindAttribute : PreserveAttribute
    {
        public DiBindAttribute()
        {
        }
    }
}