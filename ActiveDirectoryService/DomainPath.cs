using System;

namespace Affecto.ActiveDirectoryService
{
    internal class DomainPath
    {
        private const string LdapPathPrefix = "LDAP://";

        public string Value { get; private set; }

        public DomainPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Domain path value must be defined.");
            }

            Value = value;
        }

        public string GetPathWithProtocol()
        {
            return LdapPathPrefix + GetPathWithoutProtocol();
        }

        public string GetPathWithoutProtocol()
        {
            if (Value.StartsWith(LdapPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return Value.Remove(0, LdapPathPrefix.Length);
            }

            return Value;
        }
    }
}