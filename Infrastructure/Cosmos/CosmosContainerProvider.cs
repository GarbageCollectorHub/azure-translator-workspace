using Microsoft.Azure.Cosmos;

namespace TranslatorAzure.Infrastructure.Cosmos;

public sealed class CosmosContainerProvider(IConfiguration configuration) : ICosmosContainerProvider, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private CosmosClient? _client;
    private Container? _container;

    public async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        if (_container != null)
        {
            return _container;
        }

        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_container != null)
            {
                return _container;
            }

            var section = configuration.GetSection("cosmos");

            var connectionString = section.GetValue<string>("connectionString");
            var databaseName = section.GetValue<string>("databaseName");
            var containerName = section.GetValue<string>("containerName");

            if (string.IsNullOrWhiteSpace(connectionString)
                || string.IsNullOrWhiteSpace(databaseName)
                || string.IsNullOrWhiteSpace(containerName))
            {
                throw new InvalidOperationException("Cosmos DB configuration is missing.");
            }

            _client = new CosmosClient(connectionString);
            var database = _client.GetDatabase(databaseName);
            _container = database.GetContainer(containerName);

            return _container;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _lock.Dispose();
    }
}

