namespace TFFmpeg

open Elmish
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Main
open Avalonia.Threading

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "TFFmpeg"
        base.Width <- 800.0
        base.Height <- 450.0

#if DEBUG
        this.AttachDevTools(Input.KeyGesture(Input.Key.F12))
#endif

        let transformer = ffmpeg.Transformer()
        let syncDispatch (dispatch: Dispatch<'msg>): Dispatch<'msg> =
            match Dispatcher.UIThread.CheckAccess() with
            | true -> fun msg -> Dispatcher.UIThread.Post(fun () -> dispatch msg)
            | false -> fun msg -> dispatch msg

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        Elmish.Program.mkProgram (fun window -> initState window transformer) update view
        |> Program.withHost this
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.withSyncDispatch syncDispatch
        |> Program.withSubscription (fun _ -> Subs.progress(transformer) )
        |> Program.withSubscription (fun _ -> Subs.finished(transformer) )
        |> Program.withSubscription (fun _ -> Subs.error(transformer) )
        |> Program.runWith(this)

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Load "avares://Avalonia.Themes.Default/DefaultTheme.xaml"
        this.Styles.Load "avares://Avalonia.Themes.Default/Accents/BaseDark.xaml"
        this.Styles.Load "avares://TFFmpeg/Style.FixLinuxFont.xaml"

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)