[<RequireQualifiedAccess>]
module PhotoGallery

open System
open System.Threading
open Avalonia.Controls.Templates
open Avalonia.Media
open System.IO
open Elmish
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Media.Imaging
open Microsoft.FSharp.Core

type LoadingPhotoId = Guid
type PhotoPath = string
type FolderPath = string

type LoadOperationStatus =
    | WaitingForStart of FolderPath: FolderPath
    | Started of Id: LoadingPhotoId * TotalCount: int

type Model =
    { Photos: (PhotoPath * Bitmap) list
      LoadOperation: LoadOperationStatus option }

type Msg =
    | StartLoadingPhotos of FolderPath
    | LoadingStarted of (LoadingPhotoId * int)
    | PhotosLoaded of (LoadingPhotoId * (PhotoPath * Bitmap) array)
    | LoadingCompleted of LoadingPhotoId

let parallelPhotoProcessingCount = 5

let private readFileAsBitmap (path: PhotoPath) =
    use stream = File.OpenRead(path)
    let bitmap = Bitmap.DecodeToWidth(stream, 200, BitmapInterpolationMode.LowQuality)
    (path, bitmap)


let private mkDisposable f =
    { new IDisposable with
        member _.Dispose() = f () }

let runParallelRead operationId directory dispatch =

    let isAuthorize x =
        Seq.contains x [| ".jpg"; ".jpeg"; ".png"; ".gif" |]

    let files =
        Directory.EnumerateFiles(directory)
        |> Seq.where (fun x -> x |> Path.GetExtension |> isAuthorize)
        |> List.ofSeq

    match files with
    | [] ->
        LoadingCompleted operationId |> dispatch

        { new IDisposable with
            member this.Dispose() = () }
    | files ->
        LoadingStarted(operationId, files.Length) |> dispatch

        let pipeline =
            async {
                let! _ =
                    files
                    |> List.map PhotoPath
                    |> List.map (fun path ->
                        async {
                            use stream = File.OpenRead(path)
                            let bitmap = Bitmap.DecodeToWidth(stream, 200, BitmapInterpolationMode.LowQuality)
                            return (path, bitmap)
                        })
                    |> List.chunkBySize 15
                    |> List.map (fun chunk ->
                        async {
                            let! messages = Async.Parallel chunk
                            PhotosLoaded(operationId, messages) |> dispatch
                            do! Async.Sleep(150) // pause to keep UI responsive
                        })
                    |> Async.Sequential                    

                dispatch (LoadingCompleted operationId)
            }

        let cts = new CancellationTokenSource()
        let token = cts.Token

        Async.Start(
            pipeline,
            cancellationToken = token
        )

        mkDisposable (fun () ->
            cts.Cancel()
            cts.Dispose())

let subscribe (model: Model) =
    [ match model.LoadOperation with
      | Some operation ->
          let operationId, folderPath =
              match operation with
              | WaitingForStart path -> Guid.NewGuid(), path
              | Started(id, _) -> id, ""

          [ operationId |> string ], runParallelRead operationId folderPath

      | _ -> () ]

let init () = { Photos = []; LoadOperation = None }

let update msg model =

    let newModel =
        match msg, model.LoadOperation with
        | StartLoadingPhotos directory, _ ->
            { model with
                LoadOperation = directory |> LoadOperationStatus.WaitingForStart |> Some
                Photos = [] }

        | LoadingCompleted(operationId), Some(Started(currentId, _)) when operationId = currentId ->
            { model with LoadOperation = None }

        | LoadingCompleted(operationId), Some(Started(currentId, _)) when operationId <> currentId -> model

        | LoadingCompleted _, Some(WaitingForStart _) -> { model with LoadOperation = None }

        | LoadingStarted(operationId, total), Some(WaitingForStart _) ->
            { model with
                LoadOperation = (operationId, total) |> LoadOperationStatus.Started |> Some }

        | PhotosLoaded(operationId, array), Some(Started(currentId, _)) when operationId = currentId ->
            { model with
                Photos = model.Photos @ (array |> Array.toList) }

        | _ -> model

    newModel, Cmd.none

let private imageView source =
    let imageWidth = 200

    Border.create
        [ Border.cornerRadius 10
          Border.clipToBounds true
          Border.child (
              Image.create
                  [ Image.width imageWidth
                    Image.height imageWidth
                    Image.source source
                    Image.stretch Stretch.UniformToFill ]
          ) ]

let view model =
    DockPanel.create
        [ DockPanel.children
              [ match model.LoadOperation with
                | Some(Started(_, totalCount)) ->
                    ProgressBar.create
                        [ ProgressBar.dock Dock.Top
                          ProgressBar.margin (10.0, 0.0)
                          ProgressBar.minimum 0
                          ProgressBar.maximum (totalCount |> double)
                          ProgressBar.value model.Photos.Length
                          ProgressBar.showProgressText true
                          ProgressBar.progressTextFormat "{}{0}/{3} Photos Complete ({1:0}%)" ]
                | _ -> ()
                ListBox.create
                    [ ListBox.itemsPanel (FuncTemplate<Panel>(fun _ -> WrapPanel()))
                      ListBox.itemTemplate (DataTemplateView<Bitmap>.create imageView)
                      ListBox.dataItems (model.Photos |> List.map snd)
                      ListBox.dock Dock.Right ] ] ]
