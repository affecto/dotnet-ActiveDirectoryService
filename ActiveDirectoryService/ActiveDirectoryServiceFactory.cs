using System;

namespace Affecto.ActiveDirectoryService
{
    public static class ActiveDirectoryServiceFactory
    {
        public static IActiveDirectoryService CreateActiveDirectoryService(string domainPath)
        {
            return new ActiveDirectoryService(domainPath);
        }

        public static IActiveDirectoryService CreateCachedActiveDirectoryService(string domainPath, TimeSpan cacheDuration)
        {
            return new CachedActiveDirectoryService(domainPath, cacheDuration);
        }
    }
}
