using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class TokenLedgerService : ITokenLedgerService
    {
        private readonly ITokenLedgerRepository _repository;
        private readonly IProcessingTokenLedgerRepository _processingRepository;
        private readonly ServiceBusClient _busClient;
        private readonly IUserContext _userContext;

        private readonly string _queueName = "token-transactions";

        public TokenLedgerService(
            ServiceBusClient busClient,
            IUserContext userContext,
            ITokenLedgerRepository repository,
            IProcessingTokenLedgerRepository processingRepository)
        {
            _repository = repository;
            _processingRepository = processingRepository;
            _busClient = busClient;
            _userContext = userContext;
        }

        public async Task<TokenLedgerDto> AddTransactionAsync(TokenLedgerDto dto)
        {
            if (dto is null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.TokensCredited == 0 && dto.TokensDebited == 0)
                throw new ArgumentException("Either TokensCredited or TokensDebited must be non-zero.");

            var id = Guid.NewGuid();

            var message = new TokenTransactionMessage
            {
                Id = id,
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                TokensCredited = dto.TokensCredited,
                TokensDebited = dto.TokensDebited,
                TokenType = dto.TokenType,
                Description = dto.Description,
                RequestedAt = DateTime.UtcNow,
            };

            var processingEntity = new ProcessingTokenLedger
            {
                PartitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId),
                Id = id,
                RowKey = RepositoryKeyHelper.ToOrderedRowKey(id),
                MessageId = id,
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                TokensCredited = dto.TokensCredited,
                TokensDebited = dto.TokensDebited,
                TokenType = dto.TokenType,
                Description = dto.Description,
                RequestedAt = DateTime.UtcNow,
                Completed = false
            };

            await _processingRepository.SaveAsync(processingEntity);

            var sender = _busClient.CreateSender(_queueName);
            var body = BinaryData.FromObjectAsJson(message);
            var busMessage = new ServiceBusMessage(body)
            {
                MessageId = id.ToString()
            };

            await sender.SendMessageAsync(busMessage);

            return new TokenLedgerDto
            {
                Id = id,
                TenantId = _userContext.TenantId,
                UserId = _userContext.UserId,
                Date = message.RequestedAt,
                TokenType = dto.TokenType,
                TokensCredited = dto.TokensCredited,
                TokensDebited = dto.TokensDebited,
                Description = dto.Description,
                BalanceAfterTransaction = 0
            };
        }

        public async Task<IEnumerable<TokenLedgerDto>> GetAllTransactionsAsync()
        {
            var tenantId = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var entities = await _repository.GetByTenantAsync(tenantId);

            return entities.Select(entity =>
            {
                var id = entity.Id;
                if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey))
                {
                    var firstSegment = entity.RowKey.Split('|', 2)[0];
                    if (Guid.TryParse(firstSegment, out var parsed))
                        id = parsed;
                }

                return TokenLedgerMapper.ToDto(entity, id);
            }).ToList();
        }

        public async Task<IEnumerable<TokenLedgerDto>> GetTransactionsByUserAsync(Guid userId)
        {
            var tenantId = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var userIdStr = EntityKeyPolicy.Row(userId);
            var entities = await _repository.GetByUserAsync(tenantId, userIdStr);
            return entities.Select(entity =>
            {
                var id = entity.Id;
                if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey))
                {
                    var firstSegment = entity.RowKey.Split('|', 2)[0];
                    if (Guid.TryParse(firstSegment, out var parsed))
                        id = parsed;
                }

                return TokenLedgerMapper.ToDto(entity, id);
            }).ToList();
        }

        public async Task<int> GetBalanceAsync()
        {
            var tenantId = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var last = await _repository.GetLatestByTenantAsync(tenantId);
            return last?.BalanceAfterTransaction ?? 0;
        }
    }
}

