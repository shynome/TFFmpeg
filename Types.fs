namespace TFFmpeg

module Types =

    type FileStatus =
        | Added
        | Ready
        | Pending
        | Finished of string
        | Error of string

    type MetaData = FFmpeg.NET.MetaData

    type FileItem =
        { Filepath: string
          Status: FileStatus
          Meta: MetaData option
          }

    type Control =
        | Cancel of string
        | Start of FileItem * bool
        | SetVideoFiles of string array
        | PerfectVideoFiles of string array
