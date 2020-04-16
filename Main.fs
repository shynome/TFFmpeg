namespace TFFmpeg

module Main =
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Components.Hosts
    open Avalonia.Layout
    open System.Threading
    open Types
    open VideoFiles

    type VideoTransformStatus =
        | Ready
        | Finished
        | Progress of double

    type VideoFile =
        { Filepath: string
          mutable Status: VideoTransformStatus }

    let toVideoFile f =
        { Filepath = f
          Status = Ready }

    type Task = {
        TS: CancellationTokenSource
        Progress: double
        GoNext: bool
        Filepath: string
    }

    type State = {
            window: HostWindow
            VideoFiles: VideoFiles.State
            Task:  Task option
            Transformer: ffmpeg.Transformer
            Size: (int*int)
        }

    let initState window transformer =
        let s = {
            window = window
            Transformer = transformer
            VideoFiles = VideoFiles.init
            Task = None
            Size = (-2,-2)
        }
        s, Cmd.none

    type Msg =
        | SelectFiles
        | RemoveVideoFile of string
        | Finished of string * string
        | Error of string * string
        | Progress of string * double
        | GoNext
        | Control of Control
        | VideoFiles of VideoFiles.Msg
        | SetSize of (int*int)

    module Subs =
        let progress (transformer:ffmpeg.Transformer) =
            let sub dispatch = transformer.Progress.Subscribe(Progress >> dispatch) |> ignore
            Cmd.ofSub sub
        let finished (transformer:ffmpeg.Transformer) =
            let sub dispatch = transformer.Finished.Subscribe(Finished >> dispatch) |> ignore
            Cmd.ofSub sub
        let error (transformer:ffmpeg.Transformer) =
            let sub dispatch = transformer.Error.Subscribe(Error >> dispatch) |> ignore
            Cmd.ofSub sub

    let handleVideoFilesExternal (msg: Control option) =
        match msg with
        | None -> Cmd.none
        | Some msg ->
            Cmd.ofMsg (Control msg)

    let updateVideoFiles (state: VideoFiles.State) (file:string) (fn: (FileItem)->FileItem) =
        let files = state.Files
        let files =
            files
            |> Array.map (fun e ->
                if e.Filepath <> file then e else fn(e)
            )
        {state with Files = files}

    let handleControl (msg:Control) (state:State) =
        match msg with
        | Cancel file ->
            if state.Task = None then
                state, Cmd.none
            else
                let vf = state.VideoFiles
                let vf = updateVideoFiles vf file (fun f -> { f with Status = FileStatus.Ready })
                state.Task.Value.TS.Cancel()
                {state with Task = None; VideoFiles = vf}, Cmd.none
        | SetVideoFiles files ->
            let cmd =
                if isNull files then Cmd.none
                else Cmd.batch[
                        Cmd.ofMsg (AddFiles files |> VideoFiles)
                        Cmd.ofMsg (PerfectVideoFiles files |> Control)
                    ]
            state, cmd
        | PerfectVideoFiles files ->
            state, Cmd.OfAsync.perform state.Transformer.GetVideosMetadata files (UpdateVideoMeta>>VideoFiles)
        | Start (item, goNext) ->
            let filepath = item.Filepath
            let files = state.VideoFiles.Files
            let cursor = files |> Array.findIndex (fun f -> f.Filepath = item.Filepath)
            match cursor > files.Length - 1 with
            | true -> state, Cmd.none
            | false ->
                let vf = {state.VideoFiles with Cursor = cursor}
                let vf = updateVideoFiles vf item.Filepath (fun f -> {f with Status = Pending })
                try
                    let mutable size = ""
                    let (w,h) = state.Size
                    if w > 0 || h > 0 then
                        size <- sprintf " -vf scale=%s:%s" (string w) (string h)
                    let args = sprintf "%s" size
                    let ts = state.Transformer.TransformVideo item args
                    let task = {
                        TS = ts
                        Progress = 0.0
                        GoNext = goNext
                        Filepath = filepath
                    }
                    { state with Task = Some(task); VideoFiles = vf }, Cmd.none
                with e ->
                    state, Cmd.ofMsg ((filepath, e.ToString()) |> Error)

    let update (msg: Msg) (state: State) =
        match msg with
        | SetSize s ->
            { state with Size = s }, Cmd.none
        | VideoFiles msg ->
            let s, cmd, external = VideoFiles.update msg state.VideoFiles
            let mapped = Cmd.map VideoFiles cmd
            let handled = handleVideoFilesExternal external
            let batch = Cmd.batch[ mapped; handled ]
            { state with VideoFiles = s }, batch
        | SelectFiles ->
            let d = Dialogs.openFileDialog()
            let showDialog window = d.ShowAsync window |> Async.AwaitTask
            state, Cmd.OfAsync.perform showDialog state.window (SetVideoFiles >> Control)
        | Finished (file, outpath) ->
            let vf = state.VideoFiles
            let vf = updateVideoFiles vf file (fun f -> {f with Status = FileStatus.Finished outpath})
            let s = { state with Task = None; VideoFiles = vf }
            let cmd = state.Task.Value.GoNext
            let cmd = if cmd then Cmd.ofMsg GoNext else Cmd.none
            s, cmd
        | Error (file, e) ->
            let vf = state.VideoFiles
            let vf = updateVideoFiles vf file (fun f -> {f with Status = FileStatus.Error e})
            let s = { state with Task = None; VideoFiles = vf }
            let cmd = state.Task.Value.GoNext
            let cmd = if cmd then Cmd.ofMsg GoNext else Cmd.none
            s, cmd
        | GoNext ->
            let vf = state.VideoFiles
            let item = vf.Files |> Array.tryFind (fun f -> f.Status = FileStatus.Ready)
            match item with
            | None -> state, Cmd.none
            | Some item ->
                state, Cmd.ofMsg (Start (item, true) |> Control)
        | Progress (file, p) ->
            match state.Task with
            | None -> state, Cmd.none
            | Some task ->
                let t = { task with Progress = p }
                {state with Task = Some(t)}, Cmd.none
        | Control c ->
            handleControl c state
        | _ ->
            state, Cmd.none

    let actionBar (state: State) (dispatch: Msg -> unit) =
        DockPanel.create [
            DockPanel.children [
                DockPanel.create [
                    DockPanel.children [
                        Button.create [
                            Button.padding 10.0
                            Button.content (
                                let v = state.VideoFiles
                                let c =
                                    state.VideoFiles.Files
                                    |> Array.filter (fun f -> 
                                        match f.Status with
                                        | FileStatus.Finished _ -> false
                                        | _ -> true
                                    )
                                sprintf "%i/%i" c.Length v.Files.Length
                            )
                        ]
                        TextBlock.create [
                            TextBlock.width 8.0
                        ]
                        let (w,h) = state.Size
                        TextBlock.create [
                            TextBlock.text "w"
                        ]
                        TextBox.create [
                            TextBox.text (string w)
                            TextBox.height 30.0
                            TextBox.width 50.0
                            TextBox.onTextChanged (fun text ->
                                try
                                    text |> int |> (fun w->(w,-2)) |> SetSize |> dispatch
                                with e ->
                                    System.Console.WriteLine(e)
                            )
                        ]
                        TextBlock.create [
                            TextBlock.width 8.0
                        ]
                        TextBlock.create [
                            TextBlock.text "h"
                        ]
                        TextBox.create [
                            TextBox.text (string h)
                            TextBox.height 30.0
                            TextBox.width 50.0
                            TextBox.onTextChanged (fun text ->
                                try
                                    text |> int |> (fun h->(-2,h)) |> SetSize |> dispatch
                                with e ->
                                    System.Console.WriteLine(e)
                            )
                        ]
                    ]
                ]
                DockPanel.create [
                    DockPanel.horizontalAlignment HorizontalAlignment.Right
                    DockPanel.dock Dock.Right
                    DockPanel.children [
                        Button.create [
                            Button.padding 10.0
                            Button.content "select files"
                            Button.onClick (fun _ -> dispatch SelectFiles)
                        ]
                        TextBlock.create [
                            TextBox.width 10.0
                        ]
                        match state.Task with
                        | Some t ->
                            let text = sprintf "cancel transform %s/%s " (string t.Progress) "%"
                            Button.create [
                                Button.padding 10.0
                                Button.content text
                                Button.onClick (fun _ -> Control.Cancel t.Filepath |> Control |> dispatch)
                            ]
                        | None ->
                            Button.create [
                                Button.padding 10.0
                                Button.content "start transform"
                                Button.onClick (fun _ ->
                                    let c = state.VideoFiles.Cursor
                                    let f = state.VideoFiles.Files
                                    let f = f.[c]
                                    Start (f, true) |> Control |> dispatch
                                )
                            ]
                    ]
                ]
            ]
        ]

    let videoList (state: State) (dispatch: Msg -> unit) =
        match state.VideoFiles.Files |> Array.isEmpty with
        | true ->
            StackPanel.create [
                StackPanel.children [
                    Button.create [
                        Button.padding 10.0
                        Button.content "select vidoe files"
                        Button.onClick (fun _ -> dispatch SelectFiles)
                    ]
                ]
            ]
        | false ->
            StackPanel.create [
                StackPanel.children [
                    actionBar state dispatch
                    TextBlock.create [
                        TextBlock.margin 3.0
                    ]
                    if state.Task <> None then
                        ProgressBar.create [
                            ProgressBar.maximum 100.0
                            ProgressBar.minimum 0.0
                            ProgressBar.minHeight 3.0
                            ProgressBar.height 3.0
                            ProgressBar.value state.Task.Value.Progress
                        ]
                    TextBlock.create [
                        TextBlock.margin 10.0
                    ]
                    VideoFiles.view state.VideoFiles (VideoFiles >> dispatch)
                ]
            ]

    let view (state: State) (dispatch: Msg -> unit) =
        StackPanel.create [
            StackPanel.margin 20.0
            StackPanel.children [
                videoList state dispatch
            ]
        ]
