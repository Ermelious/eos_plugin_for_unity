/*
 * Copyright (c) 2024 PlayEveryWare
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace PlayEveryWare.EpicOnlineServices.Build
{
    using Editor.Config;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEngine;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using Debug = UnityEngine.Debug;
    using UnityEditor;
    using PlayEveryWare.EpicOnlineServices.Editor;
    using Utility;

    public abstract class PlatformSpecificBuilder : IPlatformSpecificBuilder
    {
        /// <summary>
        /// For every platform, there are certain binary files that represent native console-specific implementations of the EOS plugin.
        /// Each of these binaries maps to a solution file that must be compiled in order for the project to function properly on the
        /// platform. This dictionary stores the relationship between the fully-qualified path to the project file (.sln or Makefile
        /// depending on the platform) and the fully-qualified path to the binary that is expected.
        /// </summary>
        private IDictionary<string, string[]> _projectFileToBinaryFilesMap;

        /// <summary>
        /// Fully-qualified path to the directory that should contain the output from the native code.
        /// </summary>
        private readonly string _nativeCodeOutputDirectory;

        /// <summary>
        /// Fully-qualified path to the directory containing all the native code solution directories.
        /// </summary>
        private readonly static string NativeCodeDirectory = Path.Combine(Application.dataPath, "../lib/NativeCode");

        /// <summary>
        /// Constructs a new PlatformSpecificBuilder script.
        /// </summary>
        /// <param name="nativeCodeOutputDirectory">The filepath to the location of the binary files, relative to the Assets directory.</param>
        protected PlatformSpecificBuilder(string nativeCodeOutputDirectory)
        {
            _nativeCodeOutputDirectory = Path.Combine(Application.dataPath, nativeCodeOutputDirectory);
            _projectFileToBinaryFilesMap = new Dictionary<string, string[]>();
        }

        /// <summary>
        /// Adds a mapping of solution file to expected binary file output.
        /// </summary>
        /// <param name="projectFile">Path of the project file relative to the NativeCode directory (lib/NativeCode).</param>
        /// <param name="binaryFiles">Paths of any expected binary files, relative to the native code output directory defined for the builder.</param>
        protected void AddProjectFileToBinaryMapping(string projectFile, params string[] binaryFiles)
        {
            string fullyQualifiedOutputPath = Path.Combine(Application.dataPath, _nativeCodeOutputDirectory);

            string[] fullyQualifiedBinaryPaths = new string[binaryFiles.Length];
            for (int i = 0; i < binaryFiles.Length; i++)
            {
                fullyQualifiedBinaryPaths[i] = Path.Combine(fullyQualifiedOutputPath, binaryFiles[i]);
            }

            _projectFileToBinaryFilesMap.Add(Path.Combine(NativeCodeDirectory, projectFile), fullyQualifiedBinaryPaths);
        }

        /// <summary>
        /// Implement this function on a per-platform basis to provide custom logic for the platform being compiled.
        /// Any overriding implementations should first call the base implementation.
        /// </summary>
        /// <param name="report"></param>
        public virtual void PreBuild(BuildReport report)
        {
            // Check to make sure that the platform configuration exists
            CheckPlatformConfiguration();

            // Configure the version numbers per user defined preferences
            ConfigureVersion();

            // Check to make sure that the binaries for the platform exist, and build them if necessary.
            CheckPlatformBinaries();
        }

        /// <summary>
        /// Implement this function on a per-platform basis to provide custom logic for the platform being compiled.
        /// Any overriding implementations should first call the base implementation.
        /// </summary>
        /// <param name="report"></param>
        public virtual void PostBuild(BuildReport report)
        {
            // The only standalone platforms that are supported are WIN/OSX/Linux
            if (IsStandalone())
            {
                // Configure easy-anti-cheat.
                EACUtility.ConfigureEAC(report).Wait();
            }
        }

        public virtual void BuildNativeCode()
        {
            BuildUtility.BuildNativeBinaries(_projectFileToBinaryFilesMap, _nativeCodeOutputDirectory, true);
        }

        /// <summary>
        /// Check for platform specific binaries. If this method is overridden, be sure to start by calling the
        /// base implementation, because it will check for the presence of config files, and handle checking for
        /// native code and compiling it for you, and you can then add additional checks in the overriden implementation.
        /// </summary>
        protected virtual void CheckPlatformBinaries()
        {
            BuildUtility.FindVSInstallations();

            Debug.Log("Checking for platform-specific prerequisites.");

            // Validate the configuration for the platform
            // TODO-RELEASE: When in UPM form - this fails when you try and build for the first time, but 
            //               subsequent builds don't fail on the configuration being missing, instead they
            //               fail at a later point.
            BuildUtility.ValidatePlatformConfiguration();

            // Build any native libraries that need to be built for the platform
            // TODO: Consider having the "rebuild" be a setting users can determine.
            BuildNativeCode();

            // Validate that the binaries built are now in the correct location
            // TODO-RELEASE: When in UPM form - this fails because the project files do not exist.
            ValidateNativeBinaries();
        }

        /// <summary>
        /// Checks to make sure that the platform configuration file exists where it is expected to be
        /// TODO: Add configuration validation.
        /// </summary>
        private static void CheckPlatformConfiguration()
        {
            string configFilePath = PlatformManager.GetConfigFilePath();
            if (!File.Exists(configFilePath))
            {
                throw new BuildFailedException($"Expected config file \"{configFilePath}\" for platform {PlatformManager.GetFullName(PlatformManager.CurrentPlatform)} does not exist.");
            }
        }

        /// <summary>
        /// Completes all configuration tasks.
        /// </summary>
        private static void ConfigureVersion()
        {
            AutoSetProductVersion();

            const string packageVersionPath = "Assets/Resources/eosPluginVersion.asset";
            string packageVersion = EOSPackageInfo.GetPackageVersion();
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            TextAsset versionAsset = new(packageVersion);
            AssetDatabase.CreateAsset(versionAsset, packageVersionPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Determines whether the Application Version is supposed to be used as the product version, and (if so) sets it accordingly.
        /// </summary>
        private static async void AutoSetProductVersion()
        {
            var eosConfig = await Config.Get<EOSConfig>();
            var prebuildConfig = await Config.Get<PrebuildConfig>();
            var previousProdVer = eosConfig.productVersion;

            if (prebuildConfig.useAppVersionAsProductVersion)
            {
                eosConfig.productVersion = Application.version;
            }

            if (previousProdVer != eosConfig.productVersion)
            {
                await eosConfig.WriteAsync(true);
            }
        }

        /// <summary>
        /// Checks to see that native code for the platform has been compiled.
        /// Will list all missing files in the error log before throwing an exception.
        /// </summary>
        /// <exception cref="BuildFailedException">Will be thrown if any expected output binary file is missing.</exception>
        private void ValidateNativeBinaries()
        {
            bool prerequisitesSatisfied = true;

            foreach (string projectFile in _projectFileToBinaryFilesMap.Keys)
            {
                foreach (string outputFile in _projectFileToBinaryFilesMap[projectFile])
                {
                    // skip if the output file exists
                    if (File.Exists(outputFile)) continue;

                    // make sure to log all the missing files / project file pairs before throwing an exception
                    Debug.LogError($"Required file \"{outputFile}\" which is output from project file \"{projectFile}\" is missing.");
                    prerequisitesSatisfied = false;
                }
            }

            if (!prerequisitesSatisfied)
            {
                throw new BuildFailedException($"Prerequisites for platform were not met. View logs for details.");
            }
        }

        /// <summary>
        /// When building on Windows, msbuild has a flag specifying the platform to build towards. Each
        /// class that derives from PlatformSpecificBuilder must define the value to pass msbuild for it's
        /// respective platform. These strings can be confidential on unreleased or code-named platforms,
        /// so it is important for security reasons that only implementing classes contain the value.
        /// </summary>
        /// <returns>The appropriate string to pass to msbuild.</returns>
        public virtual string GetPlatformString()
        {
            return string.Empty;
        }

        /// <summary>
        /// Determines if the build is a standalone build.
        /// </summary>
        /// <returns>True if the build is standalone, false otherwise.</returns>
        protected bool IsStandalone()
        {
            // It is unclear from the Unity documentation what the meaning of "UNITY_STANDALONE" is,
            // although it can be reasonably inferred from context that it will be defined if any
            // of the following specific standalone scripting defines exist, for the sake of future-
            // proofing the scenario where a new standalone platform is introduced, each of the three
            // standalone platforms that the EOS Plugin current supports are explicitly checked here.
#if UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN
            return true;
#else
            return false;
#endif
        }
    }
}