using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator;
using Mono.Cecil;
using System;
using System.IO;
using System.Linq;

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class GenericConstraintTests
    {
        private static ModuleDefinition _testModule = null!;
        private static string _tempOutputDir = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            var assemblyDef = AssemblyDefinition.CreateAssembly(
                new AssemblyNameDefinition("TestAssemblyForConstraints", new Version(1, 0)),
                "TestModuleForConstraints",
                ModuleKind.Dll);
            _testModule = assemblyDef.MainModule;
            _tempOutputDir = Path.Combine(Path.GetTempPath(), "ReflectionGenTests_Constraints");
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

        private string GenerateAndReadFile(TypeDefinition typeDef)
        {
            Program.GenerateTypeScaffolding(typeDef, _tempOutputDir);
            string expectedFileName = Path.Combine(_tempOutputDir, $"{Program.SanitizeFileNameComponent(typeDef.Namespace)}.{Program.SanitizeFileNameComponent(typeDef.Name.Split('`')[0])}.cs");
            return File.ReadAllText(expectedFileName);
        }
        
        private TypeReference ImportType(Type type)
        {
            return _testModule.ImportReference(type);
        }

        [TestMethod]
        public void GenerateTypeScaffolding_GenericTypeWithClassConstraint_GeneratesCorrectWhereClause()
        {
            var typeDef = new TypeDefinition("MyNs", "MyGenericClass`1", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var pT = new GenericParameter("T", typeDef);
            pT.HasReferenceTypeConstraint = true; // class constraint
            typeDef.GenericParameters.Add(pT);
            _testModule.Types.Add(typeDef);

            string output = GenerateAndReadFile(typeDef);
            Assert.IsTrue(output.Contains("public partial class MyGenericClass<T> where T : class"));
            _testModule.Types.Remove(typeDef); // Clean up
        }

        [TestMethod]
        public void GenerateTypeScaffolding_GenericTypeWithStructConstraint_GeneratesCorrectWhereClause()
        {
            var typeDef = new TypeDefinition("MyNs", "MyValueClass`1", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var pT = new GenericParameter("T", typeDef);
            pT.HasNotNullableValueTypeConstraint = true; // struct constraint
            typeDef.GenericParameters.Add(pT);
             _testModule.Types.Add(typeDef);

            string output = GenerateAndReadFile(typeDef);
            // Note: struct constraint implies new(), but current generator logic adds new() explicitly if HasDefaultConstructorConstraint is true
            // Let's assume for now that HasDefaultConstructorConstraint is not set, or adjust if it is.
            // The `FormatEnumMemberValue` has `!param.HasNotNullableValueTypeConstraint` for `new()`
            // The `GenerateTypeScaffolding` for type constraints is: if (param.HasDefaultConstructorConstraint && !param.HasNotNullableValueTypeConstraint) paramConstraints.Add("new()");
            // So, for a pure struct constraint, it should be `where T : struct`
            Assert.IsTrue(output.Contains("public partial class MyValueClass<T> where T : struct"));
             _testModule.Types.Remove(typeDef);
        }

        [TestMethod]
        public void GenerateTypeScaffolding_GenericTypeWithNewConstraint_GeneratesCorrectWhereClause()
        {
            var typeDef = new TypeDefinition("MyNs", "MyCtorClass`1", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var pT = new GenericParameter("T", typeDef);
            pT.HasDefaultConstructorConstraint = true; // new() constraint
            typeDef.GenericParameters.Add(pT);
            _testModule.Types.Add(typeDef);

            string output = GenerateAndReadFile(typeDef);
            Assert.IsTrue(output.Contains("public partial class MyCtorClass<T> where T : new()"));
            _testModule.Types.Remove(typeDef);
        }
        
        [TestMethod]
        public void GenerateTypeScaffolding_GenericTypeWithInterfaceConstraint_GeneratesCorrectWhereClause()
        {
            var typeDef = new TypeDefinition("MyNs", "MyInterfaceConstrainedClass`1", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var pT = new GenericParameter("T", typeDef);
            pT.Constraints.Add(new GenericParameterConstraint(ImportType(typeof(IDisposable))));
            typeDef.GenericParameters.Add(pT);
            _testModule.Types.Add(typeDef);

            string output = GenerateAndReadFile(typeDef);
            Assert.IsTrue(output.Contains("public partial class MyInterfaceConstrainedClass<T> where T : System.IDisposable"));
             _testModule.Types.Remove(typeDef);
        }

        [TestMethod]
        public void GenerateTypeScaffolding_GenericTypeWithMultipleConstraints_GeneratesCorrectWhereClause()
        {
            var typeDef = new TypeDefinition("MyNs", "MyMultiConstrainedClass`1", TypeAttributes.Public | TypeAttributes.Class, _testModule.TypeSystem.Object);
            var pT = new GenericParameter("T", typeDef);
            pT.HasReferenceTypeConstraint = true;
            pT.Constraints.Add(new GenericParameterConstraint(ImportType(typeof(IComparable))));
            pT.HasDefaultConstructorConstraint = true;
            typeDef.GenericParameters.Add(pT);
            _testModule.Types.Add(typeDef);
            
            string output = GenerateAndReadFile(typeDef);
            // Order might vary slightly based on implementation, but all constraints should be there.
            // Current implementation: class, specific types, new()
            Assert.IsTrue(output.Contains("public partial class MyMultiConstrainedClass<T> where T : class, System.IComparable, new()"));
            _testModule.Types.Remove(typeDef);
        }

    }
}
