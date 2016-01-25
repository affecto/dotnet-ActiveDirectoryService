using System;

namespace Affecto.ActiveDirectoryService
{
    public class ActiveDirectoryException : Exception
    {
        public ActiveDirectoryException(string message)
            : base(message)
        {
        }
    }
}
