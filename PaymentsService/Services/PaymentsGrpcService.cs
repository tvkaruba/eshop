using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PaymentsService.Data;
using PaymentsService.Models;
using Shared.Contracts.Payments;
using Shared.Infrastructure.Events;
using Shared.Infrastructure.Interfaces;
using Shared.Infrastructure.Models;
using System.Text.Json;

namespace PaymentsService.Services;

public class PaymentsGrpcService : Shared.Contracts.Payments.PaymentsService.PaymentsServiceBase
{
    private readonly PaymentsDbContext _context;

    private readonly IKafkaProducer _kafkaProducer;

    private readonly ICacheService _cacheService;

    private readonly ILogger<PaymentsGrpcService> _logger;

    public PaymentsGrpcService(
        PaymentsDbContext context,
        IKafkaProducer kafkaProducer,
        ICacheService cacheService,
        ILogger<PaymentsGrpcService> logger)
    {
        _context = context;
        _kafkaProducer = kafkaProducer;
        _cacheService = cacheService;
        _logger = logger;
    }

    public override async Task<CreateAccountResponse> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Создание счета для пользователя: {UserId}", request.UserId);

            var existingAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == request.UserId);

            if (existingAccount != null)
            {
                return new CreateAccountResponse
                {
                    Success = false,
                    Message = "У пользователя уже есть счет",
                    AccountId = existingAccount.Id.ToString()
                };
            }

            var account = new Account
            {
                UserId = request.UserId
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            await _cacheService.RemoveAsync($"account:{request.UserId}");

            _logger.LogInformation("Счет создан успешно. AccountId: {AccountId}", account.Id);

            return new CreateAccountResponse
            {
                Success = true,
                Message = "Счет создан успешно",
                AccountId = account.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании счета для пользователя: {UserId}", request.UserId);
            return new CreateAccountResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    public override async Task<TopUpAccountResponse> TopUpAccount(TopUpAccountRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Пополнение счета пользователя: {UserId}, сумма: {Amount}", request.UserId, request.Amount);

            if (request.Amount <= 0)
            {
                return new TopUpAccountResponse
                {
                    Success = false,
                    Message = "Сумма должна быть больше нуля"
                };
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == request.UserId);

            if (account == null)
            {
                return new TopUpAccountResponse
                {
                    Success = false,
                    Message = "Счет не найден"
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                account.Balance += (decimal)request.Amount;
                account.UpdatedAt = DateTime.UtcNow;

                var transactionRecord = new Transaction
                {
                    AccountId = account.Id,
                    Type = TransactionType.TopUp,
                    Amount = (decimal)request.Amount,
                    BalanceAfter = account.Balance,
                    Description = "Пополнение счета"
                };

                _context.Transactions.Add(transactionRecord);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Обновляем кэш
                await _cacheService.SetAsync($"account:{request.UserId}", account, TimeSpan.FromMinutes(15));

                _logger.LogInformation("Счет пополнен успешно. UserId: {UserId}, новый баланс: {Balance}", 
                    request.UserId, account.Balance);

                return new TopUpAccountResponse
                {
                    Success = true,
                    Message = "Счет пополнен успешно",
                    NewBalance = (double)account.Balance
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при пополнении счета. UserId: {UserId}", request.UserId);
            return new TopUpAccountResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    public override async Task<GetBalanceResponse> GetBalance(GetBalanceRequest request, ServerCallContext context)
    {
        try
        {
            // Сначала проверяем кэш
            var cachedAccount = await _cacheService.GetAsync<Account>($"account:{request.UserId}");
            if (cachedAccount != null)
            {
                return new GetBalanceResponse
                {
                    Success = true,
                    Message = "Баланс получен успешно",
                    Balance = (double)cachedAccount.Balance,
                    AccountId = cachedAccount.Id.ToString()
                };
            }

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == request.UserId);

            if (account == null)
            {
                return new GetBalanceResponse
                {
                    Success = false,
                    Message = "Счет не найден"
                };
            }

            // Кэшируем результат
            await _cacheService.SetAsync($"account:{request.UserId}", account, TimeSpan.FromMinutes(15));

            return new GetBalanceResponse
            {
                Success = true,
                Message = "Баланс получен успешно",
                Balance = (double)account.Balance,
                AccountId = account.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении баланса. UserId: {UserId}", request.UserId);
            return new GetBalanceResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    public override async Task<ChargeAccountResponse> ChargeAccount(ChargeAccountRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("Списание со счета. UserId: {UserId}, Amount: {Amount}, OrderId: {OrderId}",
                request.UserId, request.Amount, request.OrderId);

            if (request.Amount <= 0)
            {
                return new ChargeAccountResponse
                {
                    Success = false,
                    Message = "Сумма должна быть больше нуля"
                };
            }

            // Проверяем идемпотентность
            var existingTransaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey);

            if (existingTransaction != null)
            {
                var account = await _context.Accounts.FindAsync(existingTransaction.AccountId);
                return new ChargeAccountResponse
                {
                    Success = true,
                    Message = "Транзакция уже была обработана",
                    RemainingBalance = (double)(account?.Balance ?? 0),
                    TransactionId = existingTransaction.Id.ToString()
                };
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            try
            {
                var userAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.UserId == request.UserId);

                if (userAccount == null)
                {
                    await PublishPaymentResult(request, false, null, "Счет не найден");
                    return new ChargeAccountResponse
                    {
                        Success = false,
                        Message = "Счет не найден"
                    };
                }

                if (userAccount.Balance < (decimal)request.Amount)
                {
                    await PublishPaymentResult(request, false, null, "Недостаточно средств на счете");
                    return new ChargeAccountResponse
                    {
                        Success = false,
                        Message = "Недостаточно средств на счете"
                    };
                }

                userAccount.Balance -= (decimal)request.Amount;
                userAccount.UpdatedAt = DateTime.UtcNow;

                var transactionRecord = new Transaction
                {
                    AccountId = userAccount.Id,
                    Type = TransactionType.Charge,
                    Amount = (decimal)request.Amount,
                    BalanceAfter = userAccount.Balance,
                    OrderId = request.OrderId,
                    IdempotencyKey = request.IdempotencyKey,
                    Description = $"Оплата заказа {request.OrderId}"
                };

                _context.Transactions.Add(transactionRecord);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Обновляем кэш
                await _cacheService.SetAsync($"account:{request.UserId}", userAccount, TimeSpan.FromMinutes(15));

                // Публикуем событие об успешной оплате
                await PublishPaymentResult(request, true, transactionRecord.Id.ToString(), null);

                _logger.LogInformation("Списание успешно. UserId: {UserId}, TransactionId: {TransactionId}",
                    request.UserId, transactionRecord.Id);

                return new ChargeAccountResponse
                {
                    Success = true,
                    Message = "Списание выполнено успешно",
                    RemainingBalance = (double)userAccount.Balance,
                    TransactionId = transactionRecord.Id.ToString()
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при списании со счета. UserId: {UserId}, OrderId: {OrderId}",
                request.UserId, request.OrderId);
                
            await PublishPaymentResult(request, false, null, "Внутренняя ошибка сервера");
            
            return new ChargeAccountResponse
            {
                Success = false,
                Message = "Внутренняя ошибка сервера"
            };
        }
    }

    private async Task PublishPaymentResult(ChargeAccountRequest request, bool success, string? transactionId, string? errorMessage)
    {
        try
        {
            var paymentEvent = new PaymentProcessedEvent
            {
                OrderId = request.OrderId,
                UserId = request.UserId,
                Amount = request.Amount,
                Success = success,
                TransactionId = transactionId,
                ErrorMessage = errorMessage,
                IdempotencyKey = request.IdempotencyKey
            };

            // Сохраняем в Outbox для гарантии доставки
            var outboxEvent = new OutboxEvent
            {
                EventType = nameof(PaymentProcessedEvent),
                EventData = JsonSerializer.Serialize(paymentEvent)
            };

            _context.OutboxEvents.Add(outboxEvent);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Событие PaymentProcessedEvent сохранено в Outbox. OrderId: {OrderId}", request.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при публикации события PaymentProcessedEvent. OrderId: {OrderId}", request.OrderId);
        }
    }
}
