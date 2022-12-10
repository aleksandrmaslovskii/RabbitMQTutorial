using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Send6;

public class RpcClient
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _replyQueueName;
    private readonly EventingBasicConsumer _consumer;
    private readonly BlockingCollection<string> _respQueue = new();
    private readonly IBasicProperties _props;

    public RpcClient()
    {
        var factory = new ConnectionFactory() {Endpoint = new AmqpTcpEndpoint("localhost", 49154)};

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _replyQueueName = _channel.QueueDeclare().QueueName;
        _consumer = new EventingBasicConsumer(_channel);

        _props = _channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        _props.CorrelationId = correlationId;
        _props.ReplyTo = _replyQueueName;

        _consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var response = Encoding.UTF8.GetString(body);
            if (ea.BasicProperties.CorrelationId == correlationId)
            {
                _respQueue.Add(response);
            }
        };

        _channel.BasicConsume(
            consumer: _consumer,
            queue: _replyQueueName,
            autoAck: true);
    }

    public string Call(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        _channel.BasicPublish(
            exchange: "",
            routingKey: "rpc_queue",
            basicProperties: _props,
            body: messageBytes);

        return _respQueue.Take();
    }

    public void Close()
    {
        _connection.Close();
    }
}