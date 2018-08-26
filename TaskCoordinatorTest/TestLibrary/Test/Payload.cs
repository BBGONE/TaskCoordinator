using System;

namespace TasksCoordinator.Test
{
    [Serializable]
    public class Payload
    {
        public Guid ClientID;
        public TaskWorkType WorkType { get; set; }
        public DateTime CreateDate { get; set; }
        public byte[] Result;
    }
}
