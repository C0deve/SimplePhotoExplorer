[<RequireQualifiedAccess>]
module MainView

open System.IO
open Elmish
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

type Model =
    { PhotoGallery: PhotoGallery.Model
      Drives: DriveInfo list
      SelectedDrive: DriveInfo option
      Directories: string list
      SelectedDirectory: string option
      SubDirectories: string list
      SelectedSubDirectory: string option }

type Msg =
    | PhotoGalleryMsg of PhotoGallery.Msg
    | LoadDrives
    | DriveSelected of DriveInfo
    | DirectorySelected of string
    | SubDirectorySelected of string

let private init () =
    { PhotoGallery = PhotoGallery.init ()
      Drives = []
      SelectedDrive = None
      Directories = []
      SelectedDirectory = None
      SubDirectories = []
      SelectedSubDirectory = None },
    Cmd.ofMsg LoadDrives

let private getDirectories path =
    try
        Directory.GetDirectories(path) |> Array.toList
    with _ ->
        []

let private update msg model =

    match msg with
    | PhotoGalleryMsg msg ->
        let mPhotoGallery, cmd = PhotoGallery.update msg model.PhotoGallery

        { model with
            PhotoGallery = mPhotoGallery },
        Cmd.map PhotoGalleryMsg cmd

    | LoadDrives ->
        let drives = DriveInfo.GetDrives() |> Array.toList
        { model with Drives = drives }, Cmd.none

    | DriveSelected drive ->
        { model with
            SelectedDrive = Some drive
            Directories = getDirectories drive.RootDirectory.FullName
            SelectedDirectory = None
            SubDirectories = []
            SelectedSubDirectory = None },
        (PhotoGallery.Msg.StartLoadingPhotos drive.Name) |> PhotoGalleryMsg |> Cmd.ofMsg

    | DirectorySelected dir ->
        { model with
            SelectedDirectory = Some dir
            SubDirectories = getDirectories dir
            SelectedSubDirectory = None },
        (PhotoGallery.Msg.StartLoadingPhotos dir) |> PhotoGalleryMsg |> Cmd.ofMsg

    | SubDirectorySelected dir ->
        { model with
            SelectedSubDirectory = Some dir },
        (PhotoGallery.Msg.StartLoadingPhotos dir) |> PhotoGalleryMsg |> Cmd.ofMsg

let private view model dispatch =
    DockPanel.create
        [ DockPanel.children
              [ ListBox.create
                    [ ListBox.dock Dock.Left
                      ListBox.dataItems model.Drives
                      ListBox.itemTemplate (
                          DataTemplateView<DriveInfo>.create (fun drive ->
                              TextBlock.create [ TextBlock.text drive.Name ])
                      )
                      ListBox.onSelectedItemChanged (fun drive ->
                          match drive with
                          | null -> ()
                          | _ -> dispatch (DriveSelected(drive :?> DriveInfo))) ]
                ListBox.create
                    [ ListBox.dock Dock.Left
                      ListBox.dataItems model.Directories
                      ListBox.itemTemplate (
                          DataTemplateView<string>.create (fun dir ->
                              TextBlock.create [ TextBlock.text (Path.GetFileName dir) ])
                      )
                      ListBox.onSelectedItemChanged (fun dir ->
                          match dir with
                          | null -> ()
                          | _ -> dispatch (DirectorySelected(dir :?> string))) ]
                ListBox.create
                    [ ListBox.dock Dock.Left
                      ListBox.dataItems model.SubDirectories
                      ListBox.itemTemplate (
                          DataTemplateView<string>.create (fun dir ->
                              TextBlock.create [ TextBlock.text (Path.GetFileName dir) ])
                      )
                      ListBox.onSelectedItemChanged (fun dir ->
                          match dir with
                          | null -> ()
                          | _ -> dispatch (SubDirectorySelected(dir :?> string))) ]

                PhotoGallery.view model.PhotoGallery ] ]

let subscribe model =
    Sub.map "photoGallery" PhotoGalleryMsg (PhotoGallery.subscribe model.PhotoGallery)

let program =
    Program.mkProgram init update view |> Program.withSubscription subscribe
