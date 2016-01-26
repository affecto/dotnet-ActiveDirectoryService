// ReSharper disable ObjectCreationAsStatement

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affecto.ActiveDirectoryService.Tests
{
    [TestClass]
    public class DomainPathTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainNameCannotBeNull()
        {
            new DomainPath(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainNameCannotBeEmpty()
        {
            new DomainPath("");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainNameCannotBeWhitespace()
        {
            new DomainPath(" ");
        }

        [TestMethod]
        public void DomainNameIsInitialized()
        {
            var sut = new DomainPath("dc");
            Assert.AreEqual("dc", sut.Value);
        }

        [TestMethod]
        public void DomainNameWithoutPrefix()
        {
            var sut = new DomainPath("dc");

            Assert.AreEqual("LDAP://dc", sut.GetPathWithProtocol());
            Assert.AreEqual("dc", sut.GetPathWithoutProtocol());
        }

        [TestMethod]
        public void DomainNameWithLowerCasePrefix()
        {
            var sut = new DomainPath("ldap://dc");

            Assert.AreEqual("LDAP://dc", sut.GetPathWithProtocol());
            Assert.AreEqual("dc", sut.GetPathWithoutProtocol());
        }

        [TestMethod]
        public void DomainNameWithUpperCasePrefix()
        {
            var sut = new DomainPath("LDAP://dc");

            Assert.AreEqual("LDAP://dc", sut.GetPathWithProtocol());
            Assert.AreEqual("dc", sut.GetPathWithoutProtocol());
        }
    }
}