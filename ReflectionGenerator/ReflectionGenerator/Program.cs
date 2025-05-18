using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace ReflectionGenerator
{
    class Program
    {
        private static HashSet<string>? _ignoredStopwords;

        static void Main(string[] args)
        {
            try
            {
                string? dllPath = null;
                string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "generated-code");
                string? ignoredFilenamesPath = null;

                // Debug output
                Console.WriteLine("Received arguments:");
                for (int i = 0; i < args.Length; i++)
                {
                    Console.WriteLine($"args[{i}] = '{args[i]}'");
                }

                // Parse command line arguments
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    // Skip '--' argument if present
                    if (arg == "--")
                    {
                        Console.WriteLine("Skipping '--' argument");
                        continue;
                    }
                        
                    if (arg == "--dll" && i + 1 < args.Length)
                    {
                        dllPath = args[++i];
                        Console.WriteLine($"Found --dll parameter: {dllPath}");
                    }
                    else if (arg == "--output" && i + 1 < args.Length)
                    {
                        outputDir = args[++i];
                        Console.WriteLine($"Found --output parameter: {outputDir}");
                    }
                    else if (arg == "--ignored-filenames" && i + 1 < args.Length)
                    {
                        ignoredFilenamesPath = args[++i];
                        Console.WriteLine($"Found --ignored-filenames parameter: {ignoredFilenamesPath}");
                    }
                }

                if (string.IsNullOrEmpty(dllPath))
                {
                    Console.WriteLine("Usage: ReflectionGenerator --dll <path-to-dll> [--output <output-directory>] [--ignored-filenames <path-to-stopwords-file>]");
                    Console.WriteLine("  --dll <path>     Path to the DLL file to process (required)");
                    Console.WriteLine("  --output <path>  Output directory for generated code (default: .\\generated-code)");
                    Console.WriteLine("  --ignored-filenames <path>  Path to file containing stopwords to ignore in filenames (optional)");
                    return;
                }

                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"Error: File {dllPath} does not exist.");
                    return;
                }

                // Load ignored stopwords if file is provided
                if (!string.IsNullOrEmpty(ignoredFilenamesPath))
                {
                    if (!File.Exists(ignoredFilenamesPath))
                    {
                        Console.WriteLine($"Error: Ignored filenames file {ignoredFilenamesPath} does not exist.");
                        return;
                    }
                    _ignoredStopwords = new HashSet<string>(File.ReadAllLines(ignoredFilenamesPath)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim()),
                        StringComparer.OrdinalIgnoreCase);
                    Console.WriteLine($"Loaded {_ignoredStopwords.Count} stopwords from {ignoredFilenamesPath}");
                }

                Console.WriteLine($"Processing DLL: {dllPath}");
                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                Console.WriteLine($"Generating code in: {outputDir}");

                int processedTypes = 0;
                int skippedTypes = 0;
                foreach (var type in assembly.MainModule.Types)
                {
                    try
                    {
                        if (ShouldProcessType(type))
                        {
                            var fileName = !string.IsNullOrEmpty(type.Namespace) ? $"{type.Namespace}.{type.Name}.cs" : $"{type.Name}.cs";
                            
                            // Check if filename contains any stopwords
                            if (_ignoredStopwords != null && _ignoredStopwords.Any(stopword => fileName.Contains(stopword, StringComparison.OrdinalIgnoreCase)))
                            {
                                Console.WriteLine($"Skipping {fileName} - contains ignored stopword");
                                skippedTypes++;
                                continue;
                            }

                            GenerateTypeScaffolding(type, outputDir);
                            processedTypes++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing type {type.FullName}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Code generation completed successfully! Processed {processedTypes} types, skipped {skippedTypes} types.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing DLL: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static bool ShouldProcessType(TypeDefinition type)
        {
            // Skip compiler-generated types and non-public types
            return type.IsPublic && 
                   !type.Name.Contains("<>") && 
                   !type.Name.Contains("__");
        }

        private static void GenerateTypeScaffolding(TypeDefinition type, string outputDir)
        {
            var sb = new StringBuilder();
            
            // Add [Serializable] for classes/structs
            if ((type.IsClass || type.IsValueType) && !type.IsInterface && !type.IsEnum)
            {
                sb.AppendLine("[Serializable]");
                Console.WriteLine($"Adding [Serializable] to {type.Name}");
            }

            // Add [DataContract] for classes
            if (type.IsClass && !type.IsInterface && !type.IsEnum)
            {
                sb.AppendLine("[DataContract]");
                Console.WriteLine($"Adding [DataContract] to {type.Name}");
            }

            // Add [Flags] for enums with FlagsAttribute
            if (type.IsEnum && type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.FlagsAttribute"))
            {
                sb.AppendLine("[Flags]");
                Console.WriteLine($"Adding [Flags] to {type.Name}");
            }

            // Add [Obsolete] if present
            var obsoleteAttr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
            if (obsoleteAttr != null)
            {
                var msg = obsoleteAttr.ConstructorArguments.Count > 0 ? obsoleteAttr.ConstructorArguments[0].Value?.ToString() : null;
                sb.AppendLine(msg != null ? $"[Obsolete(\"{msg}\")]" : "[Obsolete]");
                Console.WriteLine($"Adding [Obsolete] to {type.Name}");
            }

            // Add namespace
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }

            // Add type declaration
            var typeKind = type.IsInterface ? "interface" : 
                          type.IsEnum ? "enum" : 
                          type.IsValueType ? "struct" : "class";
            var partial = "partial ";
            var baseType = type.BaseType != null && type.BaseType.FullName != "System.Object" && !type.IsEnum ? $" : {GetTypeName(type.BaseType)}" : "";
            var interfaces = type.Interfaces.Any() ? 
                (baseType == "" ? " : " : ", ") + 
                string.Join(", ", type.Interfaces.Select(i => GetTypeName(i.InterfaceType))) : "";

            sb.AppendLine($"    public {partial}{typeKind} {type.Name}{baseType}{interfaces}");
            sb.AppendLine("    {");

            // #region Properties
            sb.AppendLine("        #region Properties");
            foreach (var property in type.Properties)
            {
                if (property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true)
                {
                    // Add XML doc comment for property
                    sb.AppendLine("        /// <summary>");
                    sb.AppendLine($"        /// Gets or sets the {property.Name}.");
                    sb.AppendLine("        /// </summary>");
                    // Add [DataMember] for properties
                    sb.AppendLine("        [DataMember]");
                    // Add [Obsolete] if present
                    var propObsolete = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
                    if (propObsolete != null)
                    {
                        var msg = propObsolete.ConstructorArguments.Count > 0 ? propObsolete.ConstructorArguments[0].Value?.ToString() : null;
                        sb.AppendLine(msg != null ? $"        [Obsolete(\"{msg}\")]" : "        [Obsolete]");
                    }
                    sb.AppendLine($"        public {GetTypeName(property.PropertyType)} {property.Name} {{ get; set; }}");
                }
            }
            sb.AppendLine("        #endregion");

            // #region Methods
            sb.AppendLine("        #region Methods");
            foreach (var method in type.Methods)
            {
                if (method.IsPublic && !method.IsConstructor && !method.IsSpecialName)
                {
                    // Add XML doc comment for method
                    sb.AppendLine("        /// <summary>");
                    sb.AppendLine($"        /// {method.Name} method.");
                    sb.AppendLine("        /// </summary>");
                    foreach (var param in method.Parameters)
                        sb.AppendLine($"        /// <param name=\"{param.Name}\">{param.ParameterType.Name}</param>");
                    if (method.ReturnType.FullName != "System.Void")
                        sb.AppendLine($"        /// <returns>{method.ReturnType.Name}</returns>");
                    // Add [Obsolete] if present
                    var methObsolete = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
                    if (methObsolete != null)
                    {
                        var msg = methObsolete.ConstructorArguments.Count > 0 ? methObsolete.ConstructorArguments[0].Value?.ToString() : null;
                        sb.AppendLine(msg != null ? $"        [Obsolete(\"{msg}\")]" : "        [Obsolete]");
                    }
                    var parameters = string.Join(", ", method.Parameters.Select(p => 
                        $"{GetTypeName(p.ParameterType)} {p.Name}"));
                    sb.AppendLine($"        public {GetTypeName(method.ReturnType)} {method.Name}({parameters});");
                }
            }
            sb.AppendLine("        #endregion");

            // Close type and namespace
            sb.AppendLine("    }");
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine("}");
            }

            // Write to file
            var fileName = !string.IsNullOrEmpty(type.Namespace) ? $"{type.Namespace}.{type.Name}.cs" : $"{type.Name}.cs";
            var filePath = Path.Combine(outputDir, fileName);
            var content = sb.ToString();
            File.WriteAllText(filePath, content);
            
            // Debug output
            Console.WriteLine($"Generated file {fileName} with content:");
            Console.WriteLine("---");
            Console.WriteLine(content);
            Console.WriteLine("---");
        }

        private static string GetTypeName(TypeReference type)
        {
            if (type.IsGenericParameter)
                return type.Name;

            if (type.IsGenericInstance)
            {
                var genericType = (GenericInstanceType)type;
                var baseType = genericType.ElementType.Name.Split('`')[0];
                var typeArgs = string.Join(", ", genericType.GenericArguments.Select(GetTypeName));
                return $"{baseType}<{typeArgs}>";
            }

            if (type.HasGenericParameters)
            {
                var baseType = type.Name.Split('`')[0];
                var typeParams = string.Join(", ", type.GenericParameters.Select(p => p.Name));
                return $"{baseType}<{typeParams}>";
            }

            return type.Name;
        }
    }
}
