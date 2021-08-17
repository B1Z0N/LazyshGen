using System;
using System.Threading;
using LazyshImpl;
using Microsoft.VisualBasic;

// this one contains three interfaces: ILoaded, IDisposable, IUnknown
// first two are registered for lazyness within MyLazyshFactory<T>
// the third one is not
// see Main to become comfortable with Lazysh source generator
namespace Usage
{
    
    #region ILoaded

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

    #endregion

    class MyDisposed : IDisposable
    {
        public MyDisposed() => Console.WriteLine("Created MyDisposed");

        void IDisposable.Dispose()
        {
        }
    }

    interface IUnknown
    {
    }

    class Unknown : IUnknown
    {
    }
    
    public static class MyLazyshFactory<[Lazysh(typeof(ILoaded), typeof(IDisposable))] T>
    {
    }

    public static class Program
    {
        public static void Main()
        {
            //
            // the lazysh was registered in MyLazyshFactory<T>
            Func<ILoaded> actualLoadedGetter = () => new Loader().Load();

            // 1. get directly with specific class constructor
            UseLazyshILoaded(() => new LazyshLoaded(actualLoadedGetter));
            // 2. get with it's static create method
            UseLazyshILoaded(() => LazyshLoaded.Create(actualLoadedGetter));
            // 3. get with generic lazysh factory for getting all generated lazies
            UseLazyshILoaded(() => LazyshFactory.Get(actualLoadedGetter));
            // 4. try get with generic lazysh factory for getting all generated lazies
            UseLazyshILoaded(() => LazyshFactory.TryGet(actualLoadedGetter));
            // 5. get with your own named lazysh factory for getting only your specified lazies
            // see generic MyLazyshFactory down below
            // returns proper lazysh because it was registered with appropriate MyLazyshFactory<T>
            UseLazyshILoaded(() => MyLazyshFactory.Get(actualLoadedGetter));
            // 6. try get with your own named lazysh factory for getting only your specified lazies
            // see generic MyLazyshFactory down below
            // returns proper lazysh because it was registered with appropriate MyLazyshFactory<T>
            UseLazyshILoaded(() => MyLazyshFactory.TryGet(actualLoadedGetter));

            //
            // the lazysh was not registered
            Func<IUnknown> actualUnknownGetter = () => new Unknown();


            // 1. 2. corresponding LazyshUnknown was not generated
            // UseLazyshIUnknown(() => new LazyshUnknown(actualUnknownGetter));
            // UseLazyshIUnknown(() => LazyshUnknown.Create(actualUnknownGetter)); 
            // 3. throws because it was not generated
            RunCatchPrintLazysh(() =>
                UseLazyshIUnknown(() => LazyshFactory.Get(actualUnknownGetter))
            );
            // 4. returns null because it was not generated
            UseLazyshIUnknown(() => LazyshFactory.TryGet(actualUnknownGetter));
            // 5. throws because it was not registered in MyLazyshFactory<T>
            RunCatchPrintLazysh(() =>
                UseLazyshIUnknown(() => MyLazyshFactory.Get(actualUnknownGetter))
            );
            // 6. returns null because it was not registered in MyLazyshFactory<T>
            UseLazyshIUnknown(() => LazyshFactory.TryGet(actualUnknownGetter));

            //
            // another lazysh that was actually registered in MyLazyshFactory<T>
            Func<IDisposable> actualDisposableGetter = () => new MyDisposed();
            // 1. get directly with specific class constructor
            UseLazyshIDisposable(() => new LazyshDisposable(actualDisposableGetter));
            // 2. get with it's static create method
            UseLazyshIDisposable(() => LazyshDisposable.Create(actualDisposableGetter));
            // 3. get with generic lazysh factory for getting all generated lazies
            UseLazyshIDisposable(() => LazyshFactory.Get(actualDisposableGetter));
            // 4. try get with generic lazysh factory for getting all generated lazies
            UseLazyshIDisposable(() => LazyshFactory.TryGet(actualDisposableGetter));
            // 5. get with your own named lazysh factory for getting only your specified lazies
            // see generic MyLazyshFactory down below
            // returns proper lazysh because it was registered with appropriate MyLazyshFactory<T>
            UseLazyshIDisposable(() => MyLazyshFactory.Get(actualDisposableGetter));
            // 6. try get with your own named lazysh factory for getting only your specified lazies
            // see generic MyLazyshFactory down below
            // returns proper lazysh because it was registered with appropriate MyLazyshFactory<T>
            UseLazyshIDisposable(() => MyLazyshFactory.TryGet(actualDisposableGetter));
            
            // and so on
            // you can actually create other factories and register there lazies that are 
            // available in that factory class's Get/TryGet and automatically added to LazyshImpl.LazyshFactory
            
            // perks:
            // 1. register any types you want, even if you don't have any control over it's source code
            // (unlike registering based on class declarating attributes)
            // 2. segregate on what is "your" lazy and what's not - what was registered in another namespace
            // (using your local SomeLazyshFactory<T>
            // 3. access every lazysh registered through globally accessible LazyshImpl.LazyshFactory
            // 4. an LazyshArgumentException for some missing lazysh interface
            // (not declared in a specific factory or at all in any factory)
        }

        private static void UseLazyshILoaded(Func<ILoaded> getter)
        {
            var lazyLoaded = getter(); // very fast(not yet loaded)

            Console.WriteLine(lazyLoaded.DoInt()); // this takes one second(loads)
            lazyLoaded.DoVoid(); // again very fast(already loaded)
        }

        private static void UseLazyshIUnknown(Func<IUnknown> getter)
        {
            var lazyUnknown = getter();
        }

        private static void UseLazyshIDisposable(Func<IDisposable> getter)
        {
            var lazyDisposable = getter();

            lazyDisposable.Dispose(); // prints "created MyDisposed"
        }

        private static void RunCatchPrintLazysh(Action f)
        {
            try
            {
                f();
            }
            catch (LazyshArgumentException e)
            { 
                // outputs wat type is not yet supported in lazy factory
                // and what are the supported ones
                // and how could one register for lazyness
                Console.WriteLine(e.Message);
            }
        }
    }

}