using System.Diagnostics;

namespace ThreadPool
{
    internal class Program
    {
        static bool IsPrime(int n)
        {
            for (int i = 2; i <= Math.Sqrt(n) + 1; i++)
                if (n % i == 0)
                    return false;
            return true;
        }
        static long[] CountPrimeOneThread(List<int> list)
        {
            int count = 0;
            Stopwatch sw = Stopwatch.StartNew();
            foreach (int num in list)
            {
                if (IsPrime(num))
                    count++;
            }
            sw.Stop();
            return [count, sw.ElapsedMilliseconds];
        }
        static void CountPrime(List<int> list, int startIndex, int endIndex, ref int count)
        {
            int localCount = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (IsPrime(list[i]))
                    localCount++;
            }
            Interlocked.Add(ref count, localCount);
        }
        static long[] CountPrimeMultipleThreads(List<int> list, int threadCount)
        {
            int count = 0;
            var sw = Stopwatch.StartNew();
            int chunkSize = list.Count / threadCount;
            Thread[] threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                int start = i * chunkSize;
                int end = (i == threadCount - 1) ? list.Count : start + chunkSize;

                threads[i] = new Thread(() => CountPrime(list, start, end, ref count));
                threads[i].Start();
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            sw.Stop();
            return [count, sw.ElapsedMilliseconds];
        }
        static long[] CountPrimeThreadPool(List<int> list, int threadCount, int chunkCount)
        {
            var sw = Stopwatch.StartNew();
            int count = 0;
            int chunkSize = list.Count / chunkCount;
            Thread[] threads = new Thread[threadCount];
            Queue<int[]> queue = new Queue<int[]>();
            for (int i = 0; i < chunkCount; i++)
            {
                int start = i * chunkSize;
                int end = (i == chunkCount - 1) ? list.Count : start + chunkSize;
                queue.Enqueue([start, end]);
            }
            object queueLock = new object();
            bool isDone = false;
            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    while (true)
                    {
                        int[] task;
                        lock (queueLock)
                        {
                            if (queue.Count == 0)
                            {
                                if (isDone) return;
                                continue;
                            }
                            task = queue.Dequeue();
                        }
                        int localCount = 0;
                        for (int i = task[0]; i < task[1]; i++)
                        {
                            if (IsPrime(list[i]))
                                localCount++;
                        }
                        Interlocked.Add(ref count, localCount);
                    }
                });
                threads[i].Start();
            }
            while (true)
            {
                lock (queueLock)
                {
                    if (queue.Count == 0)
                    {
                        isDone = true;
                        break;
                    }
                }
            }
            foreach (var thread in threads)
            {
                thread.Join();
            }
            sw.Stop();
            return [count, sw.ElapsedMilliseconds];
        }
        static void Main(string[] args)
        {
            var rnd = new Random();
            var nums = new List<int>();
            for (int i = 0; i < 100000000; i++)
            {
                nums.Add(rnd.Next((int)Math.Pow(10, 4), (int)Math.Pow(10, 5)));
            }

            // Однопоток
            var resOneThread = CountPrimeOneThread(nums);
            Console.WriteLine($"Результаты в однопотоке: {resOneThread[0]} чисел, {resOneThread[1]} мс.");

            // Многопоток с экспериментами
            long minTime = 1000000000;
            int bestThreadCount = 14;
            for (int i = Environment.ProcessorCount; i < 20; ++i)
            {
                var resMultipleThreads = CountPrimeMultipleThreads(nums, i);
                Console.WriteLine($"Результаты в многопотоке ({i} потоков и диапазонов): {resMultipleThreads[0]} чисел, {resMultipleThreads[1]} мс.");
                if (resMultipleThreads[1] < minTime)
                {
                    minTime = resMultipleThreads[1];
                    bestThreadCount = i;
                }
            }
            Console.WriteLine($"Лучшее время на многопотоке: {minTime} мс.; Кол-во потоков: {bestThreadCount}");

            // Пул потоков с экспериментами
            minTime = 1000000000;
            int bestChunkCount = bestThreadCount;
            for (int i = bestThreadCount * 2; i < 100; i += bestThreadCount)
            {
                var resThreadPool = CountPrimeThreadPool(nums, bestThreadCount, i);
                Console.WriteLine($"Результаты с пулом потоков ({bestThreadCount} потоков, {i} диапазонов): {resThreadPool[0]} чисел, {resThreadPool[1]} мс.");
                if (resThreadPool[1] < minTime)
                {
                    minTime = resThreadPool[1];
                    bestChunkCount = i;
                }
            }
            Console.WriteLine($"Лучшее время на пуле потоков: {minTime} мс.; Кол-во диапазонов: {bestChunkCount}; Кол-во потоков: {bestThreadCount}");
        }
    }
}
