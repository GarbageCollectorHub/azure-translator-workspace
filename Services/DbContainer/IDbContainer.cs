using Microsoft.Azure.Cosmos;

namespace TranslatorAzure.Services.DbContainer
{
    public interface IDbContainer
    {
        Task<Container> GetContainerAsync();
    }
}
