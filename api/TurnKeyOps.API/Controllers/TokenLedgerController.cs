using System.Security.Claims;
using Azure;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Services;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedInsights.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    public class TokenLedgerController : ApiControllerBase
    {
        private readonly ITokenLedgerService _service;
        public TokenLedgerController(ITokenLedgerService service): base()
        {
            _service = service;
        }

        // POST api/TokenLedger
        [HttpPost]
        public async Task<IActionResult> AddTransaction([FromBody] TokenLedgerDto dto)
        {
            var result = await _service.AddTransactionAsync(dto);
            return CreatedAtAction(nameof(GetUserTransactions),
                new { tenantId = result.TenantId, userId = result.UserId },
                result);
        }

        // GET api/TokenLedger/
        [HttpGet]
        public async Task<IActionResult> GetTransactions()
        {
            var results = await _service.GetAllTransactionsAsync();
            return OkResponse(results);
        }

        // GET api/TokenLedger/{userId}
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserTransactions(Guid userId)
        {
            var results = await _service.GetTransactionsByUserAsync(userId);
            return OkResponse(results);
        }

        // GET api/TokenLedger/balance
        [HttpGet("/balance")]
        public async Task<IActionResult> GetTenantBalance()
        {
            var balance = await _service.GetBalanceAsync();
            return OkResponse(balance);
        }
    }
}
