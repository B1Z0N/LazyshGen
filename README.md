# Lazysh source gen

Sort of source generator test to try to achieve this:


```csharp
interface ILoaded 
{ 
    int DoInt(); 
    void DoVoid();
}

class Loaded : ILoaded 
{ 
    public int DoInt() => 5; 
    public void DoVoid() => Console.WriteLine("This!");
}

class Loader
{
    public ILoaded Load()
    {
        Thread.Sleep(1000); // time consuming operation
        return new Loaded();
    }
}

public static class Program
{
    public static void Main()
    {
        var loader = new Loader();
        var lazyLoaded = new LazyLoaded(() => loader.Load()); // very fast(not yet loaded)

        Console.WriteLine(lazyLoaded.DoInt()); // this takes one second(loads)
        lazyLoaded.DoVoid(); // again very fast(already loaded)
    }
}

// generated
class LazyLoaded : Lazy<ILoaded>, ILoaded
{
    public LazyLoaded(Func<ILoaded> val) :base(val)
    {
    }

    public int DoInt() => this.Value.DoInt();
    public void DoVoid() => this.Value.DoVoid();
}
``` 
