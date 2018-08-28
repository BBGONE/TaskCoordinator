using System.Threading.Tasks;

namespace SSSB
{
    public interface IMessageHandler<T>
    {
        Task<T> HandleMessage(ISSSBService sender, T e);
    }
}
