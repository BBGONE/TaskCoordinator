namespace TasksCoordinator.Common
{
    public interface ISerializer
    {
        T Deserialize<T>(byte[] bytes);
        byte[] Serialize<T>(T obj);
    }
}
