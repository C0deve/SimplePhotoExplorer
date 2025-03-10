using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace PhotoExplorer.ReactiveUI.ViewModels;

public class MainWindowViewModel : ViewModelBase, IActivatableViewModel
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif"];
    private readonly SemaphoreSlim _semaphore = new(4);

    public string Greeting => "Photos :)";
    public ObservableCollection<DriveInfo> Drives { get; } = [];
    public ViewModelActivator Activator { get; }
    [Reactive] public DriveInfo? Drive { get; set; }
    [Reactive] public ADirectory? CurrentFirstLevel { get; set; }
    [Reactive] public ADirectory? CurrentSecondLevel { get; set; }

    private readonly ObservableAsPropertyHelper<ADirectory[]> _firstLevel;
    public ADirectory[] FirstLevel => _firstLevel.Value;

    private readonly ObservableAsPropertyHelper<ADirectory[]> _secondLevel;
    public ADirectory[] SecondLevel => _secondLevel.Value;

    public ObservableCollection<Bitmap> Images { get; } = [];

    private readonly ObservableAsPropertyHelper<bool> _isLoading;
    private readonly ReactiveCommand<Unit, Unit> _cancel = ReactiveCommand.Create(() => { });
    public bool IsLoading => _isLoading.Value;

    public MainWindowViewModel()
    {
        Activator = new ViewModelActivator();
        this.WhenActivated(new Action<IDisposable>(_ =>
        {
            foreach (var driveInfo in DriveInfo.GetDrives()) Drives.Add(driveInfo);
        }));

        var loadFirstLevel = ReactiveCommand.CreateRunInBackground<string?, ADirectory[]>(GetDirectories);
        var loadSecondLevel = ReactiveCommand.CreateRunInBackground<string?, ADirectory[]>(GetDirectories);
        var loadImages =
            ReactiveCommand.CreateFromObservable<string?, Bitmap>(
                s => (string.IsNullOrEmpty(s)
                    ? Observable.Empty<Bitmap>()
                    : GetImages(s)).TakeUntil(_cancel));

        _firstLevel = loadFirstLevel.ToProperty(this, vm => vm.FirstLevel, () => []);
        _secondLevel = loadSecondLevel.ToProperty(this, vm => vm.SecondLevel, () => []);
        
        loadImages
            .ObserveOn(RxApp.MainThreadScheduler)
            .Do(bitmap => Images.Add(bitmap))
            .TakeUntil(_cancel)
            .Repeat()
            .Subscribe();

        this.WhenAnyValue(vm => vm.Drive)
            .Select(info => info?.Name)
            .InvokeCommand(loadFirstLevel);

        this.WhenAnyValue(vm => vm.CurrentFirstLevel)
            .Select(model => model?.FullPath)
            .InvokeCommand(loadSecondLevel);

        // ReSharper disable once InvokeAsExtensionMethod
        Observable.Merge(
                this.WhenAnyValue(vm => vm.CurrentFirstLevel),
                this.WhenAnyValue(vm => vm.CurrentSecondLevel))
            .Do(_ =>
            {
                _cancel.Execute().Subscribe();
                Images.Clear();
            })
            .Select(model => model?.FullPath)
            .ObserveOn(RxApp.TaskpoolScheduler)
            .InvokeCommand(loadImages);

        _isLoading =
            Observable.Merge(
                    loadFirstLevel.IsExecuting,
                    loadSecondLevel.IsExecuting,
                    loadImages.IsExecuting)
                .ToProperty(this, vm => vm.IsLoading);

        Observable.Merge(
                loadFirstLevel.ThrownExceptions,
                loadSecondLevel.ThrownExceptions,
                loadImages.ThrownExceptions)
            .Do(exception => Console.WriteLine(exception.Message))
            .Subscribe();
    }

    private static ADirectory[] GetDirectories(string? parentPath) =>
        string.IsNullOrEmpty(parentPath)
            ? []
            : Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new ADirectory(path))
                .ToArray();

    private IObservable<Bitmap> GetImages(string parentPath) =>
        Directory
            .EnumerateFiles(parentPath)
            .Where(s => ImageExtensions.Contains(Path.GetExtension(s)))
            .ToObservable()
            .SelectMany((file, _, ct) => GetImageAsync(file, ct))
            .Log("bitmap provided");

    private Task<Bitmap> GetImageAsync(string path, CancellationToken ct) =>
        Task.Run(() =>
        {
            int semaphoreCount;
            Bitmap bitmap;
            
            // Each task begins by requesting the semaphore.
            Console.WriteLine($"Task {Task.CurrentId} begins and waits for the semaphore.");

            _semaphore.Wait(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                Console.WriteLine($"Processing {path}, semaphore count: {_semaphore.CurrentCount}");
                using var stream = File.OpenRead(path);

                bitmap = Bitmap.DecodeToWidth(stream, 200, BitmapInterpolationMode.LowQuality);
            }
            finally
            {
                semaphoreCount = _semaphore.Release();
            }

            Console.WriteLine($"Task {Task.CurrentId} releases the semaphore; previous count: {semaphoreCount}.");
            return bitmap;
        }, ct);
}