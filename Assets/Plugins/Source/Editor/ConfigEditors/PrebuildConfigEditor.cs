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

namespace PlayEveryWare.EpicOnlineServices.Editor
{
    using Config;
    using Utility;

    public class PrebuildConfigEditor : ConfigEditor<PrebuildConfig>
    {
        public PrebuildConfigEditor() : base("Prebuild Settings") { }

        public override void RenderContents()
        {
            GUIEditorUtility.AssigningBoolField("Use Unity App Version for the EOS product version",
                ref config.useAppVersionAsProductVersion);

            GUIEditorUtility.AssigningBoolField("Use EOS Bootstrapper", 
                ref config.useEOSBootstrapper,
                tooltip: "If false, then the EOSBootstrapper.exe is not included in the build, and therefore no Epic Games Overlay will be available. If you only want to use Easy Anti Cheat you can disable this setting. This currently only relates to Windows builds, as that is the only platform that uses the EOSBootstrapper.");
        }
    }
}