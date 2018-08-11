using System.Threading.Tasks;
namespace SSSB
{
    public interface IMessageHandler<T>
    {
        Task<T> HandleMessage(SSSB.BaseSSSBService sender, T e);
    }
}
