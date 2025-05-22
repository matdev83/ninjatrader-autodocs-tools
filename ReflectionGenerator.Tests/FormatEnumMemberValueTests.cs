using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator; // Assuming Program class is in this namespace
using Mono.Cecil;

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class FormatEnumMemberValueTests
    {
        private static ModuleDefinition _testModule;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // Create a dummy module to import type references
            var assemblyDef = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAssemblyForEnums", new Version(1, 0)),
                "TestModuleForEnums",
                ModuleKind.Dll);
            _testModule = assemblyDef.MainModule;
        }

        [TestMethod]
        public void FormatEnumMemberValue_Int_ReturnsStringAsIs()
        {
            object value = 123;
            TypeReference underlyingType = _testModule.TypeSystem.Int32;
            string expected = "123";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_Long_AppendsL()
        {
            object value = 1234567890123L;
            TypeReference underlyingType = _testModule.TypeSystem.Int64;
            string expected = "1234567890123L";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_ULong_AppendsUL()
        {
            object value = 1234567890123UL;
            TypeReference underlyingType = _testModule.TypeSystem.UInt64;
            string expected = "1234567890123UL";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_UInt_ReturnsStringAsIs()
        {
            // The method currently returns ToString() for uint, which is fine.
            // If explicit "U" suffix were desired, this test would change.
            object value = (uint)456;
            TypeReference underlyingType = _testModule.TypeSystem.UInt32;
            string expected = "456";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void FormatEnumMemberValue_Short_ReturnsStringAsIs()
        {
            object value = (short)123;
            TypeReference underlyingType = _testModule.TypeSystem.Int16;
            string expected = "123";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_UShort_ReturnsStringAsIs()
        {
            object value = (ushort)123;
            TypeReference underlyingType = _testModule.TypeSystem.UInt16;
            string expected = "123";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_Byte_ReturnsStringAsIs()
        {
            object value = (byte)12;
            TypeReference underlyingType = _testModule.TypeSystem.Byte;
            string expected = "12";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void FormatEnumMemberValue_SByte_ReturnsStringAsIs()
        {
            object value = (sbyte)-12;
            TypeReference underlyingType = _testModule.TypeSystem.SByte;
            string expected = "-12";
            string actual = Program.FormatEnumMemberValue(value, underlyingType);
            Assert.AreEqual(expected, actual);
        }
    }
}
