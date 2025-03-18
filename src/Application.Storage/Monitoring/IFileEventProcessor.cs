// File: BridgingIT.DevKit.Application.FileMonitoring/IFileEventProcessor.cs
namespace BridgingIT.DevKit.Application.FileMonitoring;

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

/// <summary>
/// Defines the contract for processing FileEvent instances in the FileMonitoring system.
/// Processors are chained sequentially per location to handle detected file events.
/// </summary>
public interface IFileEventProcessor
{
    /// <summary>
    /// Gets the unique name of the processor.
    /// Used for identification and runtime control (e.g., enabling/disabling).
    /// </summary>
    string ProcessorName { get; }

    /// <summary>
    /// Gets or sets whether the processor is enabled.
    /// Allows runtime activation/deactivation without removing it from the chain.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets the collection of processor-specific behaviors.
    /// Behaviors enhance execution (e.g., logging, retry) and are executed before/after processing.
    /// </summary>
    IEnumerable<IProcessorBehavior> Behaviors { get; }

    /// <summary>
    /// Processes a FileEvent asynchronously.
    /// Executes the processor's logic for the given event, potentially modified by behaviors.
    /// </summary>
    /// <param name="context">The processing context containing the FileEvent and additional data.</param>
    /// <param name="token">The cancellation token to stop the operation if needed.</param>
    /// <returns>A task representing the asynchronous processing operation.</returns>
    Task ProcessAsync(ProcessingContext context, CancellationToken token);
}

/// <summary>
/// Represents the context for processing a FileEvent, passed through the processor chain.
/// </summary>
public class ProcessingContext
{
    /// <summary>
    /// Gets or sets the FileEvent being processed.
    /// Contains details like EventType, FilePath, and Checksum.
    /// </summary>
    public FileEvent FileEvent { get; set; }

    // Additional properties can be added later (e.g., retry count, custom data)
}