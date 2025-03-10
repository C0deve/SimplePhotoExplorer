module Core
    open System.IO
    open Avalonia.Media.Imaging
   
    module BitmapLoader =
        let isAuthorize x =
            Seq.contains x [| ".jpg"; ".jpeg"; ".png"; ".gif" |]
       
        let getPhotos directory =
            Directory.EnumerateFiles(directory)
            |> Seq.where (fun x -> x |> Path.GetExtension |> isAuthorize)
            |> Seq.toList
                
        let pipeline opId width callBack lastCallBack photos=
            async {
                let! _ =
                    photos
                    |> Seq.map (fun path ->
                        async {
                            use stream = File.OpenRead(path)
                            let bitmap = Bitmap.DecodeToWidth(stream, width, BitmapInterpolationMode.LowQuality)
                            callBack (opId, path, bitmap)
                        })                    
                    |> Async.Parallel
                lastCallBack ()
                ()
            }
    