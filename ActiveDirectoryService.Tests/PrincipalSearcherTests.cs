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
            using (sut = new PrincipalSearcher(searchRoot))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("displayName"));
            }
        }

        [TestMethod]
        public void ObjectGuidIsLoadedOnSearch()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("objectGuid"));
            }
        }

        [TestMethod]
        public void MemberPropertyIsLoadedOnSearch()
        {
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("Member"));
            }
        }

        [TestMethod]
        public void AdditionalPropertiesAreLoadedOnSearch()
        {
            var properties = new[] { "Prop1", "Prop2" };
            
            using (searchRoot = new DirectoryEntry())
            using (sut = new PrincipalSearcher(searchRoot, properties))
            {
                Assert.IsTrue(sut.PropertiesToLoad.Contains("Prop1"));
                Assert.IsTrue(sut.PropertiesToLoad.Contains("Prop2"));
            }
        }
    }
}