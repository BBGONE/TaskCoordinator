using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Shared.Errors
{
    [Serializable]
    public class DBWrapperException : PPSException
    {
        public DBWrapperException()
            : base()
        {
        }

        public DBWrapperException(string message)
            : base(message)
        {
        }

        public DBWrapperException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public DBWrapperException(ILog log)
            : base(log)
        {
        }

        public DBWrapperException(string message, ILog log)
            : base(message, log)
        {
        }

        public DBWrapperException(string message, Exception innerException, ILog log)
            : base(message, innerException, log)
        {
        }

        public DBWrapperException(string message, string shortMessage, string vocalMessage)
            : base(message, shortMessage, vocalMessage)
        {
        }

        public DBWrapperException(string message, string shortMessage, string vocalMessage, ILog log)
            : base(message, shortMessage, vocalMessage, log)
        {
        }

        public DBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException)
            : base(message, shortMessage, vocalMessage, innerException)
        {
        }

        public DBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException, ILog log)
            : base(message, shortMessage, vocalMessage, innerException, log)
        {
        }

        protected DBWrapperException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class DeadLockDBWrapperException : DBWrapperException
    {
        public DeadLockDBWrapperException()
            : base()
        {
        }

        public DeadLockDBWrapperException(string message)
            : base(message)
        {
        }

        public DeadLockDBWrapperException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public DeadLockDBWrapperException(ILog log)
            : base(log)
        {
        }

        public DeadLockDBWrapperException(string message, ILog log)
            : base(message, log)
        {
        }

        public DeadLockDBWrapperException(string message, Exception innerException, ILog log)
            : base(message, innerException, log)
        {
        }

        public DeadLockDBWrapperException(string message, string shortMessage, string vocalMessage)
            : base(message, shortMessage, vocalMessage)
        {
        }

        public DeadLockDBWrapperException(string message, string shortMessage, string vocalMessage, ILog log)
            : base(message, shortMessage, vocalMessage, log)
        {
        }

        public DeadLockDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException)
            : base(message, shortMessage, vocalMessage, innerException)
        {
        }

        public DeadLockDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException, ILog log)
            : base(message, shortMessage, vocalMessage, innerException, log)
        {
        }

        protected DeadLockDBWrapperException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class UniqueConstraintDBWrapperException : DBWrapperException
    {
        public UniqueConstraintDBWrapperException()
            : base()
        {
        }

        public UniqueConstraintDBWrapperException(string message)
            : base(message)
        {
        }

        public UniqueConstraintDBWrapperException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public UniqueConstraintDBWrapperException(ILog log)
            : base(log)
        {
        }

        public UniqueConstraintDBWrapperException(string message, ILog log)
            : base(message, log)
        {
        }

        public UniqueConstraintDBWrapperException(string message, Exception innerException, ILog log)
            : base(message, innerException, log)
        {
        }

        public UniqueConstraintDBWrapperException(string message, string shortMessage, string vocalMessage)
            : base(message, shortMessage, vocalMessage)
        {
        }

        public UniqueConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, ILog log)
            : base(message, shortMessage, vocalMessage, log)
        {
        }

        public UniqueConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException)
            : base(message, shortMessage, vocalMessage, innerException)
        {
        }

        public UniqueConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException, ILog log)
            : base(message, shortMessage, vocalMessage, innerException, log)
        {
        }

        protected UniqueConstraintDBWrapperException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class ReferenceConstraintDBWrapperException : DBWrapperException
    {
        public ReferenceConstraintDBWrapperException()
            : base()
        {
        }

        public ReferenceConstraintDBWrapperException(string message)
            : base(message)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ReferenceConstraintDBWrapperException(ILog log)
            : base(log)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, ILog log)
            : base(message, log)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, Exception innerException, ILog log)
            : base(message, innerException, log)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, string shortMessage, string vocalMessage)
            : base(message, shortMessage, vocalMessage)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, ILog log)
            : base(message, shortMessage, vocalMessage, log)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException)
            : base(message, shortMessage, vocalMessage, innerException)
        {
        }

        public ReferenceConstraintDBWrapperException(string message, string shortMessage, string vocalMessage, Exception innerException, ILog log)
            : base(message, shortMessage, vocalMessage, innerException, log)
        {
        }

        protected ReferenceConstraintDBWrapperException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    public class DBWrapperExceptionsHelper
    {
        private static ILog _log = LogFactory.Instance;

        public const int SqlServerUniqueConstraintErrorNumber = 2627;
        public const int SqlServerUniqueIndexErrorNumber = 2601;
        public const int SqlServerReferenceConstraintErrorNumber = 547;
        public const int SqlServerDeadLockErrorNumber = 1205;
        public const int SqlServerWaitResourceRerunQueryErrorNumber = 8645;

        public static void ThrowError(SqlException ex)
        {
            ThrowError(ex, _log); 
        }

        public static void ThrowError(SqlException ex, ILog log)
        {
            ThrowError(ex, string.Empty, log);
        }

        public static void ThrowError(SqlException ex, string UserFriendlyMessage, ILog log)
        {
            Debug.Assert(ex != null,"Parameter ex must not be null");

            /*
            //TODO: Убрать этот workaround, когда MS соизволит пофиксить баг http://lab.msdn.microsoft.com/ProductFeedback/viewfeedback.aspx?feedbackid=c64491d9-d1d9-46cd-b1cb-7b4a9e2a9674
            if ((ex.Message.Contains("New request is not allowed to start because it should come with valid transaction descriptor")
                || ex.Message.Contains("A severe error occurred on the current command")) && sqlConnection != null)
            {
                SqlConnection.ClearPool(sqlConnection);
            }
            */

            if (ex.Number == SqlServerUniqueConstraintErrorNumber || ex.Number == SqlServerUniqueIndexErrorNumber)
            {
                if (string.IsNullOrEmpty(UserFriendlyMessage))
                    throw new UniqueConstraintDBWrapperException(ex.Message, ex, log);
                else
                    throw new UniqueConstraintDBWrapperException(UserFriendlyMessage, ex, log);
            }

            if (ex.Number == SqlServerReferenceConstraintErrorNumber)
            {
                if (string.IsNullOrEmpty(UserFriendlyMessage))
                    throw new ReferenceConstraintDBWrapperException(ex.Message, ex, log);
                else
                    throw new ReferenceConstraintDBWrapperException(UserFriendlyMessage, ex, log);
            }

            if (ex.Number == SqlServerDeadLockErrorNumber)
            {
                if (string.IsNullOrEmpty(UserFriendlyMessage))
                    throw new DeadLockDBWrapperException(ex.Message, ex, log);
                else
                    throw new DeadLockDBWrapperException(UserFriendlyMessage, ex, log);
            }

            if (string.IsNullOrEmpty(UserFriendlyMessage))
                throw new DBWrapperException(UserFriendlyMessage, ex, log);
            else
                throw new DBWrapperException("Ошибка базы данных", ex, log);
        }
    }		

}
