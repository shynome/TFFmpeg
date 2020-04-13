namespace TFFmpeg

module Dialogs =
    open Avalonia.Controls
    open System.Collections.Generic
    let videoFilter =
        let f = FileDialogFilter()
        f.Extensions <- List(
            seq {
            "mp4"; "m4v"; // MP4
            "mov"; "qt" // MOV
            "avi" // AVI
            "flv" // FLV
            "wmv"; "asf" // WMV
            "mpeg"; "mpg"; "vob" // MPEG
            "mkv" // MKV
            "rm"; "rmvb" // RM/RMVB
            "vob" // VOB
            "ts"; // TS
            "dat"; // DAT
            }
        )
        f.Name <- "Video"
        f
    let allowAllFileFilter =
        let f = FileDialogFilter()
        f.Extensions <- List( seq { "*" })
        f.Name <- "All Files"
        f
    let openFileDialog () =
        let dialog = OpenFileDialog()
        dialog.Filters <- List(seq { videoFilter; allowAllFileFilter; })
        dialog.AllowMultiple <- true
        dialog.Title <- "Select Video Files"
        dialog
