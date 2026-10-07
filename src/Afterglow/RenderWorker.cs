using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Afterglow;

// WPF's software renderer needs an STA. Frozen results can cross to the UI;
// serial work also keeps the shared backdrop cache on one rendering thread.
internal sealed class RenderWorker : IDisposable
{
    internal static readonly RenderWorker Wallpapers=new("Afterglow wallpaper renderer");
    private readonly Dispatcher dispatcher;
    internal RenderWorker(string name)
    {
        var started=new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{started.SetResult(Dispatcher.CurrentDispatcher);Dispatcher.Run();})
        {Name=name,IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();dispatcher=started.Task.GetAwaiter().GetResult();
    }
    internal Task<T> Run<T>(Func<T> work,CancellationToken cancellation=default)=>dispatcher.InvokeAsync(work,DispatcherPriority.Background,cancellation).Task;
    internal Task RunAsync(Func<Task> work)=>dispatcher.InvokeAsync(work,DispatcherPriority.Normal).Task.Unwrap();
    internal T Read<T>(Func<T> work)=>dispatcher.CheckAccess()?work():dispatcher.Invoke(work);
    internal void Invoke(Action work){if(dispatcher.CheckAccess())work();else dispatcher.Invoke(work);}
    public void Dispose()=>dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
}
