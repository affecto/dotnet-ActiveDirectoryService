using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affecto.ActiveDirectoryService.Tests
{
    [TestClass]
    public class ActiveDirectoryServiceTests
    {
        private ActiveDirectoryService sut;

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DomainPathCannotBeNull()
        {
            sut = new ActiveDirectoryService(null);
        }
    }
}