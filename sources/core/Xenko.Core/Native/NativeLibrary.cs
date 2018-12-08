// Copyright (c) Xenko contributors (https://xenko.com) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
#pragma warning disable SA1307 // Accessible fields must begin with upper-case letter
#pragma warning disable SA1310 // Field names must not contain underscore
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Xenko.Core
{
    public static class NativeLibrary
    {
        private static readonly Dictionary<string, IntPtr> LoadedLibraries = new Dictionary<string, IntPtr>();

#if XENKO_PLATFORM_WINDOWS_DESKTOP
        [DllImport("kernel32", EntryPoint = "LoadLibrary", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", EntryPoint = "FreeLibrary", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int FreeLibrary(IntPtr libraryHandle);
#endif

        /// <summary>
        /// Try to preload the library.
        /// This is useful when we want to have AnyCPU .NET and CPU-specific native code.
        /// Only available on Windows for now.
        /// </summary>
        /// <param name="libraryName">Name of the library.</param>
        /// <param name="owner">Assembly whose location is related to the native library.</param>
        /// <exception cref="System.InvalidOperationException">Library could not be loaded.</exception>
        public static void PreloadLibrary(string libraryName, Assembly owner)
        {
#if XENKO_PLATFORM_WINDOWS_DESKTOP
            lock (LoadedLibraries)
            {
                // If already loaded, just exit as we want to load it just once
                var libraryNameNormalized = libraryName.ToLowerInvariant();
                if (LoadedLibraries.ContainsKey(libraryNameNormalized))
                {
                    return;
                }

                string cpu;
                SYSTEM_INFO systemInfo;
                GetNativeSystemInfo(out systemInfo);

                if (systemInfo.processorArchitecture == PROCESSOR_ARCHITECTURE.PROCESSOR_ARCHITECTURE_ARM)
                    cpu = "ARM";
                else
                    cpu = IntPtr.Size == 8 ? "x64" : "x86";

                // We are trying to load the dll from a shadow path if it is already registered, otherwise we use it directly from the folder
                var dllFolder = NativeLibraryInternal.GetShadowPathForNativeDll(libraryName);
                if (dllFolder == null)
                    dllFolder = Path.Combine(Path.GetDirectoryName(owner.Location), cpu);
                if (!Directory.Exists(dllFolder))
                    dllFolder = Path.Combine(Environment.CurrentDirectory, cpu);
                var libraryFilename = Path.Combine(dllFolder, libraryName);
                var result = LoadLibrary(libraryFilename);

                if (result == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Could not load native library {libraryName} from path [{libraryFilename}] using CPU architecture {cpu}.");
                }

                LoadedLibraries.Add(libraryName.ToLowerInvariant(), result);
            }
#endif
        }

        /// <summary>
        /// UnLoad a specific native dynamic library loaded previously by <see cref="LoadLibrary" />.
        /// </summary>
        /// <param name="libraryName">Name of the library to unload.</param>
        public static void UnLoad(string libraryName)
        {
#if XENKO_PLATFORM_WINDOWS_DESKTOP
            lock (LoadedLibraries)
            {
                var libName = libraryName.ToLowerInvariant();

                IntPtr libHandle;
                if (LoadedLibraries.TryGetValue(libName, out libHandle))
                {
                    FreeLibrary(libHandle);
                    LoadedLibraries.Remove(libName);
                }
            }
#endif
        }

        /// <summary>
        /// UnLoad all native dynamic library loaded previously by <see cref="LoadLibrary"/>.
        /// </summary>
        public static void UnLoadAll()
        {
#if XENKO_PLATFORM_WINDOWS_DESKTOP
            lock (LoadedLibraries)
            {
                foreach (var libraryItem in LoadedLibraries)
                {
                    FreeLibrary(libraryItem.Value);
                }
                LoadedLibraries.Clear();
            }
#endif
        }

#if XENKO_PLATFORM_WINDOWS_DESKTOP
        private const string SYSINFO_FILE = "kernel32.dll";

        [DllImport(SYSINFO_FILE)]
        private static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public PROCESSOR_ARCHITECTURE processorArchitecture;
            private ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        private enum PROCESSOR_ARCHITECTURE : ushort
        {
            PROCESSOR_ARCHITECTURE_AMD64 = 9,
            PROCESSOR_ARCHITECTURE_ARM = 5,
            PROCESSOR_ARCHITECTURE_IA64 = 6,
            PROCESSOR_ARCHITECTURE_INTEL = 0,
            PROCESSOR_ARCHITECTURE_UNKNOWN = 0xffff,
        }
#endif
    }
}
