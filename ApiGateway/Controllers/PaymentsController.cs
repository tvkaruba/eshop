using ApiGateway.Models.PaymentService.Requests;
using ApiGateway.Models.PaymentService.Responses;
using Microsoft.AspNetCore.Mvc;
using Proto = Shared.Contracts.Payments;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly Proto.PaymentsService.PaymentsServiceClient _paymentsClient;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(Proto.PaymentsService.PaymentsServiceClient paymentsClient, ILogger<PaymentsController> logger)
    {
        _paymentsClient = paymentsClient;
        _logger = logger;
    }

    [HttpPost("accounts")]
    [ProducesResponseType<CreateAccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<CreateAccountResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<CreateAccountResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> CreateAccount([FromBody] CreateAccountRequest request)
    {
        try
        {
            var grpcRequest = new Proto.CreateAccountRequest
            {
                UserId = request.UserId.ToString(),
            };

            var grpcResponse = await _paymentsClient.CreateAccountAsync(grpcRequest);

            var response = new CreateAccountResponse(grpcResponse.Success, grpcResponse.Message, Guid.Parse(grpcResponse.AccountId));
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании счета для пользователя {UserId}", request.UserId);
            return TypedResults.InternalServerError(new CreateAccountResponse(Success: false, "Внутренняя ошибка сервера"));
        }
    }

    [HttpPost("accounts/topup")]
    [ProducesResponseType<TopUpAccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TopUpAccountResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<TopUpAccountResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> TopUpAccount([FromBody] TopUpAccountRequest request)
    {
        try
        {
            if (request.Amount <= 0)
                return TypedResults.BadRequest(new TopUpAccountResponse(Success: false, "Сумма должна быть больше нуля", NewBalance: 0));

            var grpcRequest = new Proto.TopUpAccountRequest
            {
                UserId = request.UserId.ToString(),
                Amount = (double)request.Amount
            };

            var grpcResponse = await _paymentsClient.TopUpAccountAsync(grpcRequest);

            var response = new TopUpAccountResponse(grpcResponse.Success, grpcResponse.Message, (decimal)grpcResponse.NewBalance);
            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при пополнении счета пользователя {UserId}", request.UserId);
            return TypedResults.InternalServerError(new CreateAccountResponse(Success: false, "Внутренняя ошибка сервера"));
        }
    }

    [HttpGet("accounts/{userId}/balance")]
    [ProducesResponseType<GetBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<GetBalanceResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<GetBalanceResponse>(StatusCodes.Status500InternalServerError)]
    public async Task<IResult> GetBalance(Guid userId)
    {
        try
        {
            if (userId == Guid.Empty)
                return TypedResults.BadRequest(new GetBalanceResponse(Success: false, "Идентификатор пользователя обязателен", Balance: 0));

            var grpcRequest = new Proto.GetBalanceRequest
            {
                UserId = userId.ToString(),
            };

            var grpcResponse = await _paymentsClient.GetBalanceAsync(grpcRequest);

            var response = new GetBalanceResponse(
                grpcResponse.Success,
                grpcResponse.Message,
                (decimal)grpcResponse.Balance,
                Guid.Parse(grpcResponse.AccountId));

            return TypedResults.Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении баланса пользователя {UserId}", userId);
            return TypedResults.InternalServerError(new GetBalanceResponse(Success: false, "Внутренняя ошибка сервера", Balance: 0));
        }
    }
}