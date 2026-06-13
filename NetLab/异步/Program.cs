namespace 异步
{
    internal class Program
    {
        static async Task Main()
        {
            await Task.Delay(5000);
            Console.WriteLine("异步完成了");
            // 等 5 秒才返回 → 进程才结束
        }
    }
}
