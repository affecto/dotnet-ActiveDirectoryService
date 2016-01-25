using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Affecto.ActiveDirectoryService.Tests
{
    [TestClass]
    public class AdDomainPathHandlerTests
    {
        [TestMethod]
        public void EscapeSlashCharacter()
        {
            var escapedPath = AdDomainPathHandler.Escape("CN=Teppo/ Testaaja/,OU=CAMA2,OU=ECM,DC=dev,DC=local");
            Assert.AreEqual(@"CN=Teppo\/ Testaaja\/,OU=CAMA2,OU=ECM,DC=dev,DC=local", escapedPath);
        }

        [TestMethod]
        public void EscapeDoubleSlashCharacter()
        {
            var escapedPath = AdDomainPathHandler.Escape("CN=Teppo// Testaaja//,OU=CAMA2,OU=ECM,DC=dev,DC=local");
            Assert.AreEqual(@"CN=Teppo\/\/ Testaaja\/\/,OU=CAMA2,OU=ECM,DC=dev,DC=local", escapedPath);
        }

        [TestMethod]
        public void DontEscapeNormalPath()
        {
            var escapedPath = AdDomainPathHandler.Escape("CN=Teppo Testaaja,OU=CAMA2,OU=ECM,DC=dev,DC=local");
            Assert.AreEqual("CN=Teppo Testaaja,OU=CAMA2,OU=ECM,DC=dev,DC=local", escapedPath);
        }
    }
}