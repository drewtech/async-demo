using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Xml.Linq;

namespace async_demo
{
    class Program
    {
        private static Random _random = new Random(100);
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(20);
        private static string _fileName = "c:\\temp\\accounts.txt";
        private static ReaderWriterLockSlim _fileLock = new ReaderWriterLockSlim();
        private static AsyncDataManager<string> _asyncDataManager;



        static void Main(string[] args)
        {
            var cancellationTokenSourceForAsync = new CancellationTokenSource(/*10000*/);
            var sw = Stopwatch.StartNew();
            //DownloadDataAsyncTasks(GetAccountsToProcess(100), cancellationTokenSourceForAsync.Token);
            //Console.WriteLine($"Async tasks complete in {sw.Elapsed}");

            sw.Restart();

            var cancellationTokenSourceForDataflow = new CancellationTokenSource(/*10000*/);
            DownloadDataWithDataFlow(GetAccountsToProcess(100), cancellationTokenSourceForDataflow.Token);
            Console.WriteLine($"Dataflow tasks complete in {sw.Elapsed}");
            //var cancellationTokenSourceForDataflow = new CancellationTokenSource(/*10000*/);
            //DownloadDataWithDataFlow(GetAccountsToProcess(100), cancellationTokenSourceForDataflow.Token);
            //Console.WriteLine($"Dataflow tasks complete in {sw.Elapsed}");

        }

        private static void DownloadDataWithDataFlow(IEnumerable<string> accountList, CancellationToken token)
        {
            int concurrentTasks = 50;
            var consumerList = new List<ActionBlock<string>>();
            Enumerable.Range(0, concurrentTasks).ToList().ForEach(arg =>
                consumerList.Add(new ActionBlock<string>(GetAndWriteData, new ExecutionDataflowBlockOptions { BoundedCapacity = 10 })));

            _asyncDataManager = new AsyncDataManager<string>(50, consumerList);

            try
            {
                foreach (var account in accountList)
                    _asyncDataManager.Produce(account, token).Wait(token);

                _asyncDataManager.Completed(token);
            }
            catch (AggregateException e)
            {
                e.Handle(ex =>
                {
                    if (ex is TaskCanceledException)
                    {
                        Console.WriteLine("This task has been cancelled.  Cleaning up temp files.");
                        CleanUpTempFiles();
                    }

                    if (ex is AccountFailedToDownloadException)
                    {
                        Console.WriteLine($"Account has failed to download. The error is {ex.Message}");
                    }


                    return ex is TaskCanceledException || ex is AccountFailedToDownloadException;
                });
            }
            catch (OperationCanceledException e)
            {
                Console.WriteLine("This task has been cancelled.  Cleaning up temp files.");
                CleanUpTempFiles();
            }
            catch (AccountFailedToDownloadException e)
            {
                Console.WriteLine($"Account has failed to download. The error is {e.Message}");
            }
        }

        private static void DownloadDataAsyncTasks(IEnumerable<string> accountList, CancellationToken token)
        {
            var tasks = accountList.Select(x => GetDataAsync(x, token)).ToList();

            Console.WriteLine($"TaskCount {tasks.Count}");

            while (tasks.Any())
            {
                // ReSharper disable once CoVariantArrayConversion
                var i = Task.WaitAny(tasks.ToArray());
                try
                {
                    WriteToFile(tasks[i].Result);
                }
                catch (AggregateException e)
                {
                    e.Handle(ex =>
                    {
                        if (ex is TaskCanceledException)
                        {
                            Console.WriteLine("This task has been cancelled.  Cleaning up temp files.");
                            CleanUpTempFiles();
                        }

                        if (ex is AccountFailedToDownloadException)
                        {
                            Console.WriteLine($"Account has failed to download. The error is {ex.Message}");
                        }


                        return ex is TaskCanceledException || ex is AccountFailedToDownloadException;
                    });
                }

                tasks.RemoveAt(i);
            }

            Console.WriteLine($"Ended tasks running:{tasks.Count(x => !x.IsCompleted)}");
        }

        /// <summary>
        /// Runs tasks and returns the results as they complete
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tasks"></param>
        /// <returns></returns>
        public static Task<Task<T>> [] Interleaved<T>(IEnumerable<Task<T>> tasks) 
        { 
            var inputTasks = tasks.ToList();

            var buckets = new TaskCompletionSource<Task<T>>[inputTasks.Count]; 
            var results = new Task<Task<T>>[buckets.Length]; 
            for (int i = 0; i < buckets.Length; i++)  
            { 
                buckets[i] = new TaskCompletionSource<Task<T>>(); 
                results[i] = buckets[i].Task; 
            }

            int nextTaskIndex = -1; 
            Action<Task<T>> continuation = completed => 
            { 
                var bucket = buckets[Interlocked.Increment(ref nextTaskIndex)]; 
                bucket.TrySetResult(completed); 
            };

            foreach (var inputTask in inputTasks) 
                inputTask.ContinueWith(continuation, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return results; 
        }

        private static async Task<XElement> GetDataAsync(string accountCode, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);
            try
            {
                var getDataTask = Task.Run(() => GetData(accountCode), token);
                var result = await getDataTask;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static void GetAndWriteData(string accountCode)
        {
            // test to see if exceptions are handled
            if (accountCode.EndsWith("5"))
                throw new AccountFailedToDownloadException($"Error processing account {accountCode}");

            var data = GetData(accountCode);
            
            WriteToFile(data);
        }

        private static void WriteToFile(XElement result)
        {
            _fileLock.EnterWriteLock();
            try
            {
                File.AppendAllText(_fileName, result.ToString());
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }
        }

        private static void CleanUpTempFiles()
        {
            _fileLock.EnterWriteLock();
            try
            {
                File.Delete(_fileName);
            }
            finally
            {
                _fileLock.ExitWriteLock();
            }

        }

        private static XElement GetData(string accountCode)
        {
            var waitTime = _random.Next(100, 1000);
            Console.WriteLine($"Account {accountCode} waiting for {waitTime}");
            Task.Delay(waitTime);
            Console.WriteLine($"Account {accountCode} completed in {waitTime}");
            // test to see if exceptions are handled
            if (accountCode.EndsWith("5"))
                throw new AccountFailedToDownloadException($"Error processing account {accountCode}");
            //if (accountCode == "ACC16")
            //{
            //    try
            //    {                    
            //        return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account1>");
            //    }
            //    catch (Exception e)
            //    {
            //        throw new Exception($"Error parsing xml for account {accountCode}.  The Error is {e.Message}",e);
            //    }
            //}
            return XElement.Parse($"<Account><AccountCode>{accountCode}</AccountCode></Account>");

        }

        private static IList<string> GetAccountsToProcess(int count)
        {
            return Enumerable.Range(1, count).Select(i => $"ACC{i}").ToList();
        }

    }

    
    public class AccountFailedToDownloadException : Exception
    {
        public AccountFailedToDownloadException() : base()
        {

        }

        public AccountFailedToDownloadException(string message) : base(message)
        {

        }

    }
}
