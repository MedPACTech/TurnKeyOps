using MedInsights.Lib.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MedInsights.Services
{
    public interface ITokenLedgerService
    {
        Task<TokenLedgerDto> AddTransactionAsync(TokenLedgerDto dto);

        Task<IEnumerable<TokenLedgerDto>> GetAllTransactionsAsync();

        Task<IEnumerable<TokenLedgerDto>> GetTransactionsByUserAsync(Guid userId);

        Task<int> GetBalanceAsync();
    }
}
