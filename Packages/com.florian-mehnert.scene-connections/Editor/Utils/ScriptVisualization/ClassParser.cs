using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SceneConnections.Editor.Utils.ScriptVisualization
{
    public static class ClassParser
    {
        private static readonly ThreadLocal<HashSet<string>> UsingStatements = new(() => new HashSet<string>());

        /// <summary>
        /// Gets all Class References within a directory and below given as string <see cref="GetAllClassReferencesParallel(System.Collections.Generic.IEnumerable{string}, bool, bool, bool)"/>
        /// </summary>
        /// <param name="rootDirectory">String defining the path to the root directory</param>
        /// <param name="includeInheritance">Include inheritance relationships</param>
        /// <param name="includeFields">Include Field relationships</param>
        /// <param name="includeMethods">Include Method relationships</param>
        /// <returns></returns>
        public static Dictionary<string, ClassReferences> GetAllClassReferencesParallel(string rootDirectory, bool includeInheritance = true, bool includeFields = true, bool includeMethods = true)
        {
            var scriptPaths = GetScriptPathsInDirectory(rootDirectory);
            return GetAllClassReferencesParallel(scriptPaths, includeInheritance, includeFields, includeMethods);
        }

        public static Dictionary<string, ClassReferences> GetAllClassReferencesParallel(IEnumerable<string> scriptPaths, bool includeInheritance = true, bool includeFields = true, bool includeMethods = true)
        {
            var resultDictionary = new ConcurrentDictionary<string, ClassReferences>();

            Parallel.ForEach(
                scriptPaths,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                scriptPath =>
                {
                    try
                    {
                        var references = GetClassReferences(scriptPath, includeInheritance, includeFields, includeMethods);
                        var scriptName = Path.GetFileNameWithoutExtension(scriptPath);
                        resultDictionary.TryAdd(scriptName, references);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error processing {scriptPath}: {ex.Message}");
                    }
                }
            );

            return new Dictionary<string, ClassReferences>(resultDictionary);
        }

        private static ClassReferences GetClassReferences(string scriptPath, bool includeInheritance, bool includeFields, bool includeMethods)
        {
            var inheritanceReferences = new HashSet<string>();
            var fieldReferences = new HashSet<string>();
            var methodReferences = new HashSet<string>();
            UsingStatements.Value.Clear();

            string content;
            using (var reader = new StreamReader(scriptPath))
            {
                content = reader.ReadToEnd();
            }

            // First collect using statements
            CollectUsingStatements(content);

            // Then collect references
            if (includeInheritance)
                CollectInheritanceReferences(content, inheritanceReferences);
            if (includeFields)
                CollectFieldReferences(content, fieldReferences);
            if (includeMethods)
                CollectMethodReferences(content, methodReferences);

            FilterCommonTypes(inheritanceReferences);
            FilterCommonTypes(fieldReferences);
            FilterCommonTypes(methodReferences);

            return new ClassReferences
            {
                InheritanceReferences = inheritanceReferences.ToList(),
                FieldReferences = fieldReferences.ToList(),
                MethodReferences = methodReferences.ToList()
            };
        }

        private static void CollectInheritanceReferences(string content, HashSet<string> references)
        {
            // Collect inheritance and interface references
            var inheritanceRegex = new Regex(@"(?:class|struct|interface)\s+([A-Za-z0-9_]+)\s*(?::\s*([A-Za-z0-9_,.]+))?", RegexOptions.Compiled);
            foreach (Match match in inheritanceRegex.Matches(content))
            {
                var baseTypes = match.Groups[2].Value.Split(',').Select(t => t.Trim());
                foreach (var baseType in baseTypes)
                {
                    if (!string.IsNullOrEmpty(baseType))
                    {
                        ProcessTypeReference(baseType, references);
                    }
                }
            }

            // Collect composite types from method parameters and return types
            var methodRegex = new Regex(
                @"(?:private|public|protected|internal)\s+" +
                @"(?:static\s+)?" +
                @"(?:(?:void)|(?:[A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*(?:<[^>]+>)?))?" +
                @"\s+\w+\s*\(" +
                @"([^\)]*)\)",
                RegexOptions.Compiled
            );

            foreach (Match match in methodRegex.Matches(content))
            {
                var returnType = match.Groups[2].Value;
                if (!string.IsNullOrEmpty(returnType))
                {
                    ProcessTypeReference(returnType, references);
                }

                var parameterTypes = Regex.Matches(match.Groups[1].Value, @"[A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*(?:<[^>]+>)?");
                foreach (Match parameterTypeMatch in parameterTypes)
                {
                    if (!string.IsNullOrEmpty(parameterTypeMatch.Value))
                    {
                        ProcessTypeReference(parameterTypeMatch.Value, references);
                    }
                }
            }
        }

        private static List<string> GetScriptPathsInDirectory(string path)
        {
            var scriptPaths = new List<string>();
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Directory does not exist: {path}");
                return scriptPaths;
            }

            scriptPaths.AddRange(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));
            return scriptPaths;
        }


        private static void CollectFieldReferences(string content, HashSet<string> references)
        {
            // Updated to collect composite types
            var fieldRegex = new Regex(
                @"(?:private|public|protected|internal)\s+" +
                @"(?:readonly\s+)?" +
                @"(?:static\s+)?" +
                @"([A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*)" +
                "(?:<[^>]+>)?" +
                @"\s+\w+\s*" +
                "(?:[;=]|{[^}]*})",
                RegexOptions.Compiled
            );

            foreach (Match match in fieldRegex.Matches(content))
            {
                var fullType = match.Groups[1].Value;
                ProcessTypeReference(fullType, references);

                // If there are generic parameters, extract and process them too
                var genericParamsMatch = Regex.Match(match.Value, "<([^>]+)>");
                if (!genericParamsMatch.Success) continue;
                foreach (var genericType in genericParamsMatch.Groups[1].Value.Split(','))
                {
                    ProcessTypeReference(genericType.Trim(), references);
                }
            }
        }

        private static void CollectMethodReferences(string content, HashSet<string> references)
        {
            // Updated to collect composite types
            var methodRegex = new Regex(
                @"(?:private|public|protected|internal)\s+" +
                @"(?:static\s+)?" +
                @"(?:(?:void)|(?:[A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*(?:<[^>]+>)?))?" +
                @"\s+\w+\s*\(" +
                @"([^\)]*)\)",
                RegexOptions.Compiled
            );

            foreach (Match match in methodRegex.Matches(content))
            {
                var returnType = match.Groups[2].Value;
                if (!string.IsNullOrEmpty(returnType))
                {
                    ProcessTypeReference(returnType, references);
                }

                var parameterTypes = Regex.Matches(match.Groups[1].Value, @"[A-Za-z0-9_.<>]+(?:\.[A-Za-z0-9_]+)*(?:<[^>]+>)?");
                foreach (Match parameterTypeMatch in parameterTypes)
                {
                    if (!string.IsNullOrEmpty(parameterTypeMatch.Value))
                    {
                        ProcessTypeReference(parameterTypeMatch.Value, references);
                    }
                }
            }
        }

        private static void CollectUsingStatements(string content)
        {
            var usingRegex = new Regex(@"using\s+(?!static|System)([^;]+);", RegexOptions.Compiled);
            foreach (Match match in usingRegex.Matches(content))
            {
                UsingStatements.Value.Add(match.Groups[1].Value.Trim());
            }
        }


        private static void ProcessTypeReference(string type, HashSet<string> references)
        {
            // Handle nested types (e.g., Constants.ComponentGraphDrawType)
            var typeComponents = type.Split('.');
            foreach (var component in typeComponents)
            {
                if (!string.IsNullOrWhiteSpace(component))
                {
                    AddReference(component, references);
                }
            }

            AddReference(type, references);
        }

        private static void AddReference(string type, HashSet<string> references)
        {
            type = type.Trim().Replace("[]", "");

            if (type.Contains("<"))
            {
                // Extract the base type from generic types
                type = type.Substring(0, type.IndexOf("<", StringComparison.Ordinal));
            }

            if (IsCSharpKeyword(type))
                return;

            references.Add(type);

            // Add fully qualified names based on using statements
            foreach (var usingStatement in UsingStatements.Value.Where(us => us.EndsWith("." + type)))
            {
                references.Add(usingStatement + "." + type);
            }
        }

        private static readonly HashSet<string> CommonTypes = new()
        {
            "void", "string", "int", "float", "double", "bool", "decimal", "object", "dynamic", "var", "byte", "char", "long", "short", "uint", "ulong", "ushort", "sbyte", "DateTime", "TimeSpan", "IEnumerable", "IEnumerator", "IList", "List", "Dictionary", "HashSet", "Queue", "Stack", "Array",
            "ICollection", "GameObject", "Transform", "Vector2", "Vector3", "Vector4", "Quaternion", "Mathf", "Debug", "MonoBehaviour", "Component", "Rigidbody", "Rigidbody2D", "Collider", "Collider2D", "Label", "TextField", "Color", "Component", "Node", "Group", "GameObject", "Constants", "List",
            "Dictionary"
        };

        private static readonly HashSet<string> CSharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"
        };

        private static void FilterCommonTypes(HashSet<string> references)
        {
            references.RemoveWhere(r => CommonTypes.Contains(r));
        }

        private static bool IsCSharpKeyword(string word)
        {
            return CSharpKeywords.Contains(word.ToLower());
        }
    }
}