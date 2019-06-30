using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class RpcClient
{
    private readonly IConnection connection;
    private readonly IModel channel;
    private readonly string replyQueueName;
    private readonly EventingBasicConsumer consumer;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> tasksResultHandlers
        = new ConcurrentDictionary<string, TaskCompletionSource<string>>();

    public RpcClient()
    {
        var factory = new ConnectionFactory() { HostName = "localhost" };

        connection = factory.CreateConnection();
        channel = connection.CreateModel();
        replyQueueName = channel.QueueDeclare().QueueName;
        consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body;
            var response = Encoding.UTF8.GetString(body);
            
            if (tasksResultHandlers.TryRemove(ea.BasicProperties.CorrelationId, out var taskResult))
                taskResult.SetResult(response);
        };

        connection.ConnectionShutdown += (_, e) => {
            foreach (var key in tasksResultHandlers.Keys.ToArray())
                if (tasksResultHandlers.TryRemove(key, out var taskResult))
                    taskResult.SetCanceled();
        };
        
        channel.BasicConsume(
            consumer: consumer,
            queue: replyQueueName,
            autoAck: true
        );
    }

    public Task<string> CallAsync(string message)
    {
        var correlationId = Guid.NewGuid().ToString();

        var taskResult = new TaskCompletionSource<string>();
        tasksResultHandlers[correlationId] = taskResult;

        var props = channel.CreateBasicProperties();
        props.CorrelationId = correlationId;
        props.ReplyTo = replyQueueName;
        
        var messageBytes = Encoding.UTF8.GetBytes(message);
        channel.BasicPublish(
            exchange: "",
            routingKey: "rpc_queue",
            basicProperties: props,
            body: messageBytes
        );

        return taskResult.Task;
    }
    
    public void Close()
    {
        connection.Close();
    }
}
