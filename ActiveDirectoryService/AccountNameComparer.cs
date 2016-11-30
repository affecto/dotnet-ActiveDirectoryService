using System;
using System.Collections.Generic;

namespace Affecto.ActiveDirectoryService
{
    public class AccountNameComparer : IEqualityComparer<string>
    {
        public bool Equals(string accountName1, string accountName2)
        {
            if (accountName1 == null)
            {
                return (accountName2 == null);
            }

            if (accountName2 == null)
            {
                return false;
            }

            return GetAccountNameWithoutDomain(accountName1).Equals(GetAccountNameWithoutDomain(accountName2), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string accountName)
        {
            if (accountName == null)
            {
                return 0;
            }

            return GetAccountNameWithoutDomain(accountName).GetHashCode();
        }

        private static string GetAccountNameWithoutDomain(string accountName)
        {
            if (accountName == null)
            {
                return null;
            }

            if (accountName.Contains(@"\"))
            {
                return accountName.Split('\\')[1];
            }

            return accountName;
        }
    }
}