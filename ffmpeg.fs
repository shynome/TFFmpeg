namespace TFFmpeg

module ffmpeg =
    open FSharp.Control
    open System.IO
    open FFmpeg.NET
    open System.Threading
    open FSharp.Control.Reactive
    open Types
    open System.Security

    let private internalFFmpegPath =
        let basedir = System.AppDomain.CurrentDomain.BaseDirectory + "ffmpeg-bin/"
        match Directory.Exists(basedir) with
        | false -> basedir
        | true ->
            let dirs = Directory.GetDirectories(basedir, "*")
            // let bin = dirs.[0] + "/ffmpeg"
            // System.Diagnostics.Process.Start("chmod", "+x " + bin) |> ignore
            let dir = dirs.[0]
            dir

    let getFFmpegPath() =
        let dirs =
            seq {
                yield internalFFmpegPath
                yield (Directory.GetCurrentDirectory())
            }

        let dir = dirs |> Seq.tryFind (fun dir -> File.Exists(Path.Combine(dir, "ffmpeg")))
        match dir with
        | None -> None
        | Some dir -> Path.Combine(dir, "ffmpeg") |> Some

    let makeScaleArgs (width, height) = sprintf " -vf scale=%i:%i" width height

    let getOutpath f =
        let filename = Path.GetFileName((string) f)
        let dir = Path.GetDirectoryName(string (f))
        let outdir = Path.Combine(dir, "t-video-out")
        Directory.CreateDirectory outdir |> ignore
        Path.Combine(outdir, filename)

    type Transformer() =
        let start = Subject<string>.broadcast
        let progress = Subject<string * double>.broadcast
        let error = Subject<string * string>.broadcast
        let finished = Subject<string * string>.broadcast
        member this.Start = start
        member this.Progress = progress
        member this.Error = error
        member this.Finished = finished

        member this.GetFFmpeg() =
            let bin =
                match getFFmpegPath() with
                | None -> ""
                | Some bin -> bin

            let f = Engine bin
            f

        member private this.GetVideoMetadata(inputfile: string) =
            async {
                let f = this.GetFFmpeg()
                let! a = MediaFile inputfile
                         |> f.GetMetaDataAsync
                         |> Async.AwaitTask
                let a =
                    if isNull a then None else Some(a)
                return a
            }

        member this.GetVideosMetadata(files: string array) =
            async {
                let! metas = files
                             |> Array.map this.GetVideoMetadata
                             |> Async.Parallel
                return Map(Array.zip files metas)
            }

        member this.TransformVideo (item: FileItem) (args: string) =
            let f = this.GetFFmpeg()
            let filepath = item.Filepath
            let outpath = getOutpath filepath
            let mutable cmdArgs = "-pix_fmt yuv420p"
            if args.Length > 0 then cmdArgs <- cmdArgs + args
            let cmd = sprintf "-i %s %s %s" filepath cmdArgs outpath
            f.Complete.Add(fun e -> this.Finished.OnNext(filepath, outpath))
            f.Error.Add(fun e -> this.Error.OnNext(filepath, e.Exception.ToString()))
            f.Progress.Add(fun e ->
                let percentage =
                    match item.Meta with
                    | Some meta -> e.ProcessedDuration / meta.Duration
                    | None -> 0.0

                let percentage = percentage * 100.0
                let percentage = System.Math.Round(percentage, 2)
                this.Progress.OnNext(filepath, percentage |> double))
            let t = new CancellationTokenSource()
            f.ExecuteAsync(cmd, t.Token) |> ignore
            t
