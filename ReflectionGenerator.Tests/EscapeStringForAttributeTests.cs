using Microsoft.VisualStudio.TestTools.UnitTesting;
using ReflectionGenerator; // Assuming Program class is in this namespace

namespace ReflectionGenerator.Tests
{
    [TestClass]
    public class EscapeStringForAttributeTests
    {
        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithQuotes_EscapesQuotes()
        {
            string input = "This is a \"test\" message.";
            string expected = "This is a \\\"test\\\" message.";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithBackslashes_EscapesBackslashes()
        {
            string input = "Path is C:\\Temp\\File";
            string expected = "Path is C:\\\\Temp\\\\File";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithNewlines_EscapesNewlines()
        {
            string input = "Line1\nLine2";
            string expected = "Line1\\nLine2";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithCarriageReturns_EscapesCarriageReturns()
        {
            string input = "Line1\rLine2";
            string expected = "Line1\\rLine2";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithTabs_EscapesTabs()
        {
            string input = "Column1\tColumn2";
            string expected = "Column1\\tColumn2";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_WithMessageWithMixedSpecialChars_EscapesAll()
        {
            string input = "A \"mix\"\n of \\special\\ chars.";
            string expected = "A \\\"mix\\\"\\n of \\\\special\\\\ chars.";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_EmptyString_ReturnsEmptyString()
        {
            string input = "";
            string expected = "";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_NullString_ReturnsNull()
        {
            string? input = null;
            string? expected = null;
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void EscapeStringForAttribute_NoSpecialChars_ReturnsSameString()
        {
            string input = "This is a simple message.";
            string expected = "This is a simple message.";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }
        
        [TestMethod]
        public void EscapeStringForAttribute_WithControlChars_EscapesToUnicode()
        {
            string input = "Bell sound \a and backspace \b"; // \a (alert) and \b (backspace) are control chars
            string expected = "Bell sound \\u0007 and backspace \\u0008";
            string? actual = Program.EscapeStringForAttribute(input);
            Assert.AreEqual(expected, actual);
        }
    }
}
