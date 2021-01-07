using System;

namespace TasksCoordinator.Test
{
    public class Payload
    {
        public Guid ClientID;
        public TaskWorkType WorkType { get; set; }
        public DateTime CreateDate { get; set; }
        public int TryCount;
        public byte[] Result;
        public bool RaiseError;
    }
}
