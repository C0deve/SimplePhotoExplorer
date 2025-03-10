using System;
using System.Reactive.Linq;

namespace PhotoExplorer.ReactiveUI.ViewModels;

public static class Extensions
{
    public static IObservable<T> Log<T>(this IObservable<T> observable,
        string msg = "") =>
        observable.Do(
            x => Console.WriteLine("{0} - OnNext({1})", msg, x),
            ex =>
            {
                Console.WriteLine("{0} - OnError:", msg);
                Console.WriteLine((string)"\t {0}", (object?)ex);
            },
            () => Console.WriteLine("{0} - OnCompleted()", msg));
}