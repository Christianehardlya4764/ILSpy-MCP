namespace ILSpy.Mcp.TestTargets;

public class AsyncExample
{
    public async Task<int> DoWorkAsync()
    {
        await Task.Delay(1);
        return 42;
    }
}

public class LambdaExample
{
    public Func<int, int> CreateAdder(int x)
    {
        return y => x + y;
    }
}
