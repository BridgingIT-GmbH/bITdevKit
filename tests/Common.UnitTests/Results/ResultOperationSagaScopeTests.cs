// MIT-License
// Copyright BridgingIT GmbH - All Rights Reserved
// Use of this source code is governed by an MIT-style license that can be
// found in the LICENSE file at https://github.com/bridgingit/bitdevkit/license

namespace BridgingIT.DevKit.Common.UnitTests.Results;

using Microsoft.Extensions.Logging;

[UnitTest("Common")]
public class ResultOperationSagaScopeTests
{
    private readonly Faker faker = new();

    [Fact]
    public async Task SagaScope_WithSuccessfulSteps_CommitsAll()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var saga = new SagaScope(logger);
        saga.Context.CorrelationId = "test-correlation-id";
        saga.Context.SetProperty("UserId", "user-123");
        saga.Context.Properties.Set("RequestId", "556677");

        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var hotelService = new TestHotelService();

        // Track compensation events
        var compensationEvents = new List<SagaCompensationEvent>();
        saga.OnCompensationEvent += (evt) =>
        {
            compensationEvents.Add(evt);
            return Task.CompletedTask;
        };

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation("FlightCancellation",
                        async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync(async (b, ct) =>
                {
                    var hotel = await hotelService.BookAsync(b.HotelDetails, ct);
                    saga.RegisterCompensation("HotelCancellation",
                        async ct => await hotelService.CancelAsync(hotel.Id, ct));
                    b.HotelConfirmation = hotel.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        result.Value.FlightConfirmation.ShouldNotBeNullOrEmpty();
        result.Value.HotelConfirmation.ShouldNotBeNullOrEmpty();
        saga.IsCommitted.ShouldBeTrue();
        saga.IsRolledBack.ShouldBeFalse();
        saga.CompensationCount.ShouldBe(2); // Two compensations registered
        saga.Context.CorrelationId.ShouldBe("test-correlation-id");
        saga.Context.GetProperty<string>("UserId").ShouldBe("user-123");
        saga.Context.GetProperty<string>("RequestId").ShouldBe("556677");

        // Verify compensation events
        compensationEvents.Count.ShouldBe(2); // 2 registrations
        compensationEvents.ShouldAllBe(e => e.EventType == CompensationEventType.Registered);
        compensationEvents[0].StepName.ShouldBe("FlightCancellation");
        compensationEvents[1].StepName.ShouldBe("HotelCancellation");

        flightService.BookedFlights.Count.ShouldBe(1);
        flightService.CancelledFlights.Count.ShouldBe(0); // Not cancelled
        hotelService.BookedHotels.Count.ShouldBe(1);
        hotelService.CancelledHotels.Count.ShouldBe(0); // Not cancelled
    }

    [Fact]
    public async Task SagaScope_WithFailedStep_RollsBackPreviousSteps()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var hotelService = new TestHotelService { ShouldFailBooking = true }; // Hotel booking will fail

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync(async (b, ct) =>
                {
                    // This will fail
                    var hotelResult = await hotelService.BookResultAsync(b.HotelDetails, ct);
                    if (hotelResult.IsFailure)
                    {
                        return Result<TripBooking>.Failure()
                            .WithErrors(hotelResult.Errors)
                            .WithMessage("Hotel booking failed");
                    }

                    saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotelResult.Value.Id, ct));
                    b.HotelConfirmation = hotelResult.Value.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<BookingError>();
        saga.IsCommitted.ShouldBeFalse();
        saga.IsRolledBack.ShouldBeTrue();
        saga.CompensationExecutedCount.ShouldBe(1); // Only flight compensation executed
        flightService.BookedFlights.Count.ShouldBe(1);
        flightService.CancelledFlights.Count.ShouldBe(1); // Flight was cancelled (compensated)
        hotelService.BookedHotels.Count.ShouldBe(0); // Never booked
    }

    [Fact]
    public async Task SagaScope_WithException_RollsBackAllSteps()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var hotelService = new TestHotelService();

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .TapAsync(async (b, ct) =>
                {
                    await Task.Delay(1, ct);
                    throw new InvalidOperationException("Unexpected error during booking");
                })
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        result.Errors.ShouldContain(e => e.Message.Contains("Unexpected error"));
        saga.IsCommitted.ShouldBeFalse();
        saga.IsRolledBack.ShouldBeTrue();
        saga.CompensationExecutedCount.ShouldBe(1); // Flight compensation executed
        flightService.CancelledFlights.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SagaScope_WithMultipleSteps_RegistersCompensationsInOrder()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var hotelService = new TestHotelService();
        var carService = new TestCarRentalService();

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync(async (b, ct) =>
                {
                    var hotel = await hotelService.BookAsync(b.HotelDetails, ct);
                    saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotel.Id, ct));
                    b.HotelConfirmation = hotel.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync(async (b, ct) =>
                {
                    var car = await carService.RentAsync(b.CarDetails, ct);
                    saga.RegisterCompensation(async ct => await carService.CancelAsync(car.Id, ct));
                    b.CarConfirmation = car.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        saga.CompensationCount.ShouldBe(3);
        flightService.BookedFlights.Count.ShouldBe(1);
        hotelService.BookedHotels.Count.ShouldBe(1);
        carService.RentedCars.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SagaScope_WithFailedCompensation_ContinuesWithOthers()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService { ShouldFailCancellation = true }; // Compensation will fail
        var hotelService = new TestHotelService();

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation(async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync(async (b, ct) =>
                {
                    var hotel = await hotelService.BookAsync(b.HotelDetails, ct);
                    saga.RegisterCompensation(async ct => await hotelService.CancelAsync(hotel.Id, ct));
                    b.HotelConfirmation = hotel.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .EnsureAsync(async (b, ct) =>
                {
                    await Task.Delay(1, ct);

                    return false; // Force rollback
                }, new ValidationError("Booking validation failed"))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        saga.IsRolledBack.ShouldBeTrue();
        saga.CompensationExecutedCount.ShouldBe(2); // Both compensations attempted
        saga.CompensationErrors.Count.ShouldBe(1); // One failed
        hotelService.CancelledHotels.Count.ShouldBe(1); // Hotel successfully cancelled
    }

    [Fact]
    public async Task SagaScope_EmptyBooking_HandlesGracefully()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .TapAsync(async (b, ct) => await Task.Delay(1, ct))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeSuccess();
        saga.IsCommitted.ShouldBeTrue();
        saga.CompensationCount.ShouldBe(0); // No compensations registered
    }

    [Fact]
    public async Task SagaScope_WithCancellation_RollsBackGracefully()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var cts = new CancellationTokenSource();

        // Act - Cancel before operation starts (so it completes successfully with cancellation token not yet triggered)
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation("FlightCancellation",
                        async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    // Trigger cancellation AFTER successful booking
                    cts.Cancel();

                    return Result<TripBooking>.Success(b);
                }, cts.Token)
                .BindAsync(async (b, ct) =>
                {
                    // This should be cancelled
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(100, ct);

                    return Result<TripBooking>.Success(b);
                }, cts.Token)
            .EndOperationAsync(cts.Token);

        // Assert
        result.ShouldBeFailure();
        result.ShouldContainError<OperationCancelledError>();
        saga.IsRolledBack.ShouldBeTrue();
        saga.CompensationExecutedCount.ShouldBe(1); // Flight compensation executed
    }

    [Fact]
    public async Task SagaScope_WithConditionalCompensation_OnlyExecutesWhenConditionMet()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var saga = new SagaScope(logger);
        var booking = new TripBooking();
        var flightService = new TestFlightService();
        var paymentCaptured = false; // Condition: payment not captured

        var compensationEvents = new List<SagaCompensationEvent>();
        saga.OnCompensationEvent += (evt) =>
        {
            compensationEvents.Add(evt);
            return Task.CompletedTask;
        };

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation("FlightCancellation",
                        async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .BindAsync((b, ct) =>
                {
                    // Register conditional compensation for payment reversal
                    // Only execute if payment was actually captured
                    saga.RegisterCompensation("PaymentReversal",
                        async ct => await Task.CompletedTask, // Mock payment reversal
                        async ct => await Task.FromResult(paymentCaptured)); // Condition: only if captured

                    return Task.FromResult(Result<TripBooking>.Success(b));
                }, CancellationToken.None)
                .EnsureAsync(async (b, ct) =>
                {
                    await Task.Delay(1, ct);
                    return false; // Force rollback
                }, new ValidationError("Forced failure"))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        saga.IsRolledBack.ShouldBeTrue();
        saga.CompensationExecutedCount.ShouldBe(1); // Only flight, payment reversal skipped
        flightService.CancelledFlights.Count.ShouldBe(1);

        // Verify compensation events include skipped event
        compensationEvents.ShouldContain(e =>
            e.EventType == CompensationEventType.Skipped &&
            e.StepName == "PaymentReversal");
        compensationEvents.ShouldContain(e =>
            e.EventType == CompensationEventType.Succeeded &&
            e.StepName == "FlightCancellation");
    }

    [Fact]
    public async Task SagaScope_WithEvents_NotifiesAllLifecycleStages()
    {
        // Arrange
        var saga = new SagaScope();
        var booking = new TripBooking();
        var flightService = new TestFlightService();

        var events = new List<SagaCompensationEvent>();
        saga.OnCompensationEvent += (evt) =>
        {
            events.Add(evt);
            return Task.CompletedTask;
        };

        // Act
        var result = await Result<TripBooking>.Success(booking)
            .StartOperation(saga)
                .BindAsync(async (b, ct) =>
                {
                    var flight = await flightService.BookAsync(b.FlightDetails, ct);
                    saga.RegisterCompensation("FlightCancellation",
                        async ct => await flightService.CancelAsync(flight.Id, ct));
                    b.FlightConfirmation = flight.ConfirmationNumber;

                    return Result<TripBooking>.Success(b);
                }, CancellationToken.None)
                .EnsureAsync(async (b, ct) =>
                {
                    await Task.Delay(1, ct);
                    return false; // Force rollback
                }, new ValidationError("Forced failure"))
            .EndOperationAsync(CancellationToken.None);

        // Assert
        result.ShouldBeFailure();
        saga.IsRolledBack.ShouldBeTrue();

        // Verify all event types
        events.Count.ShouldBe(3);
        events[0].EventType.ShouldBe(CompensationEventType.Registered);
        events[0].StepName.ShouldBe("FlightCancellation");

        events[1].EventType.ShouldBe(CompensationEventType.Started);
        events[1].StepName.ShouldBe("FlightCancellation");

        events[2].EventType.ShouldBe(CompensationEventType.Succeeded);
        events[2].StepName.ShouldBe("FlightCancellation");
        events[2].Duration.ShouldNotBeNull();
        events[2].Duration.Value.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(0);
    }
}

// Test Domain Models
public class TripBooking
{
    public string FlightConfirmation { get; set; }
    public string HotelConfirmation { get; set; }
    public string CarConfirmation { get; set; }
    public FlightDetails FlightDetails { get; set; } = new();
    public HotelDetails HotelDetails { get; set; } = new();
    public CarDetails CarDetails { get; set; } = new();
}

public class FlightDetails
{
    public string Destination { get; set; } = "New York";
    public DateTime DepartureDate { get; set; } = DateTime.UtcNow.AddDays(7);
}

public class HotelDetails
{
    public string Location { get; set; } = "Manhattan";
    public DateTime CheckIn { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime CheckOut { get; set; } = DateTime.UtcNow.AddDays(10);
}

public class CarDetails
{
    public string PickupLocation { get; set; } = "Airport";
    public DateTime PickupDate { get; set; } = DateTime.UtcNow.AddDays(7);
}

public class FlightBooking
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConfirmationNumber { get; set; } = $"FL{Guid.NewGuid():N}"[..10].ToUpper();
}

public class HotelBooking
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConfirmationNumber { get; set; } = $"HT{Guid.NewGuid():N}"[..10].ToUpper();
}

public class CarRental
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConfirmationNumber { get; set; } = $"CR{Guid.NewGuid():N}"[..10].ToUpper();
}

public class BookingError(string message) : ResultErrorBase(message)
{
}

// Test Service Implementations
public class TestFlightService
{
    public List<FlightBooking> BookedFlights { get; } = [];
    public List<string> CancelledFlights { get; } = [];
    public bool ShouldFailCancellation { get; set; }

    public Task<FlightBooking> BookAsync(FlightDetails details, CancellationToken cancellationToken)
    {
        var booking = new FlightBooking();
        this.BookedFlights.Add(booking);

        return Task.FromResult(booking);
    }

    public Task CancelAsync(string flightId, CancellationToken cancellationToken)
    {
        if (this.ShouldFailCancellation)
        {
            throw new InvalidOperationException("Flight cancellation failed");
        }

        this.CancelledFlights.Add(flightId);

        return Task.CompletedTask;
    }
}

public class TestHotelService
{
    public List<HotelBooking> BookedHotels { get; } = [];
    public List<string> CancelledHotels { get; } = [];
    public bool ShouldFailBooking { get; set; }

    public Task<HotelBooking> BookAsync(HotelDetails details, CancellationToken cancellationToken)
    {
        if (this.ShouldFailBooking)
        {
            throw new InvalidOperationException("Hotel booking failed");
        }

        var booking = new HotelBooking();
        this.BookedHotels.Add(booking);

        return Task.FromResult(booking);
    }

    public Task<Result<HotelBooking>> BookResultAsync(HotelDetails details, CancellationToken cancellationToken)
    {
        if (this.ShouldFailBooking)
        {
            return Task.FromResult(Result<HotelBooking>.Failure()
                .WithError(new BookingError("Hotel booking service unavailable")));
        }

        var booking = new HotelBooking();
        this.BookedHotels.Add(booking);

        return Task.FromResult(Result<HotelBooking>.Success(booking));
    }

    public Task CancelAsync(string hotelId, CancellationToken cancellationToken)
    {
        this.CancelledHotels.Add(hotelId);

        return Task.CompletedTask;
    }
}

public class TestCarRentalService
{
    public List<CarRental> RentedCars { get; } = [];
    public List<string> CancelledCars { get; } = [];

    public Task<CarRental> RentAsync(CarDetails details, CancellationToken cancellationToken)
    {
        var rental = new CarRental();
        this.RentedCars.Add(rental);
        return Task.FromResult(rental);
    }

    public Task CancelAsync(string carId, CancellationToken cancellationToken)
    {
        this.CancelledCars.Add(carId);

        return Task.CompletedTask;
    }
}
