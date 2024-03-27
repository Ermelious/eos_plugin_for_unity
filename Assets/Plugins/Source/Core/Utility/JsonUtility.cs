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

namespace PlayEveryWare.EpicOnlineServices.Utility
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Schema;
    using System;
    using System.IO;

    public static class JsonUtility
    {
        /// <summary>
        /// Label used in Json to indicate the schema to use for validation purposes.
        /// </summary>
        private const string SchemaLabel = "$schema";

        /// <summary>
        /// The directory (relative to the project root) that contains the Json schemas for validation.
        /// </summary>
        private const string SchemaDirectory = "etc/config/schemas/";

        /// <summary>
        /// Determines if a given json object has a local schema that can be used for
        /// validation purposes.
        /// </summary>
        /// <param name="obj">The json object.</param>
        /// <returns>True if there is a local schema, false otherwise.</returns>
        private static bool HasLocalSchema(JObject obj)
        {
            return (obj[SchemaLabel] != null && FileUtility.IsLocalPath(obj[SchemaLabel].ToString()));
        }

        public static string GetSchemaFilePath(string fullyQualifiedPath)
        {
            string schemaFileName = Path.GetFileName(fullyQualifiedPath)
                .Replace("eos_plugin_", "")
                .Replace("_config", "");

            return Path.Combine(SchemaDirectory, schemaFileName);
        }
        public static string ToJson(object obj, bool pretty = false)
        {
            return UnityEngine.JsonUtility.ToJson(obj, pretty);
        }

        public static T FromJson<T>(string json)
        {
            return UnityEngine.JsonUtility.FromJson<T>(json);
        }

        public static void FromJsonOverwrite(string json, object obj)
        {
            UnityEngine.JsonUtility.FromJsonOverwrite(json, obj);
        }


    }
}    