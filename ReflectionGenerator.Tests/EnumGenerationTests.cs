using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator;
using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class EnumGenerationTests
    {
        private static ModuleDefinition _testModule = null!;
        private static string _tempOutputDir = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var assemblyDef = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAssemblyForEnumsGen", new Version(1, 0)),
                "TestModuleForEnumsGen",
                ModuleKind.Dll);
            _testModule = assemblyDef.MainModule;
            _tempOutputDir = Path.Combine(Path.GetTempPath(), "ReflectionGenTests_Enums");
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
        
        private TypeReference ImportType(Type type)
        {
            return _testModule.ImportReference(type);
        }

        private string GenerateAndReadFile(TypeDefinition typeDef)
        {
            if (!_testModule.Types.Contains(typeDef))
            {
                _testModule.Types.Add(typeDef);
            }
            
            string sanitizedNamespace = string.IsNullOrEmpty(typeDef.Namespace) ? "" : Program.SanitizeFileNameComponent(typeDef.Namespace);
            // GetTypeName for enum returns its simple name, which should be sanitized.
            string sanitizedTypeName = Program.SanitizeFileNameComponent(Program.GetTypeName(typeDef)); 

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
        public void GenerateEnum_WithDefaultUnderlyingType_GeneratesCorrectly()
        {
            var enumDef = new TypeDefinition("Test.Enums", "MySimpleEnum", TypeAttributes.Public | TypeAttributes.Sealed, ImportType(typeof(Enum)));
            // Mono.Cecil requires a field named "value__" for enums, representing the instance field for the enum's value.
            var valueField = new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, _testModule.TypeSystem.Int32);
            enumDef.Fields.Add(valueField);

            var member1 = new FieldDefinition("OptionOne", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member1.Constant = 10;
            enumDef.Fields.Add(member1);

            var member2 = new FieldDefinition("OptionTwo", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member2.Constant = 20;
            enumDef.Fields.Add(member2);
            
            string output = GenerateAndReadFile(enumDef);

            StringAssert.Contains(output, "namespace Test.Enums");
            StringAssert.Contains(output, "public enum MySimpleEnum : int"); // Default is int
            StringAssert.Contains(output, "OptionOne = 10,");
            StringAssert.Contains(output, "OptionTwo = 20");
            _testModule.Types.Remove(enumDef);
        }

        [TestMethod]
        public void GenerateEnum_WithLongUnderlyingTypeAndFlags_GeneratesCorrectly()
        {
            var enumDef = new TypeDefinition("Test.Flags", "MyLongFlags", TypeAttributes.Public | TypeAttributes.Sealed, ImportType(typeof(Enum)));
            var valueField = new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, _testModule.TypeSystem.Int64); // Long
            enumDef.Fields.Add(valueField);

            // Add [Flags] attribute
            var flagsCtor = _testModule.ImportReference(typeof(FlagsAttribute).GetConstructor(Type.EmptyTypes));
            enumDef.CustomAttributes.Add(new CustomAttribute(flagsCtor));

            var member1 = new FieldDefinition("FlagA", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member1.Constant = 1L; // Long value
            enumDef.Fields.Add(member1);

            var member2 = new FieldDefinition("FlagB", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member2.Constant = 2L; // Long value
            enumDef.Fields.Add(member2);
            
            var member3 = new FieldDefinition("FlagC", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member3.Constant = 0x100000000L; // > Int32.MaxValue
            enumDef.Fields.Add(member3);


            string output = GenerateAndReadFile(enumDef);

            StringAssert.Contains(output, "namespace Test.Flags");
            StringAssert.Contains(output, "[Flags]");
            StringAssert.Contains(output, "public enum MyLongFlags : long");
            StringAssert.Contains(output, "FlagA = 1L,");
            StringAssert.Contains(output, "FlagB = 2L,");
            StringAssert.Contains(output, $"FlagC = {0x100000000L}L"); // Check the large long value
            _testModule.Types.Remove(enumDef);
        }
        
        [TestMethod]
        public void GenerateEnum_WithByteUnderlyingType_GeneratesCorrectly()
        {
            var enumDef = new TypeDefinition("Test.Bytes", "MyByteEnum", TypeAttributes.Public | TypeAttributes.Sealed, ImportType(typeof(Enum)));
            var valueField = new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, _testModule.TypeSystem.Byte); // Byte
            enumDef.Fields.Add(valueField);

            var member1 = new FieldDefinition("LowByte", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member1.Constant = (byte)5;
            enumDef.Fields.Add(member1);

            var member2 = new FieldDefinition("HighByte", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, enumDef);
            member2.Constant = (byte)250;
            enumDef.Fields.Add(member2);
            
            string output = GenerateAndReadFile(enumDef);

            StringAssert.Contains(output, "namespace Test.Bytes");
            StringAssert.Contains(output, "public enum MyByteEnum : byte");
            StringAssert.Contains(output, "LowByte = 5,");
            StringAssert.Contains(output, "HighByte = 250");
            _testModule.Types.Remove(enumDef);
        }
    }
}
