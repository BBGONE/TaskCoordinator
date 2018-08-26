namespace TasksCoordinator.Test.Interface
{
    public interface ISerializer
    {
        T Deserialize<T>(byte[] bytes);
        byte[] Serialize<T>(T obj);
    }
}
