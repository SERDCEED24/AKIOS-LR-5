using System.Diagnostics; // Подключаем пространство имен для работы с таймером (Stopwatch) и другими диагностическими инструментами

namespace ThreadPool // Определяем пространство имен ThreadPool для организации кода
{
    internal class Program // Объявляем внутренний класс Program, содержащий основной код программы
    {
        // Метод для проверки, является ли число простым
        static bool IsPrime(int n)
        {
            // Цикл начинается с 2 (наименьший делитель) и идет до корня из n + 1
            for (int i = 2; i <= Math.Sqrt(n) + 1; i++)
                // Если число n делится на i без остатка, оно не простое
                if (n % i == 0)
                    return false; // Возвращаем false, так как нашли делитель
            return true; // Если делителей нет, число простое, возвращаем true
        }

        // Метод для подсчета простых чисел в однопоточном режиме
        static long[] CountPrimeOneThread(List<int> list)
        {
            int count = 0; // Переменная для подсчета количества простых чисел
            Stopwatch sw = Stopwatch.StartNew(); // Создаем и запускаем таймер для измерения времени выполнения
            // Проходим по каждому числу в списке
            foreach (int num in list)
            {
                // Если число простое, увеличиваем счетчик
                if (IsPrime(num))
                    count++;
            }
            sw.Stop(); // Останавливаем таймер после завершения подсчета
            // Возвращаем массив из двух элементов: количество простых чисел и время выполнения в миллисекундах
            return [count, sw.ElapsedMilliseconds];
        }

        // Метод для подсчета простых чисел в заданном диапазоне списка (используется в многопоточности)
        static void CountPrime(List<int> list, int startIndex, int endIndex, ref int count)
        {
            int localCount = 0; // Локальная переменная для подсчета простых чисел в текущем потоке
            // Проходим по списку от startIndex до endIndex
            for (int i = startIndex; i < endIndex; i++)
            {
                // Если текущее число простое, увеличиваем локальный счетчик
                if (IsPrime(list[i]))
                    localCount++;
            }
            // Безопасно добавляем локальный счетчик к общей переменной count, используя Interlocked для синхронизации между потоками
            Interlocked.Add(ref count, localCount);
        }

        // Метод для подсчета простых чисел с использованием нескольких потоков
        static long[] CountPrimeMultipleThreads(List<int> list, int threadCount)
        {
            int count = 0; // Переменная для хранения общего количества простых чисел
            var sw = Stopwatch.StartNew(); // Запускаем таймер для измерения времени выполнения
            int chunkSize = list.Count / threadCount; // Вычисляем размер одного диапазона для каждого потока
            Thread[] threads = new Thread[threadCount]; // Создаем массив потоков
            // Создаем и запускаем потоки
            for (int i = 0; i < threadCount; i++)
            {
                int start = i * chunkSize; // Начальный индекс диапазона для текущего потока
                // Конечный индекс: для последнего потока берем весь остаток списка
                int end = (i == threadCount - 1) ? list.Count : start + chunkSize;
                // Создаем новый поток, который выполнит метод CountPrime для заданного диапазона
                threads[i] = new Thread(() => CountPrime(list, start, end, ref count));
                threads[i].Start(); // Запускаем поток
            }
            // Ожидаем завершения всех потоков
            foreach (var thread in threads)
            {
                thread.Join(); // Блокируем основной поток, пока текущий не завершится
            }
            sw.Stop(); // Останавливаем таймер
            // Возвращаем массив: количество простых чисел и время выполнения
            return [count, sw.ElapsedMilliseconds];
        }

        // Метод для подсчета простых чисел с использованием пула потоков
        static long[] CountPrimeThreadPool(List<int> list, int threadCount, int chunkCount)
        {
            var sw = Stopwatch.StartNew(); // Запускаем таймер для измерения времени
            int count = 0; // Переменная для хранения общего количества простых чисел
            int chunkSize = list.Count / chunkCount; // Вычисляем размер каждого диапазона
            Thread[] threads = new Thread[threadCount]; // Создаем массив потоков
            Queue<int[]> queue = new Queue<int[]>(); // Очередь для хранения диапазонов индексов
            // Заполняем очередь диапазонами индексов
            for (int i = 0; i < chunkCount; i++)
            {
                int start = i * chunkSize; // Начальный индекс текущего диапазона
                // Конечный индекс: для последнего диапазона берем весь остаток списка
                int end = (i == chunkCount - 1) ? list.Count : start + chunkSize;
                queue.Enqueue([start, end]); // Добавляем диапазон в очередь
            }
            object queueLock = new object(); // Объект для синхронизации доступа к очереди
            bool isDone = false; // Флаг, указывающий, что все задачи выполнены
            // Создаем и запускаем потоки
            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    // Бесконечный цикл для обработки задач из очереди
                    while (true)
                    {
                        int[] task; // Переменная для хранения текущего диапазона
                        lock (queueLock) // Блокируем очередь для безопасного доступа
                        {
                            // Если очередь пуста и флаг isDone true, завершаем поток
                            if (queue.Count == 0)
                            {
                                if (isDone) return;
                                continue; // Если очередь пуста, но задачи еще есть, продолжаем
                            }
                            task = queue.Dequeue(); // Извлекаем диапазон из очереди
                        }
                        int localCount = 0; // Локальный счетчик простых чисел
                        // Обрабатываем текущий диапазон
                        for (int i = task[0]; i < task[1]; i++)
                        {
                            // Если число простое, увеличиваем локальный счетчик
                            if (IsPrime(list[i]))
                                localCount++;
                        }
                        // Безопасно добавляем локальный счетчик к общему
                        Interlocked.Add(ref count, localCount);
                    }
                });
                threads[i].Start(); // Запускаем поток
            }
            // Ожидаем, пока очередь не опустеет
            while (true)
            {
                lock (queueLock) // Блокируем очередь для проверки
                {
                    // Если очередь пуста, устанавливаем флаг isDone и выходим
                    if (queue.Count == 0)
                    {
                        isDone = true;
                        break;
                    }
                }
            }
            // Ожидаем завершения всех потоков
            foreach (var thread in threads)
            {
                thread.Join();
            }
            sw.Stop(); // Останавливаем таймер
            // Возвращаем массив: количество простых чисел и время выполнения
            return [count, sw.ElapsedMilliseconds];
        }

        // Главный метод программы
        static void Main(string[] args)
        {
            var rnd = new Random(); // Создаем объект для генерации случайных чисел
            var nums = new List<int>(); // Создаем список для хранения чисел
            // Заполняем список 100 миллионами случайных чисел от 10^4 до 10^5
            for (int i = 0; i < 100000000; i++)
            {
                nums.Add(rnd.Next((int)Math.Pow(10, 4), (int)Math.Pow(10, 5)));
            }

            // Выполняем подсчет простых чисел в однопоточном режиме
            var resOneThread = CountPrimeOneThread(nums);
            // Выводим результаты: количество простых чисел и время выполнения
            Console.WriteLine($"Результаты в однопотоке: {resOneThread[0]} чисел, {resOneThread[1]} мс.");

            // Тестируем многопоточный режим с разным количеством потоков
            long minTime = 1000000000; // Переменная для хранения минимального времени выполнения
            int bestThreadCount = 14; // Начальное значение оптимального количества потоков
            // Перебираем количество потоков от количества логических ядер процессора до 20
            for (int i = Environment.ProcessorCount; i < 20; ++i)
            {
                // Выполняем подсчет простых чисел в многопоточном режиме
                var resMultipleThreads = CountPrimeMultipleThreads(nums, i);
                // Выводим результаты для текущего количества потоков
                Console.WriteLine($"Результаты в многопотоке ({i} потоков и диапазонов): {resMultipleThreads[0]} чисел, {resMultipleThreads[1]} мс.");
                // Если время выполнения меньше минимального, обновляем значения
                if (resMultipleThreads[1] < minTime)
                {
                    minTime = resMultipleThreads[1]; // Обновляем минимальное время
                    bestThreadCount = i; // Сохраняем оптимальное количество потоков
                }
            }
            // Выводим лучшее время и соответствующее количество потоков
            Console.WriteLine($"Лучшее время на многопотоке: {minTime} мс.; Кол-во потоков: {bestThreadCount}");

            // Тестируем пул потоков с разным количеством диапазонов
            minTime = 1000000000; // Сбрасываем минимальное время
            int bestChunkCount = bestThreadCount; // Начальное значение оптимального количества диапазонов
            // Перебираем количество диапазонов от удвоенного bestThreadCount до 100 с шагом bestThreadCount
            for (int i = bestThreadCount * 2; i < 100; i += bestThreadCount)
            {
                // Выполняем подсчет простых чисел с использованием пула потоков
                var resThreadPool = CountPrimeThreadPool(nums, bestThreadCount, i);
                // Выводим результаты для текущего количества потоков и диапазонов
                Console.WriteLine($"Результаты с пулом потоков ({bestThreadCount} потоков, {i} диапазонов): {resThreadPool[0]} чисел, {resThreadPool[1]} мс.");
                // Если время выполнения меньше минимального, обновляем значения
                if (resThreadPool[1] < minTime)
                {
                    minTime = resThreadPool[1]; // Обновляем минимальное время
                    bestChunkCount = i; // Сохраняем оптимальное количество диапазонов
                }
            }
            // Выводим лучшее время, количество диапазонов и потоков
            Console.WriteLine($"Лучшее время на пуле потоков: {minTime} мс.; Кол-во диапазонов: {bestChunkCount}; Кол-во потоков: {bestThreadCount}");
        }
    }
}