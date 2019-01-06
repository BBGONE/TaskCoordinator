using System;
using ProtoBuf;

namespace TasksCoordinator.Test
{
    [ProtoContract]
    public class Payload
    {
        [ProtoMember(1)]
        public Guid ClientID;
        [ProtoMember(2)]
        public TaskWorkType WorkType { get; set; }
        [ProtoMember(3)]
        public DateTime CreateDate { get; set; }
        [ProtoMember(4)]
        public int TryCount;
        [ProtoMember(5)]
        public byte[] Result;
        [ProtoMember(6)]
        public bool RaiseError;
    }
}
