using System;

namespace SSSB
{
    internal class ErrorMessage
    {
        public Guid MessageID;
        public int ErrorCount;
        public DateTime LastAccess;
        public Exception FirstError;
    }
}
