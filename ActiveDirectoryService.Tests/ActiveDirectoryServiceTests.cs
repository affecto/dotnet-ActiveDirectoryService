using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affecto.ActiveDirectoryService.Tests
{
    [TestClass]
    public class ActiveDirectoryServiceTests
    {
        private ActiveDirectoryService sut;

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainPathCannotBeNull()
        {
            sut = new ActiveDirectoryService(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DomainPathCannotBeEmpty()
        {
            sut = new ActiveDirectoryService(string.Empty);
        }

        //[TestMethod]
        //public void ActiveDirectoryTest()
        //{
        //    sut = new CachedActiveDirectoryService(@"LDAP://hkidev01.dev.local", new TimeSpan(0, 0, 0, 30));
        //    sut.GetUser("dev\\jarvijus"); // add user to cache
        //    sut.GetUser("dev\\jarvijus"); // get user from cache

        //    CachedActiveDirectoryService sut2 = new CachedActiveDirectoryService(@"LDAP://hkidev01.dev.local", new TimeSpan(0, 0, 0, 30));
        //    sut2.GetUser("dev\\valtojoh"); // get user from cache
        //    sut2.GetUser("dev\\valtojoh"); // get user from cache
        //}

        //[TestMethod]
        //public void GetGroupMemberPrincipals()
        //{
        //    sut = new ActiveDirectoryService(@"LDAP://hkidev01.dev.local");
        //    var result = sut.GetGroupMemberPrincipals("Cama2Handlers", true);
        //    Assert.AreEqual(result.Select(r => r.NativeGuid).Distinct().Count(), result.Count());
        //}

        //[TestMethod]
        //public void CachedGetGroupMemberPrincipals()
        //{
        //    sut = new CachedActiveDirectoryService(@"LDAP://hkidev01.dev.local", new TimeSpan(0, 0, 0, 30));
        //    var result = sut.GetGroupMemberPrincipals("Cama2Handlers", true);
        //    Assert.AreEqual(result.Select(r => r.NativeGuid).Distinct().Count(), result.Count());
        //    var cachedResult = sut.GetGroupMemberPrincipals("Cama2Handlers", true);
        //    Assert.AreEqual(result.Count(), cachedResult.Count());
        //}
    }
}