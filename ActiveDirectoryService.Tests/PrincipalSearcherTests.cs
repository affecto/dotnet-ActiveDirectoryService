using System.DirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affecto.ActiveDirectoryService.Tests
{
    [TestClass]
    public class PrincipalSearcherTests
    {
        private const string UserName = "user";

        private PrincipalSearcher sut;
        private DirectoryEntry searchRoot;

        [TestMethod]
        public void DisplayNameIsLoadedOnSearch()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, UserName))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("displayName"));
            }
        }

        [TestMethod]
        public void ObjectGuidIsLoadedOnSearch()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, UserName))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("objectGuid"));
            }
        }

        [TestMethod]
        public void MemberPropertyIsLoadedOnSearch()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, UserName))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("Member"));
            }
        }

        [TestMethod]
        public void SearchIsFilteredWithUserName()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, UserName))
            {
                Assert.AreEqual("(sAMAccountName=user)", sut.Filter);
            }
        }

        [TestMethod]
        public void DomainIsRemovedFromUserNameBeforeTheFilterIsSet()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, @"fi\user"))
            {
                Assert.AreEqual("(sAMAccountName=user)", sut.Filter);
            }
        }
    }
}
