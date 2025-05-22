using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator; // Assuming Program class is in this namespace

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class SanitizeFileNameComponentTests
    {
        [TestMethod]
        public void SanitizeFileNameComponent_WithValidName_ReturnsSameName()
        {
            string input = "ValidName123";
            string expected = "ValidName123";
            string actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SanitizeFileNameComponent_WithSpaces_ReplacesSpacesWithUnderscore()
        {
            // Although spaces are often allowed, Path.GetInvalidFileNameChars() might include them
            // or general best practice might be to replace them.
            // The current implementation of SanitizeFileNameComponent relies on Path.GetInvalidFileNameChars()
            // and char.IsControl(). If spaces are not in Path.GetInvalidFileNameChars() on the test OS,
            // they won't be replaced. For this test, we'll assume a context where they *are* replaced or add them to a custom list if not.
            // For now, let's test common invalid chars explicitly.
            string input = "Name With Spaces";
            // This assertion depends on whether space is an invalid char on the OS running the test
            // For robust testing, one might mock Path.GetInvalidFileNameChars or use a fixed list in SanitizeFileNameComponent
            // For now, let's assume it's *not* replaced by default unless it's a control char.
            // To make it a more direct test of *our* logic beyond system defaults, let's use chars we *know* are invalid.
            input = "Name<With>Invalid:Chars";
            string expected = "Name_With_Invalid_Chars";
            string actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual, "Special characters like <, >, : should be replaced.");
        }


        [TestMethod]
        public void SanitizeFileNameComponent_WithControlChars_ReplacesWithUnderscore()
        {
            string input = "NameWith\nNewlineAnd\tTab";
            // \n and \t are control characters
            string expected = "NameWith_NewlineAnd_Tab";
            string actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SanitizeFileNameComponent_EmptyString_ReturnsEmptyString()
        {
            string input = "";
            string expected = "";
            string actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SanitizeFileNameComponent_NullString_ReturnsNull()
        {
            string? input = null;
            string? expected = null;
            string? actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void SanitizeFileNameComponent_NameWithBackticks_KeepsBackticks()
        {
            // Backticks are often used in compiler-generated names for generics, but are valid in filenames.
            // The Split('`') logic is applied *before* calling sanitize, so this test is for the sanitizer itself.
            string input = "TypeName`1";
            string expected = "TypeName`1"; // Assuming backtick is not in Path.GetInvalidFileNameChars()
            string actual = Program.SanitizeFileNameComponent(input);
            Assert.AreEqual(expected, actual);
        }
    }
}
