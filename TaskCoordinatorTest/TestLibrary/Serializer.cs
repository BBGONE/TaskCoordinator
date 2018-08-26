using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using TasksCoordinator.Test.Interface;

namespace TasksCoordinator.Test
{
    public class Serializer : ISerializer
    {
        private BinaryFormatter formatter = new BinaryFormatter();

        T ISerializer.Deserialize<T>(byte[] bytes)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                return (T)formatter.Deserialize(stream);
            }
        }

        byte[] ISerializer.Serialize<T>(T obj)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, obj);
                return stream.ToArray();
            }
        }
    }
}
