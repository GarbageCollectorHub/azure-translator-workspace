using Microsoft.Azure.Cosmos;

namespace TranslatorAzure.Services.DbContainer
{
    // Резервное подключение к Cosmos DB для будущих пользовательских данных.
    // В текущем workflow переводчика не зарегистрировано и не используется.
    public class CosmosDbContainer(IConfiguration configuration) : IDbContainer
    {
        private readonly IConfiguration _configuration = configuration;
        private Container? _container;

        public Task<Container> GetContainerAsync()
        {
            if (_container != null)
            {
                return Task.FromResult(_container);
            }

            var cosmosSection = _configuration.GetSection("cosmos");
            var client = new CosmosClient(cosmosSection.GetValue<string>("connectionString"));
            var database = client.GetDatabase(cosmosSection.GetValue<string>("databaseName"));
            _container = database.GetContainer(cosmosSection.GetValue<string>("containerName"));

            return Task.FromResult(_container);
        }
    }
}
