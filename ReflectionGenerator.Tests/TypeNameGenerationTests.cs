using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator; // Assuming Program class is in this namespace
using Mono.Cecil;
using System;
using System.Linq; // Required for Linq operations like Select

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class TypeNameGenerationTests
    {
        private static ModuleDefinition _testModule;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var assemblyDef = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAssemblyForTypes", new Version(1, 0)),
                "TestModuleForTypes",
                ModuleKind.Dll);
            _testModule = assemblyDef.MainModule;
        }

        [TestMethod]
        public void GetTypeName_SystemInt32_ReturnsIntKeyword()
        {
            TypeReference typeRef = _testModule.TypeSystem.Int32;
            string expected = "int";
            string actual = Program.GetTypeName(typeRef);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_SystemString_ReturnsStringKeyword()
        {
            TypeReference typeRef = _testModule.TypeSystem.String;
            string expected = "string";
            string actual = Program.GetTypeName(typeRef);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_SystemVoid_ReturnsVoidKeyword()
        {
            TypeReference typeRef = _testModule.TypeSystem.Void;
            string expected = "void";
            string actual = Program.GetTypeName(typeRef);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_NullableInt_ReturnsIntQuestionMark()
        {
            // Create Nullable<T>
            var nullableDef = _testModule.ImportReference(typeof(Nullable<>)).Resolve();
            var genericInstance = new GenericInstanceType(nullableDef);
            genericInstance.GenericArguments.Add(_testModule.TypeSystem.Int32);
            
            string expected = "int?";
            string actual = Program.GetTypeName(genericInstance);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_SimpleGenericType_ReturnsCorrectFormat()
        {
            // Define a generic type like MyGeneric<T>
            var genericTypeDef = new TypeDefinition("MyNamespace", "MyGeneric`1", TypeAttributes.Public, _testModule.TypeSystem.Object);
            var genericParam = new GenericParameter("T", genericTypeDef);
            genericTypeDef.GenericParameters.Add(genericParam);
            _testModule.Types.Add(genericTypeDef); // Add to module if it's resolved from here

            // Create an instance MyGeneric<int>
            var instance = new GenericInstanceType(genericTypeDef);
            instance.GenericArguments.Add(_testModule.TypeSystem.Int32);

            string expected = "MyNamespace.MyGeneric<int>";
            string actual = Program.GetTypeName(instance);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_NestedGenericType_ReturnsCorrectFormat()
        {
            // MyOuterGeneric<MyInnerGeneric<string>>
            var outerTypeDef = new TypeDefinition("MyNamespace", "MyOuterGeneric`1", TypeAttributes.Public, _testModule.TypeSystem.Object);
            var outerParam = new GenericParameter("TOuter", outerTypeDef);
            outerTypeDef.GenericParameters.Add(outerParam);
            _testModule.Types.Add(outerTypeDef);

            var innerTypeDef = new TypeDefinition("MyNamespace", "MyInnerGeneric`1", TypeAttributes.Public, _testModule.TypeSystem.Object);
            var innerParam = new GenericParameter("TInner", innerTypeDef);
            innerTypeDef.GenericParameters.Add(innerParam);
            _testModule.Types.Add(innerTypeDef);
            
            var innerInstance = new GenericInstanceType(innerTypeDef);
            innerInstance.GenericArguments.Add(_testModule.TypeSystem.String); // MyInnerGeneric<string>

            var outerInstance = new GenericInstanceType(outerTypeDef);
            outerInstance.GenericArguments.Add(innerInstance); // MyOuterGeneric<MyInnerGeneric<string>>

            string expected = "MyNamespace.MyOuterGeneric<MyNamespace.MyInnerGeneric<string>>";
            string actual = Program.GetTypeName(outerInstance);
            Assert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void GetTypeName_GenericTypeDefinition_ReturnsNameWithGenericParameters()
        {
            var typeDef = new TypeDefinition("MyNamespace", "MyClass`2", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            typeDef.GenericParameters.Add(new GenericParameter("T1", typeDef));
            typeDef.GenericParameters.Add(new GenericParameter("T2", typeDef));

            string expected = "MyNamespace.MyClass<T1, T2>";
            string actual = Program.GetTypeName(typeDef);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_NonGenericType_ReturnsFullName()
        {
            var typeDef = new TypeDefinition("MyLib.Utils", "Helper", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            string expected = "MyLib.Utils.Helper";
            string actual = Program.GetTypeName(typeDef);
            Assert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void GetTypeName_GenericParameter_ReturnsName()
        {
            var ownerType = new TypeDefinition("MyNamespace", "MyOwnerClass", TypeAttributes.Public, _testModule.TypeSystem.Object);
            var genericParam = new GenericParameter("TKey", ownerType);
            // No need to add to ownerType.GenericParameters for this specific test, as GetTypeName directly uses the parameter.

            string expected = "TKey";
            string actual = Program.GetTypeName(genericParam);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void GetTypeName_TypeWithMultipleGenericArguments_ReturnsCorrectFormat()
        {
            var dictDef = _testModule.ImportReference(typeof(System.Collections.Generic.Dictionary<,>)).Resolve();
            var genericInstance = new GenericInstanceType(dictDef);
            genericInstance.GenericArguments.Add(_testModule.TypeSystem.String);
            genericInstance.GenericArguments.Add(_testModule.TypeSystem.Int32);

            // Note: Mono.Cecil might return System.Collections.Generic.Dictionary while C# keyword is dictionary
            // The GetTypeName method doesn't currently map this specific generic type to a keyword.
            // It maps System.String to string, System.Int32 to int etc.
            string expected = "System.Collections.Generic.Dictionary<string, int>";
            string actual = Program.GetTypeName(genericInstance);
            Assert.AreEqual(expected, actual);
        }


        [TestMethod]
        public void GetTypeName_NestedNonGenericType_ReturnsCorrectFullName()
        {
            // Simulating OuterType.InnerType
            var outerType = new TypeDefinition("MyTestNs", "OuterType", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            _testModule.Types.Add(outerType);
            var innerType = new TypeDefinition("", "InnerType", TypeAttributes.NestedPublic | TypeAttributes.Class, _testModule.TypeSystem.Object);
            outerType.NestedTypes.Add(innerType);

            // GetTypeName expects FullName with "/" for nested types from Cecil, which it converts to "."
            // Manually constructing such a TypeReference is tricky; usually, it's obtained from an existing type.
            // Let's use the `innerType` directly, its FullName should be "MyTestNs.OuterType/InnerType"
            
            string expected = "MyTestNs.OuterType.InnerType"; // After GetTypeName replaces "/"
            string actual = Program.GetTypeName(innerType); 
            Assert.AreEqual(expected, actual);
        }
    }
}
