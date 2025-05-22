using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator;
using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class ObsoleteAttributeTests
    {
        private static ModuleDefinition _testModule = null!;
        private static string _tempOutputDir = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var assemblyDef = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAssemblyForObsolete", new Version(1, 0)),
                "TestModuleForObsolete",
                ModuleKind.Dll);
            _testModule = assemblyDef.MainModule;
            _tempOutputDir = Path.Combine(Path.GetTempPath(), "ReflectionGenTests_Obsolete");
            if (Directory.Exists(_tempOutputDir))
            {
                Directory.Delete(_tempOutputDir, true);
            }
            Directory.CreateDirectory(_tempOutputDir);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (Directory.Exists(_tempOutputDir))
            {
                Directory.Delete(_tempOutputDir, true);
            }
        }

        private CustomAttribute CreateObsoleteAttribute(string message)
        {
            var obsoleteCtor = _testModule.ImportReference(typeof(ObsoleteAttribute).GetConstructor(new[] { typeof(string) }));
            var attribute = new CustomAttribute(obsoleteCtor);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(_testModule.TypeSystem.String, message));
            return attribute;
        }

        private string GenerateAndReadFile(TypeDefinition typeDef)
        {
            // Ensure type is added to module if not already
            if (!_testModule.Types.Contains(typeDef))
            {
                _testModule.Types.Add(typeDef);
            }
            
            string sanitizedNamespace = string.IsNullOrEmpty(typeDef.Namespace) ? "" : Program.SanitizeFileNameComponent(typeDef.Namespace);
            string sanitizedTypeName = Program.SanitizeFileNameComponent(typeDef.Name.Split('`')[0]);
            string expectedFileName = Path.Combine(_tempOutputDir, 
                string.IsNullOrEmpty(sanitizedNamespace) ? $"{sanitizedTypeName}.cs" : $"{sanitizedNamespace}.{sanitizedTypeName}.cs");

            Program.GenerateTypeScaffolding(typeDef, _tempOutputDir);
            
            if (!File.Exists(expectedFileName))
            {
                Assert.Fail($"Expected file '{expectedFileName}' was not generated. Namespace: '{typeDef.Namespace}', Name: '{typeDef.Name}'");
            }
            return File.ReadAllText(expectedFileName);
        }

        [TestMethod]
        public void ObsoleteType_WithMessage_GeneratesCorrectlyEscapedAttribute()
        {
            var typeDef = new TypeDefinition("MyNs", "ObsoleteClass", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            string message = "This type is \"obsolete\" and has a \nnewline.";
            string expectedEscapedMessage = "This type is \\\"obsolete\\\" and has a \\nnewline.";
            typeDef.CustomAttributes.Add(CreateObsoleteAttribute(message));
            
            string output = GenerateAndReadFile(typeDef);
            
            Assert.IsTrue(output.Contains($"[Obsolete(\"{expectedEscapedMessage}\")]"), "Generated output does not contain correctly escaped Obsolete attribute for type.");
            _testModule.Types.Remove(typeDef);
        }

        [TestMethod]
        public void ObsoleteProperty_WithMessage_GeneratesCorrectlyEscapedAttribute()
        {
            var typeDef = new TypeDefinition("MyNs", "ClassWithObsoleteProperty", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var propertyDef = new PropertyDefinition("MyProp", PropertyAttributes.None, _testModule.TypeSystem.Int32);
            propertyDef.GetMethod = new MethodDefinition("get_MyProp", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, _testModule.TypeSystem.Int32);
            typeDef.Methods.Add(propertyDef.GetMethod); // Getter needed for property to be processed

            string message = "Property \"MyProp\" is outdated.";
            string expectedEscapedMessage = "Property \\\"MyProp\\\" is outdated.";
            propertyDef.CustomAttributes.Add(CreateObsoleteAttribute(message));
            typeDef.Properties.Add(propertyDef);
            
            string output = GenerateAndReadFile(typeDef);

            Assert.IsTrue(output.Contains($"[Obsolete(\"{expectedEscapedMessage}\")]"), "Generated output does not contain correctly escaped Obsolete attribute for property.");
            Assert.IsTrue(output.Contains($"public int MyProp {{ get; set; }}"), "Property definition is missing or incorrect.");
            _testModule.Types.Remove(typeDef);
        }


        [TestMethod]
        public void ObsoleteEnumMember_WithMessage_GeneratesCorrectlyEscapedAttribute()
        {
            var typeDef = new TypeDefinition("MyNs", "EnumWithObsoleteMember", TypeAttributes.Public | TypeAttributes.Sealed, _testModule.ImportReference(typeof(Enum)));
            // Set underlying type for enum for completeness, though not strictly needed for this test
            typeDef.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, _testModule.TypeSystem.Int32));


            var enumField = new FieldDefinition("OldValue", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, typeDef);
            enumField.Constant = 1; // Enum members must have a constant value.
            
            string message = "Enum value 'OldValue' is deprecated \t with a tab.";
            string expectedEscapedMessage = "Enum value 'OldValue' is deprecated \\t with a tab.";
            enumField.CustomAttributes.Add(CreateObsoleteAttribute(message));
            typeDef.Fields.Add(enumField);
            
            string output = GenerateAndReadFile(typeDef);
            
            StringAssert.Contains(output, $"[Obsolete(\"{expectedEscapedMessage}\")]");
            StringAssert.Contains(output, "OldValue = 1");
            _testModule.Types.Remove(typeDef);
        }
    }
}
