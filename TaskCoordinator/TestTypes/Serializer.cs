using System.IO;
using TasksCoordinator.Test.Interface;
using ProtoSerializer = ProtoBuf.Serializer;

namespace TasksCoordinator.Test
{
    public class Serializer : ISerializer
    {
        T ISerializer.Deserialize<T>(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return ProtoSerializer.Deserialize<T>(stream);
            }
        }

        byte[] ISerializer.Serialize<T>(T obj)
        {
            using (var stream = new MemoryStream())
            {
                ProtoSerializer.Serialize(stream, obj);
                return stream.ToArray();
            }
        }
    }
}
