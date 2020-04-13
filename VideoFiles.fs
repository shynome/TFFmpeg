namespace TFFmpeg

module VideoFiles =

    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Components
    open Avalonia.Controls
    open Avalonia.Layout
    open Types
    open Elmish

    let toFileStatus file =
        { Filepath = file
          Status = Added
          Meta = None
          }

    type State =
        { Files: FileItem array
          Cursor: int }

    let init = {
        Files = [||] |> Array.map toFileStatus
        Cursor = 0
    }

    type Msg =
        | AddFiles of string array
        | Remove of string
        | Control of Control
        | UpdateVideoMeta of Map<string, MetaData option>

    let update (msg:Msg) (state:State): State * Cmd<Msg> * Control option =
        match msg with
        | UpdateVideoMeta (metas) ->
            let f =
                state.Files
                |> Array.map (fun f ->
                    match metas.TryGetValue(f.Filepath) with
                    | (true, meta) ->
                        let status =
                            if meta = None then Error("无法找到对应文件的视频信息")
                            else Ready
                        {f with Meta = meta; Status = status}
                    | _ -> f
                )
            { state with Files = f }, Cmd.none, None
        | AddFiles files ->
            let files = files |> Array.map toFileStatus
            let f =
                state.Files
                |> Array.append files
                |> Array.distinctBy (fun f -> f.Filepath)
            { state with Files = f }, Cmd.none, None
        | Remove file ->
            let f = state.Files |> Array.filter (fun f -> f.Filepath <> file)
            let cursor = state.Cursor
            let cursor =  if cursor > f.Length - 1 then 0 else cursor
            {state with Files = f; Cursor = cursor}, Cmd.none, None
        | Control c ->
            state, Cmd.none, Some(c)

    let view (state: State) (dispatch: Msg -> unit) =
        ListBox.create [
            ListBox.dataItems state.Files
            ListBox.selectionMode SelectionMode.Toggle
            ListBox.selectedIndex state.Cursor
            ListBox.maxHeight 300.0
            ListBox.itemTemplate(DataTemplateView<FileItem>.create(fun v ->
                let t = v.Filepath
                let t =
                    match v.Meta with
                    | Some meta ->
                        let d = meta.Duration.ToString()
                        let d = d.Split(".").[0]
                        t + (sprintf " duration: %s size: %s" d meta.VideoData.FrameSize )
                    | None -> t
                DockPanel.create [
                    DockPanel.margin 5.0
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text t
                                ]
                                match v.Status with
                                | Pending ->
                                    TextBlock.create [
                                        TextBlock.text "pending"
                                    ]
                                | Error e ->
                                    TextBlock.create [
                                        TextBlock.text e
                                    ]
                                | Finished outpath ->
                                    TextBlock.create [
                                        TextBlock.text (sprintf "Finished. outpath: %s" outpath)
                                    ]
                                | Ready ->
                                    TextBlock.create [
                                        TextBlock.text "Ready"
                                    ]
                                | Added ->
                                    TextBlock.create [
                                        TextBlock.text "Added"
                                    ]
                            ]
                        ]
                        DockPanel.create [
                            DockPanel.lastChildFill false
                            DockPanel.horizontalAlignment HorizontalAlignment.Right
                            DockPanel.margin 5.0
                            DockPanel.children [
                                if v.Status <> Pending then
                                    Button.create[
                                        Button.padding 5.0
                                        Button.content "remove"
                                        Button.onClick(fun e ->dispatch (Remove v.Filepath))
                                    ]
                                TextBlock.create [
                                    TextBox.width 10.0
                                ]
                                match v.Status with
                                | Pending ->
                                    Button.create[
                                        Button.padding 5.0
                                        Button.content "cancel"
                                        Button.onClick (fun _ -> Cancel v.Filepath |> Control |> dispatch)
                                    ]
                                | Added ->
                                    Button.create[
                                        Button.padding 5.0
                                        Button.content "get meta"
                                        Button.onClick (fun _ -> PerfectVideoFiles [|v.Filepath|] |> Control |> dispatch)
                                    ]
                                | _ ->
                                    Button.create[
                                        Button.padding 5.0
                                        Button.content "start"
                                        Button.onClick (fun _ -> Start (v, false) |> Control |> dispatch)
                                    ]
                            ]
                        ]
                    ]
            ]))
        ]
