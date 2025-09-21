using Microsoft.VisualStudio.TestTools.UnitTesting;
using Pjx_CreateCertificates;
using System;
using System.Collections.Generic;
using System.Text;

namespace Pjx_CreateCertificates.Tests
{
    [TestClass()]
    public class ProgramTests
    {
        /// <summary>
        /// To test the arguments "-h" and "-help"
        /// </summary>
        [TestMethod()]
        public void ReadArgs_HelpTest()
        {
            string password;
            string dnsName; 
            AsymmetricAlgorithms algorithm;

            // -help
            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "-h" });
            Assert.IsNull(password);
            Assert.IsNull(dnsName);
            // value of algorithm is not important in this scenario, so skip

            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "-help" });
            Assert.IsNull(password);
            Assert.IsNull(dnsName);
        }

        /// <summary>
        /// To test the regular arguments
        /// </summary>
        [TestMethod()]
        public void ReadArgs_Test()
        {
            string password;
            string dnsName;
            AsymmetricAlgorithms algorithm;

            // normal case with 2 arguments
            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "dns", "password" });
            Assert.AreEqual("password", password);
            Assert.AreEqual("dns", dnsName);
            Assert.AreEqual(AsymmetricAlgorithms.ECDSA, algorithm, "Default should be ECDSA");

            // normal case with 3 arguments, RSA
            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "dns", "password", "RSA" });
            Assert.AreEqual("password", password);
            Assert.AreEqual("dns", dnsName);
            Assert.AreEqual(AsymmetricAlgorithms.RSA, algorithm, "argument 3 is RSA");

            // normal case with 3 arguments, ECDSA
            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "dns", "password", "ECDSA" });
            Assert.AreEqual("password", password);
            Assert.AreEqual("dns", dnsName);
            Assert.AreEqual(AsymmetricAlgorithms.ECDSA, algorithm, "argument 3 is ECDSA");

            // test the case-insensitive of the 3rd argument
            (password, dnsName, algorithm) = Program.ReadArgs(new string[] { "dns", "password", "eCDsa" });
            Assert.AreEqual("password", password);
            Assert.AreEqual("dns", dnsName);
            Assert.AreEqual(AsymmetricAlgorithms.ECDSA, algorithm, "argument 3 is ECDSA");
        }

        /// <summary>
        /// When no arguments it should throw exception.
        /// </summary>
        [TestMethod()]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ReadArgs_NoArgsTest()
        {
            Program.ReadArgs(new string[] { });
        }

        /// <summary>
        /// If empty arguments it should throw exception.
        /// </summary>
        [TestMethod()]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ReadArgs_EmptyArgsTest()
        {
            Program.ReadArgs(new string[] { string.Empty, string.Empty });
        }

        [TestMethod()]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ReadArgs_OneArgsTest()
        {
            // one arg, but not "-h" nor "-help"
            Program.ReadArgs(new string[] { "-somethingWrong" });
        }
    }
}
