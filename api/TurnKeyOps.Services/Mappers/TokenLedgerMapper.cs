using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TokenLedgerMapper
    {
        public static TokenLedgerDto ToDto(TokenLedger entity, Guid id)
        {
            return new TokenLedgerDto
            {
                Id = id,
                UserId = entity.UserId,
                Date = entity.Date,
                TokenType = entity.TokenType,
                TokensCredited = entity.TokensCredited,
                TokensDebited = entity.TokensDebited,
                Description = entity.Description,
                BalanceAfterTransaction = entity.BalanceAfterTransaction
            };
        }

        public static TokenLedger ToEntity(TokenLedgerDto dto, string partitionKey, string rowKey)
        {
            return new TokenLedger
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                UserId = dto.UserId,
                Date = dto.Date,
                TokenType = dto.TokenType,
                TokensCredited = dto.TokensCredited,
                TokensDebited = dto.TokensDebited,
                Description = dto.Description,
                BalanceAfterTransaction = dto.BalanceAfterTransaction,
                IsDeleted = false
            };
        }
    }
}
