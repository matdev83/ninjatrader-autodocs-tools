using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace ReflectionGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string dllPath = null;
                string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "generated-code");

                // Parse command line arguments
                foreach (var arg in args)
                {
                    if (arg.StartsWith("--dll="))
                    {
                        dllPath = arg.Substring(6);
                    }
                    else if (arg.StartsWith("--output="))
                    {
                        outputDir = arg.Substring(9);
                    }
                }

                if (string.IsNullOrEmpty(dllPath))
                {
                    Console.WriteLine("Usage: ReflectionGenerator --dll=<path-to-dll> [--output=<output-directory>]");
                    Console.WriteLine("  --dll=<path>     Path to the DLL file to process (required)");
                    Console.WriteLine("  --output=<path>  Output directory for generated code (default: .\\generated-code)");
                    return;
                }

                if (!File.Exists(dllPath))
                {
                    Console.WriteLine($"Error: File {dllPath} does not exist.");
                    return;
                }

                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                int processedTypes = 0;
                foreach (var type in assembly.MainModule.Types)
                {
                    try
                    {
                        if (ShouldProcessType(type))
                        {
                            GenerateTypeScaffolding(type, outputDir);
                            processedTypes++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing type {type.FullName}: {ex.Message}");
                    }
                }

                Console.WriteLine($"Code generation completed successfully! Processed {processedTypes} types.");
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
            }

            // Add [DataContract] for classes
            if (type.IsClass && !type.IsInterface && !type.IsEnum)
            {
                sb.AppendLine("[DataContract]");
            }

            // Add [Flags] for enums with FlagsAttribute
            if (type.IsEnum && type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.FlagsAttribute"))
            {
                sb.AppendLine("[Flags]");
            }

            // Add [Obsolete] if present
            var obsoleteAttr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
            if (obsoleteAttr != null)
            {
                var msg = obsoleteAttr.ConstructorArguments.Count > 0 ? obsoleteAttr.ConstructorArguments[0].Value?.ToString() : null;
                sb.AppendLine(msg != null ? $"[Obsolete(\"{msg}\")]" : "[Obsolete]");
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
            var fileName = $"{type.Name}.cs";
            var filePath = Path.Combine(outputDir, fileName);
            var content = sb.ToString();
            File.WriteAllText(filePath, content);
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
