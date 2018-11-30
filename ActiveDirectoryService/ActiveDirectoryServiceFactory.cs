using System;
using System.Collections.Generic;
using System.Linq;

namespace Affecto.ActiveDirectoryService
{
    public static class ActiveDirectoryServiceFactory
    {
        public static IActiveDirectoryService CreateActiveDirectoryService(string domainPath)
        {
            return CreateActiveDirectoryService(new[] { domainPath });
        }

        public static IActiveDirectoryService CreateActiveDirectoryService(IEnumerable<string> domainPaths)
        {
            IEnumerable<DomainPath> paths = domainPaths.Select(o => new DomainPath(o));
            return new ActiveDirectoryService(paths);
        }

        public static IActiveDirectoryService CreateCachedActiveDirectoryService(string domainPath, TimeSpan cacheDuration)
        {
            return CreateCachedActiveDirectoryService(new[] { domainPath }, cacheDuration);
        }

        public static IActiveDirectoryService CreateCachedActiveDirectoryService(IEnumerable<string> domainPaths, TimeSpan cacheDuration)
        {
            IEnumerable<DomainPath> paths = domainPaths.Select(o => new DomainPath(o));
            return new CachedActiveDirectoryService(paths, cacheDuration);
        }
    }
}