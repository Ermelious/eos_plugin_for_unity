/*
* Copyright (c) 2021 PlayEveryWare
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

using UnityEngine;
using UnityEditor;

// make lines a little shorter
using UPMUtility = PlayEveryWare.EpicOnlineServices.Editor.Utility.UnityPackageCreationUtility;

using System;

namespace PlayEveryWare.EpicOnlineServices.Editor.Windows
{
    using Config;
    using EpicOnlineServices.Utility;
    using NUnit.Framework;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Utility;
    using Config = EpicOnlineServices.Config;

    [Serializable]
    public class CreatePackageWindow : EOSEditorWindow
    {
        const string DEFAULT_OUTPUT_DIRECTORY = "Build";
        private const string DefaultPackageDescription = "etc/PackageConfigurations/eos_package_description.json";
        
        [RetainPreference("ShowAdvanced")]
        private bool _showAdvanced = false;

        [RetainPreference("CleanBeforeCreate")]
        private bool _cleanBeforeCreate = true;

        [RetainPreference("IgnoreGitWhenCleaning")]
        private bool _ignoreGitWhenCleaning = true;

        private PackagingConfig _packagingConfig;

        private Task _createPackageTask;
        private bool _packageCreated = false;

        [MenuItem("Tools/EOS Plugin/Create Package")]
        public static void ShowWindow()
        {
            GetWindow<CreatePackageWindow>("Create Package");
        }

        protected override async Task AsyncSetup()
        {
            _packagingConfig = await Config.Get<PackagingConfig>();

            if (string.IsNullOrEmpty(_packagingConfig.pathToJSONPackageDescription))
            {
                _packagingConfig.pathToJSONPackageDescription =
                    Path.Combine(FileUtility.GetProjectPath(), DefaultPackageDescription);
                await _packagingConfig.WriteAsync();
            }
            await base.AsyncSetup();
        }

        protected static bool SelectOutputDirectory(ref string path)
        {
            string selectedPath = EditorUtility.OpenFolderPanel(
                "Pick output directory",
                Path.GetDirectoryName(FileUtility.GetProjectPath()),
                "");

            if (string.IsNullOrEmpty(selectedPath) || !Directory.Exists(selectedPath))
            {
                return false;
            }

            path = selectedPath;
            return true;
        }

        protected override void RenderWindow()
        {
            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            var outputPath = _packagingConfig.pathToOutput;
            GUIEditorUtility.AssigningTextField("Output Path", ref outputPath);
            if (GUILayout.Button("Select", GUILayout.MaxWidth(100)))
            {
                if (SelectOutputDirectory(ref outputPath))
                {
                    _packagingConfig.pathToOutput = outputPath;
                    _packagingConfig.Write();
                }
            }

            GUILayout.Space(10f);
            GUILayout.EndHorizontal();

            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced");
            if (_showAdvanced)
            {
                RenderAdvanced();
            }

            GUILayout.Space(20f);

            GUILayout.BeginHorizontal();
            GUILayout.Space(20f);
            GUILayout.FlexibleSpace();

            if (_createPackageTask != null)
            {
                GUI.enabled = _createPackageTask.IsCompleted != false;
            }

            List<(string buttonLabel, UPMUtility.PackageType packageToMake, bool enableButton)> buttons = new()
            {
                ("UPM Directory", UPMUtility.PackageType.UPM,        true),
                ("UPM Tarball",   UPMUtility.PackageType.UPMTarball, true),
                (".unitypackage", UPMUtility.PackageType.DotUnity,   false)
            };

            foreach ((string buttonLabel, UPMUtility.PackageType packageToMake, bool enabled) in buttons)
            {
                GUI.enabled = enabled;
                if (GUILayout.Button($"Export {buttonLabel}", GUILayout.MaxWidth(200)))
                {
                    StartCreatePackageAsync(packageToMake, _cleanBeforeCreate, _ignoreGitWhenCleaning);
                }
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(20f);
            GUILayout.EndHorizontal();
        }

        protected void RenderAdvanced()
        {
            GUILayout.Space(10f);

            GUILayout.BeginVertical();
            GUIEditorUtility.AssigningBoolField("Clean target directory", ref _cleanBeforeCreate, 200f,
                "Cleans the output target directory before creating the package.");

            GUIEditorUtility.AssigningBoolField("Don't clean .git directory", ref _ignoreGitWhenCleaning, 200f, "" +
                "When cleaning the output target directory, don't delete any .git files.");

            var jsonPackageFile = _packagingConfig.pathToJSONPackageDescription;

            GUILayout.BeginHorizontal();
            GUIEditorUtility.AssigningTextField("JSON Description Path", ref jsonPackageFile);
            if (GUILayout.Button("Select", GUILayout.MaxWidth(100)))
            {
                var jsonFile = EditorUtility.OpenFilePanel("Pick JSON Package Description", "", "json");
                if (!string.IsNullOrWhiteSpace(jsonFile))
                {
                    jsonPackageFile = jsonFile;
                }
            }
            GUILayout.EndHorizontal();

            if (jsonPackageFile != _packagingConfig.pathToJSONPackageDescription)
            {
                _packagingConfig.pathToJSONPackageDescription = jsonPackageFile;
                _packagingConfig.Write(true, false);
            }

            GUILayout.EndVertical();
            GUILayout.Space(10f);
        }

        private void StartCreatePackageAsync(UPMUtility.PackageType type, bool clean, bool ignoreGit)
        {
            string outputPath = _packagingConfig.pathToOutput;

            // if the output path is empty or doesn't exist, prompt for the user to select one
            if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath))
            {
                if (SelectOutputDirectory(ref outputPath))
                {
                    _packagingConfig.pathToOutput = outputPath;
                    _packagingConfig.Write();
                }
                else
                {
                    EditorUtility.DisplayDialog("Package Export Canceled",
                        "An output directory was not selected, so package export has been canceled.",
                        "ok");
                    return;
                }
            }

            EditorApplication.update += CheckForPackageCreated;
            _createPackageTask = UPMUtility.CreatePackage(type, clean, ignoreGit);
        }

        private void CheckForPackageCreated()
        {
            if (_createPackageTask.IsCompleted && !_packageCreated)
            {
                _packageCreated = true;
                EditorApplication.update -= CheckForPackageCreated;

                // TODO: Add option to open directory.
                EditorUtility.DisplayDialog("Package Created",
                    $"Package was successfully created at \"{_packagingConfig.pathToOutput}\".",
                    "Okay");
            }
        }
    }
}
