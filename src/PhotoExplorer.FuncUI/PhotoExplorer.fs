module PhotoExplorer
open System
open System.IO
open System.Threading
open Avalonia.Controls.Templates
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Core

let private imageView imageWidth image =       
    Border.create [ Border.cornerRadius 10
                    Border.clipToBounds true
                    Border.child
                        (Image.create
                            [ 
                                Image.width imageWidth
                                Image.height imageWidth
                                Image.source image
                                Image.stretch Stretch.UniformToFill ])]   

let driveView onSelectionChanged =
    Component.create("drive-view", fun ctx ->
        let store: IWritable<DriveInfo array> = ctx.useState Array.empty
        ctx.useEffect (
            handler = (fun _ -> DriveInfo.GetDrives() |> store.Set),
            triggers = [ EffectTrigger.AfterInit ]
        )

        let template =
            DataTemplateView<DriveInfo>.create (fun data -> TextBlock.create [ TextBlock.text data.Name ])

        ListBox.create
            [ 
                ListBox.dock Dock.Left
                ListBox.dataItems store.Current
                ListBox.itemTemplate template
                ListBox.onSelectedItemChanged onSelectionChanged ] :> IView)

let directoriesView name (parent : IReadable<string option>) onSelectionChanged =
    Component.create("directories-view", fun ctx ->
        let parent = ctx.usePassedRead parent
        let store: IWritable<string array> = ctx.useState Array.empty
        ctx.useEffect (
            handler = (fun _ ->
                printfn $"{name} {parent.Current}"
                match parent.Current with
                | Some path ->
                    let files = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly)
                    printfn $"{name} {files.Length}"
                    files
                | None -> Array.empty
                |> store.Set),
            triggers = [ EffectTrigger.AfterChange parent ]
        )

        let template =
            DataTemplateView<string>.create (fun data ->
                let name = Path.GetFileName data
                TextBlock.create [ TextBlock.text name ])

        ListBox.create
            [ 
                ListBox.dock Dock.Left
                ListBox.dataItems store.Current
                ListBox.itemTemplate template
                ListBox.onSelectedItemChanged onSelectionChanged ] :> IView)

let imageListView (parentPath : IReadable<string option>) =
    Component.create("image-list", fun ctx ->
        let imageWidth = 200             
        let imageTemplate data = imageView imageWidth data
        let photoStore : IWritable<Bitmap array> = ctx.useState Array.empty
        let op : IWritable<(Guid*int) option> = ctx.useState None
        let token : IWritable<CancellationTokenSource option> = ctx.useState None
        
        let resetToken () =
            let cts = new CancellationTokenSource()
            token.Set (Some cts)
            Console.WriteLine "Set token"
            cts.Token

        let resetGallery () =
            match token.Current with
            | None -> ()
            | Some token ->
                token.Cancel()
                Console.WriteLine "Cancel token"
            token.Set None
            op.Set None
            photoStore.Set Array.empty
        
        let callBack messages =
                match messages, op.Current with
                | (opId, _, bitmap), Some(currentOpId, _) when opId = currentOpId ->
                    photoStore.Current
                    |> Array.insertAt photoStore.Current.Length bitmap
                    |> photoStore.Set
                | _ -> ()
            
        let lastCallBack () = op.Set None
        
        let startLoadingPhotos (photos: string list) =
            let opId = Guid.NewGuid()       
            let newToken = resetToken ()
            op.Set (Some (opId, photos.Length))
            Async.Start (
                BitmapLoader.pipeline opId imageWidth callBack lastCallBack photos,
                newToken
            )
            
        ctx.useEffect (
            handler = ( fun _ ->
                            resetGallery ()
                            match parentPath.Current with
                            | None -> ()
                            | Some path ->
                                let photos = BitmapLoader.getPhotos path
                                if photos.Length > 0 then
                                        startLoadingPhotos photos
            ),
            triggers = [ EffectTrigger.AfterChange parentPath ]
           )
        
        DockPanel.create
            [ DockPanel.children
                [   match op.Current with
                    | Some(_, totalCount) ->
                        ProgressBar.create
                            [ ProgressBar.dock Dock.Top
                              ProgressBar.margin (10.0, 0.0)
                              ProgressBar.minimum 0
                              ProgressBar.maximum (totalCount |> double)
                              ProgressBar.value photoStore.Current.Length
                              ProgressBar.showProgressText true
                              ProgressBar.progressTextFormat "{}{0}/{3} Photos Complete ({1:0}%)" ]
                    | None -> ()
                    ListBox.create
                        [ 
                            ListBox.itemsPanel (FuncTemplate<Panel>(fun _ -> WrapPanel()))
                            ListBox.itemTemplate (DataTemplateView<Bitmap>.create imageTemplate)
                            ListBox.dataItems photoStore.Current
                            ListBox.dock Dock.Right ]]]
        )   
    
let view () =
    Component(fun ctx ->
        let drive: IWritable<DriveInfo option> = ctx.useState None
        let firstLevel: IWritable<string option> = ctx.useState None
        let secondLevel: IWritable<string option> = ctx.useState None
        let fileInfoList: IWritable<string array> = ctx.useState Array.empty
        let photoDirectoryPath: IWritable<string option> = ctx.useState None
        let driveName = drive.Map (fun driveInfoOption ->
            driveInfoOption
            |> Option.map _.Name)           
        
        let setFileList path =
             path |> Some |> photoDirectoryPath.Set              
              
        let fileTemplate =
            DataTemplateView<string>.create (fun data ->
                let name = Path.GetFileName data
                TextBlock.create [ TextBlock.text name ])
        
        let onDriveChanged (aDrive: obj) =
            (aDrive :?> DriveInfo) |> Option.ofObj |> drive.Set
            
        let onFirstLevelChanged (path: obj) =
            (path :?> string) |> Option.ofObj |> firstLevel.Set

        let onSecondLevelChanged (path: obj) =
            (path :?> string) |> Option.ofObj |> secondLevel.Set
        
        ctx.useEffect(
            handler = (fun _ ->
                drive.Current
                |> Option.map _.Name
                |> Option.iter(setFileList) 
            ),
            triggers = [ EffectTrigger.AfterChange drive ]
        )
        ctx.useEffect(
            handler = (fun _ ->
                firstLevel.Current
                |> Option.iter(setFileList) 
            ),
            triggers = [ EffectTrigger.AfterChange firstLevel ]
        )
        ctx.useEffect(
            handler = (fun _ ->
                secondLevel.Current
                |> Option.iter(setFileList) 
            ),
            triggers = [ EffectTrigger.AfterChange secondLevel ]
        )
        DockPanel.create
            [ DockPanel.children
                    [ 
                        driveView onDriveChanged
                        directoriesView "first" driveName onFirstLevelChanged
                        directoriesView "second" firstLevel onSecondLevelChanged
                        
                        ListBox.create
                            [ 
                                ListBox.dock Dock.Right
                                ListBox.dataItems fileInfoList.Current
                                ListBox.itemTemplate fileTemplate ]
                        
                        imageListView photoDirectoryPath ] ])
