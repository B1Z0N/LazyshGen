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
    public ILoaded GetLoader()
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
public class LazyLoaded : Lazy<ILoaded>, ILoaded
{
    public int DoInt() => this.Value.DoInt();
    public int DoVoid() => this.Value.DoVoid();
}

``` 
