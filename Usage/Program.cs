using System;
using System.Threading;
using LazyshImplDefault;

namespace Usage
{
    [Lazysh] // our attribute to implement interface as lazy one
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
            var lazyLoaded = LazyshLoaded.Create(() => loader.Load()); // very fast(not yet loaded)
            
            Console.WriteLine(lazyLoaded.DoInt()); // this takes one second(loads)
            lazyLoaded.DoVoid(); // again very fast(already loaded)
        }
    } 
}
