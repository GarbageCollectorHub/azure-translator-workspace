using Microsoft.Azure.Cosmos;

namespace TranslatorAzure.Infrastructure.Cosmos;

public interface ICosmosContainerProvider
{
    Task<Container> GetContainerAsync(CancellationToken cancellationToken);
}