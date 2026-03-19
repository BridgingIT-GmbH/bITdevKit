// NSubstitute Mock/Stub/Spy 模式範例
// 展示 Test Double 五大類型與常見測試模式

using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace NSubstituteMockingExamples;

// ==================== 測試資料模型 ====================

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public OrderStatus Status { get; set; }
}

public enum CustomerType
{
    Regular,
    Premium,
    VIP
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled
}

public enum PaymentResult
{
    Success,
    Failed,
    Pending
}

// ==================== 相依介面定義 ====================

public interface IUserRepository
{
    User? GetById(int id);
    Task<User?> GetByIdAsync(int id);
    void Save(User user);
    Task SaveAsync(User user);
    void Delete(int id);
    IEnumerable<User> GetAll();
}

public interface IEmailService
{
    void SendEmail(string to, string subject, string body, ILogger logger);
    void SendWelcomeEmail(string email, string name);
    void SendConfirmation(string email);
    bool SendNotification(string email, string message);
}

public interface ICustomerService
{
    CustomerType GetCustomerType(int customerId);
}

public interface IPaymentGateway
{
    PaymentResult ProcessPayment(decimal amount);
}

public interface IOrderRepository
{
    Order? GetById(int id);
    void Save(Order order);
}

// ==================== 業務邏輯類別 ====================

public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService? _emailService;
    private readonly ILogger<UserService>? _logger;
    
    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }
    
    public UserService(IUserRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public UserService(IUserRepository repository, ILogger<UserService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
    
    public User? GetUser(int id)
    {
        return _repository.GetById(id);
    }
    
    public async Task<User?> GetUserAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }
    
    public void CreateUser(User user)
    {
        _repository.Save(user);
        _logger?.LogInformation("User created: {Name}", user.Name);
    }
    
    public void RegisterUser(string email, string name)
    {
        _emailService?.SendWelcomeEmail(email, name);
    }
    
    public async Task SaveUserAsync(User user)
    {
        await _repository.SaveAsync(user);
    }
}

public class OrderService
{
    private readonly IOrderRepository? _repository;
    private readonly IEmailService? _emailService;
    private readonly ILogger<OrderService>? _logger;
    
    public OrderService() { }
    
    public OrderService(IOrderRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public OrderService(IOrderRepository repository, IEmailService emailService, ILogger<OrderService> logger)
    {
        _repository = repository;
        _emailService = emailService;
        _logger = logger;
    }
    
    public OrderResult ProcessOrder(Order order, ILogger dummyLogger)
    {
        // 處理訂單邏輯
        return new OrderResult { Success = true };
    }
    
    public OrderResult ProcessOrder(int orderId)
    {
        var order = _repository?.GetById(orderId);
        if (order == null)
            return new OrderResult { Success = false };
            
        order.Status = OrderStatus.Completed;
        return new OrderResult { Success = true };
    }
}

public class OrderResult
{
    public bool Success { get; set; }
}

public class PricingService
{
    private readonly ICustomerService _customerService;
    
    public PricingService(ICustomerService customerService)
    {
        _customerService = customerService;
    }
    
    public decimal CalculateDiscount(int customerId, decimal amount)
    {
        var customerType = _customerService.GetCustomerType(customerId);
        
        return customerType switch
        {
            CustomerType.Premium => amount * 0.2m,
            CustomerType.VIP => amount * 0.3m,
            _ => 0
        };
    }
}

public class PaymentService
{
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<PaymentService> _logger;
    
    public PaymentService(IPaymentGateway paymentGateway, ILogger<PaymentService> logger)
    {
        _paymentGateway = paymentGateway;
        _logger = logger;
    }
    
    public void ProcessPayment(decimal amount)
    {
        var result = _paymentGateway.ProcessPayment(amount);
        _logger.LogInformation("Payment processed: {Amount} - Result: {Result}", amount, result);
    }
}

// ==================== 測試類別 ====================

public class TestDoublePatternTests
{
    // ==================== 模式 1: Dummy - 填充物件 ====================
    
    [Fact]
    public void Pattern1_Dummy_只為滿足參數要求不會被使用()
    {
        // Arrange - Dummy：只是為了滿足參數要求
        var dummyLogger = Substitute.For<ILogger>();
        var order = new Order { Id = 1, ProductName = "Product A" };
        var service = new OrderService();
        
        // Act
        var result = service.ProcessOrder(order, dummyLogger);
        
        // Assert
        Assert.True(result.Success);
        // 不關心 dummyLogger 是否被調用
    }
    
    // ==================== Test Double 類型範例 ====================
    
    [Fact]
    public void Pattern1_Dummy_僅用於滿足方法簽章()
    {
        // Arrange
        var dummyLogger = Substitute.For<ILogger>();
        var order = new Order { Id = 1, ProductName = "Product A" };
        var service = new OrderService();
        
        // Act
        var result = service.ProcessOrder(order, dummyLogger);
        
        // Assert
        Assert.True(result.Success);
        // 不關心 dummyLogger 是否被調用
    }
}

// ==================== 測試類別 ====================

public class NSubstituteMockPatternsTests
{
    // ==================== 模式 1: Dummy - 填充物件 ====================
    
    [Fact]
    public void Dummy_ProcessOrder_不使用Logger_應成功處理訂單()
    {
        // Arrange - Dummy：只是為了滿足參數要求
        var dummyLogger = Substitute.For<ILogger>();
        var service = new OrderService();
        var order = new Order { Id = 1, ProductName = "Test" };
        
        // Act
        var result = service.ProcessOrder(order, dummyLogger);
        
        // Assert
        Assert.True(result.Success);
        // 不關心 logger 是否被調用
    }
    
    // ==================== Stub 測試模式 ====================
    
    [Fact]
    public void Stub_GetUser_有效的使用者ID_應回傳使用者資料()
    {
        // Arrange - Stub：預設回傳值
        var stubRepository = Substitute.For<IUserRepository>();
        stubRepository.GetById(123).Returns(new User 
        { 
            Id = 123, 
            Name = "John Doe",
            Email = "john@example.com"
        });
        
        var service = new UserService(stubRepository);
        
        // Act
        var actual = service.GetUser(123);
        
        // Assert
        Assert.NotNull(actual);
        Assert.Equal("John Doe", actual.Name);
        Assert.Equal("john@example.com", actual.Email);
        // 不關心 GetById 被呼叫了幾次
    }
    
    [Fact]
    public void Stub_GetUser_任意ID_應回傳預設使用者()
    {
        // Arrange - 使用 Arg.Any 匹配任意參數
        var stubRepository = Substitute.For<IUserRepository>();
        stubRepository.GetById(Arg.Any<int>()).Returns(new User 
        { 
            Id = 999, 
            Name = "Default User" 
        });
        
        var service = new UserService(stubRepository);
        
        // Act
        var result1 = service.GetUser(1);
        var result2 = service.GetUser(100);
        var result3 = service.GetUser(999);
        
        // Assert
        Assert.Equal("Default User", result1?.Name);
        Assert.Equal("Default User", result2?.Name);
        Assert.Equal("Default User", result3?.Name);
    }
    
    [Fact]
    public void Stub_CalculateDiscount_高級會員_應回傳20折扣()
    {
        // Arrange - Stub：只關心回傳值
        var stubCustomerService = Substitute.For<ICustomerService>();
        stubCustomerService.GetCustomerType(123).Returns(CustomerType.Premium);
        
        var service = new PricingService(stubCustomerService);
        
        // Act
        var discount = service.CalculateDiscount(123, 1000);
        
        // Assert - 只驗證結果狀態
        Assert.Equal(200, discount); // 20% of 1000
    }
    
    [Fact]
    public void Stub_CalculateDiscount_VIP會員_應回傳30折扣()
    {
        // Arrange
        var stubCustomerService = Substitute.For<ICustomerService>();
        stubCustomerService.GetCustomerType(456).Returns(CustomerType.VIP);
        
        var service = new PricingService(stubCustomerService);
        
        // Act
        var discount = service.CalculateDiscount(456, 1000);
        
        // Assert
        Assert.Equal(300, discount); // 30% of 1000
    }
    
    // ==================== Fake 測試模式 ====================
    
    [Fact]
    public void Fake_CreateAndGetUser_應正確儲存並查詢()
    {
        // Arrange - Fake：有真實邏輯的簡化實作
        var fakeRepository = new FakeUserRepository();
        var service = new UserService(fakeRepository);
        
        var newUser = new User { Id = 1, Name = "John Doe", Email = "john@example.com" };
        
        // Act
        fakeRepository.Save(newUser);
        var actual = service.GetUser(1);
        
        // Assert
        Assert.NotNull(actual);
        Assert.Equal("John Doe", actual.Name);
        Assert.Equal("john@example.com", actual.Email);
    }
    
    [Fact]
    public void Fake_DeleteUser_應移除使用者()
    {
        // Arrange
        var fakeRepository = new FakeUserRepository();
        var service = new UserService(fakeRepository);
        
        fakeRepository.Save(new User { Id = 1, Name = "John" });
        
        // Act
        fakeRepository.Delete(1);
        var actual = service.GetUser(1);
        
        // Assert
        Assert.Null(actual);
    }
    
    // ==================== Spy 測試模式 ====================
    
    [Fact]
    public void Spy_CreateUser_應記錄使用者建立資訊()
    {
        // Arrange
        var spyLogger = Substitute.For<ILogger<UserService>>();
        var repository = Substitute.For<IUserRepository>();
        var service = new UserService(repository, spyLogger);
        
        var newUser = new User { Id = 1, Name = "John Doe" };
        
        // Act
        service.CreateUser(newUser);
        
        // Assert - Spy：驗證呼叫記錄
        spyLogger.Received(1).LogInformation("User created: {Name}", "John Doe");
    }
    
    // ==================== Mock 測試模式 ====================
    
    [Fact]
    public void Mock_RegisterUser_應發送歡迎郵件()
    {
        // Arrange
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IUserRepository>();
        var service = new UserService(repository, mockEmailService);
        
        // Act
        service.RegisterUser("john@example.com", "John Doe");
        
        // Assert - Mock：驗證特定的互動行為
        mockEmailService.Received(1).SendWelcomeEmail("john@example.com", "John Doe");
    }
    
    [Fact]
    public void Mock_RegisterUser_應只發送一次郵件()
    {
        // Arrange
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IUserRepository>();
        var service = new UserService(repository, mockEmailService);
        
        // Act
        service.RegisterUser("john@example.com", "John");
        
        // Assert - 驗證呼叫次數
        mockEmailService.Received(1).SendWelcomeEmail(Arg.Any<string>(), Arg.Any<string>());
    }
    
    // ==================== 非同步測試模式 ====================
    
    [Fact]
    public async Task Async_GetUserAsync_使用者存在_應回傳使用者資料()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        repository.GetByIdAsync(123).Returns(Task.FromResult<User?>(
            new User { Id = 123, Name = "John Doe" }));
        
        var service = new UserService(repository);
        
        // Act
        var result = await service.GetUserAsync(123);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Doe", result.Name);
        await repository.Received(1).GetByIdAsync(123);
    }
    
    [Fact]
    public async Task Async_SaveUserAsync_資料庫錯誤_應拋出例外()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        repository.SaveAsync(Arg.Any<User>())
                  .Throws(new InvalidOperationException("Database connection failed"));
        
        var service = new UserService(repository);
        
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.SaveUserAsync(new User { Name = "John" }));
    }
    
    // ==================== 回傳序列值測試 ====================
    
    [Fact]
    public void Returns_Sequence_GetAll_應依序回傳不同使用者集合()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        
        // 設定序列回傳值
        repository.GetAll().Returns(
            new[] { new User { Id = 1, Name = "User1" } },
            new[] { new User { Id = 1, Name = "User1" }, new User { Id = 2, Name = "User2" } },
            new[] { new User { Id = 1, Name = "User1" }, new User { Id = 2, Name = "User2" }, new User { Id = 3, Name = "User3" } }
        );
        
        // Act
        var result1 = repository.GetAll();
        var result2 = repository.GetAll();
        var result3 = repository.GetAll();
        
        // Assert
        Assert.Single(result1);
        Assert.Equal(2, result2.Count());
        Assert.Equal(3, result3.Count());
    }
    
    // ==================== 例外處理測試 ====================
    
    [Fact]
    public void Throws_GetUser_資料庫連線失敗_應拋出例外()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        repository.GetById(Arg.Any<int>())
                  .Throws(new InvalidOperationException("Database connection failed"));
        
        var service = new UserService(repository);
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.GetUser(123));
    }
    
    // ==================== 條件回傳值測試 ====================
    
    [Fact]
    public void Returns_Conditional_GetById_根據ID回傳不同使用者()
    {
        // Arrange
        var repository = Substitute.For<IUserRepository>();
        
        // 條件回傳：偶數 ID 回傳 Premium 使用者，奇數回傳 Regular 使用者
        repository.GetById(Arg.Any<int>()).Returns(x =>
        {
            var id = (int)x[0];
            return new User
            {
                Id = id,
                Name = $"User{id}",
                CustomerType = id % 2 == 0 ? CustomerType.Premium : CustomerType.Regular
            };
        });
        
        var service = new UserService(repository);
        
        // Act
        var user1 = service.GetUser(1);
        var user2 = service.GetUser(2);
        var user3 = service.GetUser(3);
        
        // Assert
        Assert.Equal(CustomerType.Regular, user1?.CustomerType);
        Assert.Equal(CustomerType.Premium, user2?.CustomerType);
        Assert.Equal(CustomerType.Regular, user3?.CustomerType);
    }
    
    // ==================== 未呼叫驗證測試 ====================
    
    [Fact]
    public void DidNotReceive_ProcessOrder_訂單處理失敗_不應發送郵件()
    {
        // Arrange
        var mockEmailService = Substitute.For<IEmailService>();
        var repository = Substitute.For<IOrderRepository>();
        repository.GetById(Arg.Any<int>()).Returns((Order?)null); // 訂單不存在
        
        var service = new OrderService(repository, mockEmailService);
        
        // Act
        var result = service.ProcessOrder(999);
        
        // Assert
        Assert.False(result.Success);
        mockEmailService.DidNotReceive().SendConfirmation(Arg.Any<string>());
    }
}

// ==================== Fake 實作範例 ====================

public class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<int, User> _users = new();
    
    public User? GetById(int id)
    {
        _users.TryGetValue(id, out var user);
        return user;
    }
    
    public Task<User?> GetByIdAsync(int id)
    {
        return Task.FromResult(GetById(id));
    }
    
    public void Save(User user)
    {
        _users[user.Id] = user;
    }
    
    public Task SaveAsync(User user)
    {
        Save(user);
        return Task.CompletedTask;
    }
    
    public void Delete(int id)
    {
        _users.Remove(id);
    }
    
    public IEnumerable<User> GetAll()
    {
        return _users.Values;
    }
}
