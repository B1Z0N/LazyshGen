using System;
using System.Threading;
using LazyshImpl;

namespace Usage
{
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
            
            // var lazyLoaded = LazyshLoaded.Create(() => loader.Load()); // very fast(not yet loaded)
            
            //Console.WriteLine(lazyLoaded.DoInt()); // this takes one second(loads)
            //lazyLoaded.DoVoid(); // again very fast(already loaded)
        }
    }
    
    public static partial class LazyshFactory<[Lazysh(typeof(ILoaded), typeof(IDisposable))] T> { }
}
