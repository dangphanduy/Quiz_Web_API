using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Confluent.Kafka;
using Serilog.Configuration;
using Serilog;

namespace Quiz_Web.Extensions;

public class KafkaSink : ILogEventSink, IDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ITextFormatter _formatter;

    public KafkaSink(string bootstrapServers, string topic, ITextFormatter formatter)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            QueueBufferingMaxMessages = 100000,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100
        };
        _producer = new ProducerBuilder<Null, string>(config).Build();
        _topic = topic;
        _formatter = formatter;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            var message = writer.ToString();

            // Send asynchronously to avoid blocking the main thread
            _producer.Produce(_topic, new Message<Null, string> { Value = message }, handler =>
            {
                if (handler.Error.IsError)
                {
                    System.Diagnostics.Trace.WriteLine($"Kafka publish error: {handler.Error.Reason}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"Kafka Sink Emit Exception: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(2));
        _producer?.Dispose();
    }
}

public static class KafkaSinkExtensions
{
    public static LoggerConfiguration Kafka(
        this LoggerSinkConfiguration loggerSinkConfiguration,
        string bootstrapServers,
        string topic,
        ITextFormatter formatter)
    {
        return loggerSinkConfiguration.Sink(new KafkaSink(bootstrapServers, topic, formatter));
    }
}
