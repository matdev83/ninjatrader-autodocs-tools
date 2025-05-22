using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections.Generic;

namespace ReflectionGenerator
{
    public class Program
    {
        private static HashSet<string>? _ignoredStopwords;

        static void Main(string[] args)
        {
            try
            {
                string? dllPath = null;
                string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "generated-code");
                string? ignoredFilenamesPath = null;

                // (Verbose argument output removed)

                // Parse command line arguments
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    // Skip '--' argument if present
                    if (arg == "--")
                    {
                        continue;
                    }
                        
                    if (arg == "--dll" && i + 1 < args.Length)
                    {
                        dllPath = args[++i];
                    }
                    else if (arg == "--output" && i + 1 < args.Length)
                    {
                        outputDir = args[++i];
                    }
                    else if (arg == "--ignored-filenames" && i + 1 < args.Length)
                    {
                        ignoredFilenamesPath = args[++i];
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
                // Check if original type name starts with underscore
                if (type.Name.StartsWith("_"))
                {
                    Console.WriteLine($"Skipping {type.Name} - starts with underscore in original name");
                    skippedTypes++;
                    continue;
                }

                // Sanitize components for filename used in logging and stopword check
                var sanitizedTypeNameForDisplay = SanitizeFileNameComponent(type.Name.Split('`')[0]);
                var sanitizedNamespaceForDisplay = !string.IsNullOrEmpty(type.Namespace) ? SanitizeFileNameComponent(type.Namespace) : null;
                var fileName = !string.IsNullOrEmpty(sanitizedNamespaceForDisplay) ? $"{sanitizedNamespaceForDisplay}.{sanitizedTypeNameForDisplay}.cs" : $"{sanitizedTypeNameForDisplay}.cs";
                
                // Check if filename starts with underscore after sanitization
                if (fileName.StartsWith("_"))
                {
                    Console.WriteLine($"Skipping {fileName} - starts with underscore");
                    skippedTypes++;
                    continue;
                }
                            
                            // Check if filename contains any stopwords
                            if (_ignoredStopwords != null && _ignoredStopwords.Any(stopword => fileName.Contains(stopword, StringComparison.OrdinalIgnoreCase)))
                            {
                                Console.WriteLine($"Skipping {fileName} - contains ignored stopword");
                                skippedTypes++;
                                continue;
                            }

                            Console.WriteLine($"Generating {fileName}...");
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

        public static bool ShouldProcessType(TypeDefinition type)
        {
            // Skip compiler-generated types and non-public types
            return type.IsPublic &&
                   !type.Name.StartsWith("<") &&
                   !type.Name.StartsWith("_");
        }

        public static void GenerateTypeScaffolding(TypeDefinition type, string outputDir)
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
                var escapedMsg = EscapeStringForAttribute(msg);
                sb.AppendLine(escapedMsg != null ? $"[Obsolete(\"{escapedMsg}\")]" : "[Obsolete]");
            }

            // Add namespace
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                sb.AppendLine($"namespace {type.Namespace}");
                sb.AppendLine("{");
            }

            // Handle enum generation
            if (type.IsEnum)
            {
                        var underlyingType = type.Fields.FirstOrDefault(f => f.Name == "value__")?.FieldType ?? type.Module.TypeSystem.Int32;
                        sb.AppendLine($"    public enum {GetTypeName(type)} : {GetTypeName(underlyingType)}");
                sb.AppendLine("    {");
                // Only include fields that are enum members (not special fields)
                var enumFields = type.Fields
                    .Where(f => f.IsStatic && f.HasConstant && !f.Name.StartsWith("value__"))
                    .ToList();

                for (int i = 0; i < enumFields.Count; i++)
                {
                    var field = enumFields[i];
                    var value = field.Constant;
                    string valueString = FormatEnumMemberValue(value, underlyingType);

                    // Add [Obsolete] if present
                    var fieldObsolete = field.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
                    if (fieldObsolete != null)
                    {
                        var msg = fieldObsolete.ConstructorArguments.Count > 0 ? fieldObsolete.ConstructorArguments[0].Value?.ToString() : null;
                        var escapedMsg = EscapeStringForAttribute(msg);
                        sb.AppendLine(escapedMsg != null ? $"        [Obsolete(\"{escapedMsg}\")]" : "        [Obsolete]");
                    }
                    sb.Append($"        {field.Name} = {valueString}");
                    if (i < enumFields.Count - 1)
                        sb.AppendLine(",");
                    else
                        sb.AppendLine();
                }
                sb.AppendLine("    }");
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    sb.AppendLine("}");
                }

                // Write to file
                var sanitizedEnumTypeName = SanitizeFileNameComponent(GetTypeName(type)); // GetTypeName for enum returns simple name
                var sanitizedEnumNamespace = !string.IsNullOrEmpty(type.Namespace) ? SanitizeFileNameComponent(type.Namespace) : null;
                var enumFileName = !string.IsNullOrEmpty(sanitizedEnumNamespace) ? $"{sanitizedEnumNamespace}.{sanitizedEnumTypeName}.cs" : $"{sanitizedEnumTypeName}.cs";
                var filePath = Path.Combine(outputDir, enumFileName);
                var content = sb.ToString();
                File.WriteAllText(filePath, content);
            }
            else
            {
                // Add type declaration
                var typeKind = type.IsInterface ? "interface" :
                              type.IsValueType ? "struct" : "class";
                var partial = "partial "; // Assuming all generated types are partial
                
                // Use GetTypeName for the type name itself, base type, and interfaces
                string typeNameString = GetTypeName(type);
                
                // For baseType, ensure it's not System.Object and not an enum's implicit base.
                var baseTypeString = "";
                if (type.BaseType != null && type.BaseType.FullName != "System.Object" && !type.IsEnum)
                {
                    baseTypeString = $" : {GetTypeName(type.BaseType)}";
                }

                var interfacesString = "";
                if (type.Interfaces.Any())
                {
                    interfacesString = (baseTypeString == "" ? " : " : ", ") +
                                     string.Join(", ", type.Interfaces.Select(i => GetTypeName(i.InterfaceType)));
                }

                // Remove potential namespace qualifier from the type name for declaration if it's in the current namespace
                string localTypeName = typeNameString;
                if (!string.IsNullOrEmpty(type.Namespace) && typeNameString.StartsWith(type.Namespace + "."))
                {
                     localTypeName = typeNameString.Substring(type.Namespace.Length + 1);
                }
                // If the type is nested, only the final part of the name should be used for declaration.
                // GetTypeName returns full path like MyNamespace.OuterType.NestedType
                // We only want NestedType for the declaration if OuterType is the current scope.
                // However, GenerateTypeScaffolding is called for top-level types first, 
                // and recursively for nested types. So, type.Name is appropriate here.
                // The GetTypeName will handle the full name for base types and interfaces.
                // The type name in declaration should be simple name if not generic, or simpleName<T> if generic.
                // Let's adjust how typeNameString is used for the declaration:
                string declarationName = type.Name; // Start with simple name
                if (type.HasGenericParameters)
                {
                    declarationName = type.Name.Split('`')[0] + "<" + string.Join(", ", type.GenericParameters.Select(p => p.Name)) + ">";
                    // Append constraints if any, directly to the declaration name
                    var constraints = new StringBuilder();
                    foreach (var param in type.GenericParameters)
                    {
                        var paramConstraints = param.Constraints.Select(c => GetTypeName(c.ConstraintType)).ToList();
                        if (param.HasReferenceTypeConstraint) paramConstraints.Insert(0, "class");
                        if (param.HasNotNullableValueTypeConstraint) paramConstraints.Insert(0, "struct");
                        if (param.HasDefaultConstructorConstraint && !param.HasNotNullableValueTypeConstraint) paramConstraints.Add("new()");
                        if (paramConstraints.Count > 0)
                        {
                            constraints.Append($" where {param.Name} : {string.Join(", ", paramConstraints)}");
                        }
                    }
                    declarationName += constraints.ToString();
                }


                sb.AppendLine($"    public {partial}{typeKind} {declarationName}{baseTypeString}{interfacesString}");
                sb.AppendLine("    {");

                // #region Properties
                sb.AppendLine("        #region Properties");
                foreach (var property in type.Properties)
                {
                    if (property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true)
                    {
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine($"        /// Gets or sets the {property.Name}.");
                        sb.AppendLine("        /// </summary>");
                        sb.AppendLine("        [DataMember]");
                        var propObsolete = property.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
                        if (propObsolete != null)
                        {
                            var msg = propObsolete.ConstructorArguments.Count > 0 ? propObsolete.ConstructorArguments[0].Value?.ToString() : null;
                            var escapedMsg = EscapeStringForAttribute(msg);
                            sb.AppendLine(escapedMsg != null ? $"        [Obsolete(\"{escapedMsg}\")]" : "        [Obsolete]");
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
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine($"        /// {method.Name} method.");
                        sb.AppendLine("        /// </summary>");
                        foreach (var param in method.Parameters)
                            sb.AppendLine($"        /// <param name=\"{param.Name}\">{GetTypeName(param.ParameterType)}</param>"); // Use GetTypeName for param type
                        if (method.ReturnType.FullName != "System.Void")
                            sb.AppendLine($"        /// <returns>{GetTypeName(method.ReturnType)}</returns>"); // Use GetTypeName for return type
                        var methObsolete = method.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "System.ObsoleteAttribute");
                        if (methObsolete != null)
                        {
                            var msg = methObsolete.ConstructorArguments.Count > 0 ? methObsolete.ConstructorArguments[0].Value?.ToString() : null;
                            var escapedMsg = EscapeStringForAttribute(msg);
                            sb.AppendLine(escapedMsg != null ? $"        [Obsolete(\"{escapedMsg}\")]" : "        [Obsolete]");
                        }
                        
                        string methodGenericParams = "";
                        var methodConstraints = new StringBuilder();
                        if (method.HasGenericParameters)
                        {
                            methodGenericParams = "<" + string.Join(", ", method.GenericParameters.Select(p => p.Name)) + ">";
                            // Append constraints for method generic parameters
                            foreach (var param in method.GenericParameters)
                            {
                                var paramConstraints = param.Constraints.Select(c => GetTypeName(c.ConstraintType)).ToList();
                                if (param.HasReferenceTypeConstraint) paramConstraints.Insert(0, "class");
                                if (param.HasNotNullableValueTypeConstraint) paramConstraints.Insert(0, "struct");
                                if (param.HasDefaultConstructorConstraint && !param.HasNotNullableValueTypeConstraint) paramConstraints.Add("new()");
                                if (paramConstraints.Count > 0)
                                {
                                    methodConstraints.Append($"where {param.Name} : {string.Join(", ", paramConstraints)}");
                                }
                            }
                            if (methodConstraints.Length > 0)
                            {
                                sb.AppendLine();
                                sb.AppendLine();
                                sb.Append($"            {methodConstraints.ToString().Trim()}");
                            }
                        }

                        var parameters = string.Join(", ", method.Parameters.Select(p =>
                            $"{GetTypeName(p.ParameterType)} {p.Name}"));
                        if (type.IsInterface)
                        {
                            sb.Append($"        public {GetTypeName(method.ReturnType)} {method.Name}{methodGenericParams}({parameters})");
                            if (methodConstraints.Length > 0)
                            {
                                sb.Append($" {methodConstraints.ToString().Trim()}");
                            }
                            sb.AppendLine(";");
                        }
                        else
                        {
                            sb.AppendLine($"        public {GetTypeName(method.ReturnType)} {method.Name}{methodGenericParams}({parameters})");
                            if (methodConstraints.Length > 0)
                            {
                                sb.AppendLine($"            {methodConstraints.ToString().Trim()}");
                            }
                            sb.AppendLine("        {");
                            sb.AppendLine("            // Method implementation goes here");
                            sb.AppendLine("        }");
                        }
                    }
                }
                sb.AppendLine("        #endregion");

                sb.AppendLine("    }");
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    sb.AppendLine("}");
                }

                // Adjust filename generation to use the simple name for the file
                var simpleTypeNameForFile = SanitizeFileNameComponent(type.Name.Split('`')[0]);
                var sanitizedNamespace = !string.IsNullOrEmpty(type.Namespace) ? SanitizeFileNameComponent(type.Namespace) : null;
                var finalFileName = !string.IsNullOrEmpty(sanitizedNamespace) ? $"{sanitizedNamespace}.{simpleTypeNameForFile}.cs" : $"{simpleTypeNameForFile}.cs";
                
                var filePath = Path.Combine(outputDir, finalFileName);
                var content = sb.ToString();
                File.WriteAllText(filePath, content);

                foreach (var nestedType in type.NestedTypes)
                {
                    if (ShouldProcessType(nestedType))
                    {
                        // For nested types, the output directory is the same.
                        // GenerateTypeScaffolding will handle their full names correctly.
                        GenerateTypeScaffolding(nestedType, outputDir);
                    }
                }
            }
        }

        public static string GetTypeName(TypeReference type)
        {
            // Handle enum types - return their actual name
            if (type.Resolve()?.IsEnum == true)
            {
                return type.Name;
            }
            if (type.IsGenericParameter)
                return type.Name;

            string fullName = type.FullName.Replace("/", "."); // Handle nested type names like declaringType.NestedType

            // Rest of the original GetTypeName implementation
            if (type.IsGenericInstance)
            {
                var genericType = (GenericInstanceType)type;
                var elementTypeFullName = genericType.ElementType.FullName.Replace("/", ".");
                var baseTypeName = elementTypeFullName.Split('`')[0];
                var typeArgs = string.Join(", ", genericType.GenericArguments.Select(GetTypeName));
                
                // Handle cases like Nullable<T> which should be T?
                if (baseTypeName == "System.Nullable" && genericType.GenericArguments.Count == 1)
                {
                    return $"{GetTypeName(genericType.GenericArguments[0])}?";
                }
                
                return $"{baseTypeName}<{typeArgs}>";
            }

            if (type.HasGenericParameters)
            {
                var baseTypeName = fullName.Split('`')[0];
                var typeParams = string.Join(", ", type.GenericParameters.Select(p => p.Name));
                // Constraints are handled at the declaration site (class/method), not in GetTypeName for type definitions.
                // GetTypeName is for resolving type references.
                return $"{baseTypeName}<{typeParams}>";
            }
            
            // Use C# keyword for primitive types
            switch (fullName)
            {
                case "System.Boolean": return "bool";
                case "System.Byte": return "byte";
                case "System.SByte": return "sbyte";
                case "System.Char": return "char";
                case "System.Decimal": return "decimal";
                case "System.Double": return "double";
                case "System.Single": return "float";
                case "System.Int32": return "int";
                case "System.UInt32": return "uint";
                case "System.Int64": return "long";
                case "System.UInt64": return "ulong";
                case "System.Int16": return "short";
                case "System.UInt16": return "ushort";
                case "System.String": return "string";
                case "System.Object": return "object";
                case "System.Void": return "void";
                // Consider global:: prefix for types that might conflict with local names, though Cecil usually gives full names.
                default: return fullName; 
            }
        }

        public static string? EscapeStringForAttribute(string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            var sb = new StringBuilder();
            foreach (char c in message)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    // Add other escapes if necessary, e.g. for other control characters
                    // case '\b': sb.Append("\\b"); break;
                    // case '\f': sb.Append("\\f"); break;
                    // case '\v': sb.Append("\\v"); break;
                    // case '\0': sb.Append("\\0"); break;
                    default:
                        // Check for other control characters and unicode escape them if needed
                        if (char.IsControl(c))
                        {
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        public static string FormatEnumMemberValue(object value, TypeReference underlyingType)
        {
            // field.Constant gives the value directly in its underlying type (e.g., int, long, byte).
            // We need to ensure it's formatted correctly for C# source code.
            string underlyingTypeName = underlyingType.FullName;

            if (underlyingTypeName == "System.Int64") // long
            {
                return $"{value}L";
            }
            else if (underlyingTypeName == "System.UInt64") // ulong
            {
                // For ulong, if the value is large enough to be ambiguous with long, it might need UL.
                // However, ToString() on a ulong usually produces a number that C# interprets correctly.
                // Explicitly adding UL can be done for absolute clarity or if issues arise.
                // For very large ulong values, C# might require a cast if not using UL, but direct assignment usually works.
                // Let's add UL for consistency and to avoid any ambiguity.
                return $"{value}UL";
            }
            else if (underlyingTypeName == "System.UInt32" || // uint
                     underlyingTypeName == "System.UInt16" || // ushort
                     underlyingTypeName == "System.Byte")     // byte
            {
                 // For uint, ushort, byte, sometimes 'U' suffix is used for uint if number could be int,
                 // but generally, direct number is fine.
                 // Example: (uint)10 or 10u.
                 // For now, direct ToString() is usually sufficient as context of enum implies the type.
                 // If we want to be extremely explicit:
                 // if (underlyingTypeName == "System.UInt32") return $"{value}U";
                 // if (underlyingTypeName == "System.UInt16") return $"(ushort){value}"; // or just value
                 // if (underlyingTypeName == "System.Byte") return $"(byte){value}"; // or just value
                 return value.ToString(); // Default ToString() is generally fine for these.
            }
            // For System.Int32, System.Int16, System.SByte, System.Char, the default ToString() is correct.
            return value.ToString();
        }

        public static string SanitizeFileNameComponent(string component)
        {
            if (string.IsNullOrEmpty(component))
            {
                return component;
            }

            var sb = new StringBuilder();
            // Define invalid characters. Includes Path.GetInvalidFileNameChars() and Path.GetInvalidPathChars()
            // Also explicitly list some common ones to be sure.
            char[] invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
            // To be absolutely safe, add common problematic characters explicitly if not already covered
            // by the system's invalid char sets. For this exercise, we'll assume the system sets are comprehensive enough
            // for typical scenarios but one might add < > : " / \ | ? * and control chars explicitly.
            // For this implementation, we rely on system provided lists and then manually check for control characters.

            foreach (char c in component)
            {
                if (Array.IndexOf(invalidChars, c) >= 0 || char.IsControl(c))
                {
                    sb.Append('_'); // Replace invalid char with underscore
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
