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
    using Controls;
    using EpicOnlineServices.Utility;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Utility;
    using Config = EpicOnlineServices.Config;

    [Serializable]
    public class CreatePackageWindow : EOSEditorWindow
    {
        private const string DefaultPackageDescription = "etc/PackageConfigurations/eos_package_description.json";
        
        [RetainPreference("ShowAdvanced")]
        private bool _showAdvanced = false;

        // TODO: Re-enable the following fields once their values are actually utilized
        //       in the package creation process.
        //[RetainPreference("CleanBeforeCreate")]
        //private bool _cleanBeforeCreate = true;

        //[RetainPreference("IgnoreGitWhenCleaning")]
        //private bool _ignoreGitWhenCleaning = true;

        private PackagingConfig _packagingConfig;

        private CancellationTokenSource _createPackageCancellationTokenSource;

        private bool _operationInProgress;
        private float _progress;
        private string _progressText;

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

        protected override void Teardown()
        {
            base.Teardown();

            if (_createPackageCancellationTokenSource != null)
            {
                _createPackageCancellationTokenSource.Cancel();
                _createPackageCancellationTokenSource.Dispose();
                _createPackageCancellationTokenSource = null;
            }
        }
        
        protected override void RenderWindow()
        {
            if (_operationInProgress)
            {
                GUI.enabled = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(10f);
            var outputPath = _packagingConfig.pathToOutput;
            GUIEditorUtility.AssigningTextField("Output Path", ref outputPath, 100f);
            if (GUILayout.Button("Select", GUILayout.MaxWidth(100)))
            {
                if (SelectOutputDirectory(ref outputPath))
                {
                    _packagingConfig.pathToOutput = outputPath;
                    _packagingConfig.Write();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10f);

            GUIEditorUtility.RenderFoldout(ref _showAdvanced, "Hide Advanced Options", "Show Advanced Options", RenderAdvanced);

            GUILayout.Space(10f);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            List<(string buttonLabel, UPMUtility.PackageType packageToMake, bool enableButton)> buttons = new()
            {
                ("UPM Directory", UPMUtility.PackageType.UPM,        true),
                ("UPM Tarball",   UPMUtility.PackageType.UPMTarball, true)
            };

            foreach ((string buttonLabel, UPMUtility.PackageType packageToMake, bool enabled) in buttons)
            {
                GUI.enabled = enabled && !_operationInProgress;
                if (GUILayout.Button($"Export {buttonLabel}", GUILayout.MaxWidth(200)))
                {
                    StartCreatePackageAsync(packageToMake);
                }
                GUI.enabled = _operationInProgress;
            }

            GUILayout.FlexibleSpace();
            GUILayout.Space(20f);
            GUILayout.EndHorizontal();

            /*
             * NOTES:
             *
             * There are several things here that need to be fixed:
             *
             * 1. The trade-off between how fast a package can be created and how frequently the UI is updated has
             *    not been optimized. All that is known for certain is that the UI is smooth, but ends up costing
             *    too much in overhead, ending up in slower package creation.
             * 2. For exporting a UPM Tarball, none of the progress indicators capture the work that is done to compress
             *    the output. Basically, it just shows the progress of copying the files to the temporary directory, then
             *    it will stop showing progress (appearing to be completed) when in reality the compressed tgz file will
             *    continue to be created.
             * 3. Currently, the "Clean directory", and "Ignore .git directory" options default to true, and the UI
             *    does not change the behavior.
             */

            if (_operationInProgress)
            {
                RenderProgressBar();
            }
        }

        protected void RenderProgressBar()
        {
            CancellableAsyncProgressBar.Render(
                _progress, _progressText, _createPackageCancellationTokenSource, () =>
                {
                    FileUtility.CleanDirectory(_packagingConfig.pathToOutput);
                    _progress = 0f;
                    _progressText = null;
                });
        }

        protected void RenderAdvanced()
        {
            GUILayout.BeginVertical();
            GUILayout.Space(5f);
            var jsonPackageFile = _packagingConfig.pathToJSONPackageDescription;
            
            GUILayout.BeginHorizontal();
            GUIEditorUtility.AssigningTextField("JSON Description Path", ref jsonPackageFile, 150f);
            if (GUILayout.Button("Select", GUILayout.MaxWidth(100)))
            {
                var jsonFile = EditorUtility.OpenFilePanel(
                    "Pick JSON Package Description",
                    Path.Combine(FileUtility.GetProjectPath(), Path.GetDirectoryName(DefaultPackageDescription)),
                    "json");

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

            // TODO: Utilize these values in the package creation process, and re-enable the render of the 
            //       controls that enable the user to manipulate them. For the time being the implementation
            //       is such that both options are defaulted to "true."
            //GUIEditorUtility.AssigningBoolField("Clean target directory", ref _cleanBeforeCreate, 150f,
            //    "Cleans the output target directory before creating the package.");

            //GUIEditorUtility.AssigningBoolField("Don't clean .git directory", ref _ignoreGitWhenCleaning, 150f,
            //    "When cleaning the output target directory, don't delete any .git files.");

            GUILayout.EndVertical();
            GUILayout.Space(10f);
        }

        private async void StartCreatePackageAsync(UPMUtility.PackageType type)
        {
            _createPackageCancellationTokenSource = new CancellationTokenSource();
            _operationInProgress = true;

            var progressHandler = new Progress<UPMUtility.CreatePackageProgressInfo>(value =>
            {
                var fileCountStrSize = value.TotalFilesToCopy.ToString().Length;
                string filesCopiedStrFormat = "{0," + fileCountStrSize + "}";
                var filesCopiedCountStr = String.Format(filesCopiedStrFormat, value.FilesCopied);
                var filesToCopyCountStr = String.Format(filesCopiedStrFormat, value.TotalFilesToCopy);

                _progress = value.SizeOfFilesCopied / (float)value.TotalSizeOfFilesToCopy;
                _progressText = $"{filesCopiedCountStr} out of {filesToCopyCountStr} files copied";
                Repaint();
            });

            try
            {
                string outputPath = _packagingConfig.pathToOutput;

                // if the output path is empty or doesn't exist, prompt for the user to select one
                if (string.IsNullOrEmpty(outputPath) || !Directory.Exists(outputPath))
                {
                    if (SelectOutputDirectory(ref outputPath))
                    {
                        _packagingConfig.pathToOutput = outputPath;
                        await _packagingConfig.WriteAsync();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Package Export Canceled",
                            "An output directory was not selected, so package export has been canceled.",
                            "ok");
                        return;
                    }
                }

                await UPMUtility.CreatePackage(type, progressHandler, _createPackageCancellationTokenSource.Token);

                if (EditorUtility.DisplayDialog("Package Created", "Package was successfully created",
                        "Open Output Path", "Close"))
                {
                    FileUtility.OpenFolder(outputPath);
                }
            }
            catch (OperationCanceledException ex)
            {
                _progressText = $"Operation Canceled: {ex.Message}";
            }
            finally
            {
                _operationInProgress = false;
                _progressText = "";
                _progress = 0f;
                _createPackageCancellationTokenSource?.Dispose();
                _createPackageCancellationTokenSource = null;
            }
        }
    }
}
