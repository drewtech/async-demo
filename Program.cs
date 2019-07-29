using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace async_demo
{
    class Program
    {
        private static Random _random = new Random(100);

        static void Main(string[] args)
        {
            var accountList = new List<string> {
                "ACC1",
                "ACC2",
                "ACC3",
                "ACC4",
                "ACC5",
                "ACC6",
                "ACC7",
                "ACC8",
                "ACC9",
                "ACC10",
                "ACC11",
                "ACC12",
                "ACC13",
                "ACC14",
                "ACC15",
                "ACC16",
                "ACC17",
                "ACC18",
                "ACC19",
                "ACC20"
            };

            List<Task<XElement>> tasks = new List<Task<XElement>>();

            var sw = Stopwatch.StartNew();
            foreach (var accountCode in accountList)
            {
                tasks.Add(GetDataAsync(accountCode));
            }
            foreach (var task in tasks)
            {
                try
                {
                    Console.WriteLine(task.Result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OMG Error! {ex.Message}");
                }

            }

            Console.WriteLine($"Finished async in {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            foreach (var accountCode in accountList)
            {
                try
                {
                    Console.WriteLine(GetData(accountCode));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OMG Error! {ex.Message}");
                }
            }
            Console.WriteLine($"Finished synchronous in {sw.ElapsedMilliseconds} ms");
        }

        private static Task<XElement> GetDataAsync(string accountCode)
        {
            return Task.Run(() => 
            {
                var waitTime = _random.Next(100, 1000);
                Console.WriteLine($"Account {accountCode} waiting for {waitTime}");
                Thread.Sleep(waitTime);
                Console.WriteLine($"Account {accountCode} completed in {waitTime}");
                if (accountCode == "ACC16")
                {
                    try
                    {                    
                        return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account1>");
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Error parsing xml for account {accountCode}.  The Error is {e.Message}",e);
                    }
                }
                return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account>");
            });

        }

        private static XElement GetData(string accountCode)
        {
            var waitTime = _random.Next(100, 1000);
            Console.WriteLine($"Account {accountCode} waiting for {waitTime}");
            Thread.Sleep(waitTime);
            Console.WriteLine($"Account {accountCode} completed in {waitTime}");
            if (accountCode == "ACC16")
            {
                try
                {                    
                    return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account1>");
                }
                catch (Exception e)
                {
                    throw new Exception($"Error parsing xml for account {accountCode}.  The Error is {e.Message}",e);
                }
            }
            return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account>");

        }
    }
}
