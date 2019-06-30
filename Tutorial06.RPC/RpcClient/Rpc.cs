using System;
using System.Linq;
using System.Threading.Tasks;

public class Rpc
{
    public static void Main(string[] args)
    {
        var rpcClient = new RpcClient();
        
        var tasks = args.Select(async arg => {
            Console.WriteLine(" [x] Requesting fib({0})", arg);
            
            try
            {
                var response = await rpcClient.CallAsync(arg);

                if (!int.TryParse(response, out _))
                    throw new Exception("The result of the RPC call is invalid.");

                Console.WriteLine(" [.] Got fib({0}) = '{1}'", arg, response);
            }
            catch (Exception ex)
            {
                Console.WriteLine(" [!] Fail: fib({0}): {1}", arg, ex.Message);
            }
        }).ToArray();
        
        Task.WhenAll(tasks)
            .ContinueWith(x => {
                Console.WriteLine(" [x] All tasks is finished.");
            });
        
        Console.WriteLine(" Press [enter] to exit.");
        Console.ReadLine();

        rpcClient.Close();
    }
}
