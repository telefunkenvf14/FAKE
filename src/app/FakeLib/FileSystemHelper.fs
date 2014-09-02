﻿[<AutoOpen>]
/// Contains helpers which allow to interact with the file system.
module Fake.FileSystemHelper

open System
open System.Text
open System.IO
open System.Runtime.InteropServices

module Interop =

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetFullPathNameW")>]
    extern uint32 GetFullPathName(string lpFileName, uint32 nBufferLength, [<Out>] StringBuilder lpBuffer, IntPtr lpFilePart);

    [<DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetLongPathNameW")>]
    extern int GetLongPathName(string lpszShortPath,  StringBuilder lpszLongPath,  uint32 cchBuffer);

    [<DllImport("kernel32.dll")>]
    extern int GetShortPathName(string lpszShortPath,  StringBuilder lpszLongPath,  int cchBuffer);

/// Creates a DirectoryInfo for the given path.
let inline directoryInfo path = new DirectoryInfo(path)

/// Creates a FileInfo for the given path.
let inline fileInfo path = new FileInfo(path)

/// Creates a FileInfo or a DirectoryInfo for the given path
let inline fileSystemInfo path : FileSystemInfo = 
    if Directory.Exists path then upcast directoryInfo path
    else upcast fileInfo path

/// Converts a filename to it's full file system name.
let inline FullName fileName = Path.GetFullPath fileName

///Allows file paths longer than MAX_PATH (260 chars) but has a whole load of caveats
/// see here (http://blogs.msdn.com/b/bclteam/archive/2007/02/13/long-paths-in-net-part-1-of-3-kim-hamilton.aspx)
let inline FullNameLong fileName = 
    
    let fullPath = new StringBuilder(32768)
    let longPath = new StringBuilder(260)
    let size = (Interop.GetFullPathName(@"\\?\" + fileName, 32768u, fullPath, IntPtr.Zero))
    printfn "%A %s" size (fullPath.ToString())
    let size = Interop.GetLongPathName(fullPath.ToString(), longPath, 32768u)
    printfn "%A" size
    longPath.ToString()

/// Gets the directory part of a filename.
let inline DirectoryName fileName = Path.GetDirectoryName fileName

/// Gets all subdirectories of a given directory.
let inline subDirectories (dir : DirectoryInfo) = dir.GetDirectories()

/// Gets all files in the directory.
let inline filesInDir (dir : DirectoryInfo) = dir.GetFiles()

/// Finds all the files in the directory matching the search pattern.
let filesInDirMatching pattern (dir : DirectoryInfo) = 
    if dir.Exists then dir.GetFiles pattern
    else [||]

/// Gets the first file in the directory matching the search pattern as an option value.
let TryFindFirstMatchingFile pattern dir = 
    dir
    |> directoryInfo
    |> filesInDirMatching pattern
    |> fun files -> 
        if Seq.isEmpty files then None
        else (Seq.head files).FullName |> Some

/// Gets the first file in the directory matching the search pattern or throws an error if nothing was found.
let FindFirstMatchingFile pattern dir = 
    match TryFindFirstMatchingFile pattern dir with
    | Some x -> x
    | None -> new FileNotFoundException(sprintf "Could not find file matching %s in %s" pattern dir) |> raise

/// Gets the current directory.
let currentDirectory = Path.GetFullPath "."

/// Get the full location of the current assembly.
let fullAssemblyPath = System.Reflection.Assembly.GetAssembly(typeof<RegistryBaseKey>).Location

/// Checks if the file exists on disk.
let fileExists fileName = File.Exists fileName

/// Raises an exception if the file doesn't exist on disk.
let checkFileExists fileName = 
    if not <| fileExists fileName then new FileNotFoundException(sprintf "File %s does not exist." fileName) |> raise

/// Checks if all given files exist.
let allFilesExist files = Seq.forall fileExists files

/// Normalizes a filename.
let rec normalizeFileName (fileName : string) = 
    fileName.Replace("\\", Path.DirectorySeparatorChar.ToString()).Replace("/", Path.DirectorySeparatorChar.ToString())
            .TrimEnd(Path.DirectorySeparatorChar).ToLower()

/// Checks if dir1 is a subfolder of dir2. If dir1 equals dir2 the function returns also true.
let rec isSubfolderOf (dir2 : DirectoryInfo) (dir1 : DirectoryInfo) = 
    if normalizeFileName dir1.FullName = normalizeFileName dir2.FullName then true
    else if dir1.Parent = null then false
    else dir1.Parent |> isSubfolderOf dir2

/// Checks if the file is in a subfolder of the dir.
let isInFolder (dir : DirectoryInfo) (fileInfo : FileInfo) = isSubfolderOf dir fileInfo.Directory

/// Checks if the directory exists on disk.
let directoryExists dir = Directory.Exists dir

/// Ensure that directory chain exists. Create necessary directories if necessary.
let inline ensureDirExists (dir : DirectoryInfo) = 
    if not dir.Exists then dir.Create()

/// Checks if the given directory exists. If not then this functions creates the directory.
let inline ensureDirectory dir = directoryInfo dir |> ensureDirExists

/// Detects whether the given path is a directory.
let isDirectory path = 
    let attr = File.GetAttributes path
    attr &&& FileAttributes.Directory = FileAttributes.Directory

/// Detects whether the given path is a file.
let isFile path = isDirectory path |> not
