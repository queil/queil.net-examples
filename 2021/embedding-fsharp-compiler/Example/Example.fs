namespace Example
open FSharp.Compiler.SourceCodeServices
open FSharp.DependencyManager.Nuget
open FSharp.Compiler.SyntaxTree
open System.IO
open System.Reflection
open FSharp.Compiler.Text


module Parser =
  
  module Types =
    type ScriptsFile = {
      path: string
      memberFqName: string
    }

    [<RequireQualifiedAccess>]
    module Errors = 
      exception NuGetRestoreFailed of message: string
      exception ScriptParseError of errors: string seq
      exception ScriptCompileError of errors: string seq
      exception ScriptModuleNotFound of path: string * moduleName: string
      exception ScriptsPropertyHasInvalidType of path: string * propertyName: string
      exception ScriptsPropertyNotFound of path: string * propertyName: string * foundProperties: string list
      exception ExpectedMemberParentTypeNotFound of path: string * memberFqName: string
      exception MultipleMemberParentTypeCandidatesFound of path: string * memberFqName: string

  [<RequireQualifiedAccess>]
  module Parser =
    open Types

    let readScripts<'a> (verbose:bool) (scripts:ScriptsFile): Async<'a> =
      let checker = FSharpChecker.Create()
      let compileScripts (fileAst:ParsedInput) (nugetResult:ResolveDependenciesResult) =
        async {

          if not nugetResult.Success then
            raise (Errors.NuGetRestoreFailed(nugetResult.StdOut |> String.concat "\n"))

          let refs = nugetResult.Resolutions |> Seq.map (fun r ->
            let refName = Path.GetFileNameWithoutExtension(FileInfo(r).Name)
            $"--reference:{refName}")

          let libPaths = nugetResult.Resolutions |> Seq.map (fun r ->
            let libPath = FileInfo(r).DirectoryName
            $"--lib:{libPath}")

          nugetResult.Resolutions |> Seq.iter (fun r -> Assembly.LoadFrom r |> ignore)

          let compilerArgs = [|
            "-a"; scripts.path
            "--targetprofile:netcore"
            "--target:module"
            yield! libPaths
            yield! refs
            sprintf "--reference:%s" (Assembly.GetEntryAssembly().GetName().Name)
            "--langversion:preview"
          |]

          if verbose then printfn "Compiler args: %s" (compilerArgs |> String.concat " ")

          let! errors, _, maybeAssembly =
            checker.CompileToDynamicAssembly (compilerArgs, None)
            // Not sure how to set targetprofile
            //checker.CompileToDynamicAssembly([fileAst], "Script", nugetResult.Resolutions |> Seq.toList, None, debug=true)

          return
            match maybeAssembly with
            | Some x -> x
            | None ->
              raise (Errors.ScriptCompileError (errors |> Seq.map (fun d -> d.ToString())))
        }

      let parse () : Async<ParsedInput> =
        async {
          let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| scripts.path |] }
          let source = File.ReadAllText scripts.path |> SourceText.ofString
          let! parsed = checker.ParseFile(scripts.path, source, parsingOptions)

          if parsed.ParseHadErrors then
            raise (Errors.ScriptParseError (parsed.Errors |> Seq.map(fun d -> d.ToString())))
          if verbose then printfn "%A" parsed.ParseTree.Value
          return (parsed.ParseTree.Value)
        }

      let resolveNugets (parsed:ParsedInput) : Async<ResolveDependenciesResult> =
        async {

          let fileAst =
            match parsed with
            | ParsedInput.ImplFile x -> x
            | ParsedInput.SigFile _ -> failwith "Sig fles not supported"

          let (ParsedImplFileInput(_, _, _, _, _, modules, _)) = fileAst
          let (SynModuleOrNamespace(_, _, _, moduleDeclarations, _, _, _, _)) = modules.[0]

          let nugets =
            moduleDeclarations
              |> Seq.choose (
                  function
                  | SynModuleDecl.HashDirective(ParsedHashDirective("r", args, range), _) as x -> Some (args)
                  | _ -> None)
              |> Seq.collect (id)
              |> Seq.map (fun d ->
                   let chunks = d.Split(": ")
                   (chunks.[0], chunks.[1]))
              |> Seq.toList

          let mgr = FSharpDependencyManager(None)

          let result = mgr.ResolveDependencies(FileInfo(scripts.path).Extension, nugets, "netstandard2.0", "linux-x64", 36000)
          return result :?> ResolveDependenciesResult
        }

      let getScripts (assembly:Assembly) =
      
        let fqNameChunks = scripts.memberFqName.Split(".") |> Seq.rev |> Seq.toList
        let (memberName, fqTypeName) =
          match fqNameChunks with
          | h::t -> (h, t |> Seq.rev |> String.concat ("."))
          | [] -> failwith "Empty name"

        let candidates = assembly.GetTypes() |> Seq.where (fun t -> t.FullName = fqTypeName) |> Seq.toList
        
        if verbose then assembly.GetTypes() |> Seq.iter (fun t ->  printfn "%s" t.FullName)

        let typ =
          match candidates with
          | [s] -> s
          | [] -> raise (Errors.ExpectedMemberParentTypeNotFound (scripts.path, scripts.memberFqName))
          | _ -> raise (Errors.MultipleMemberParentTypeCandidatesFound (scripts.path, scripts.memberFqName))

        try
          match typ.GetProperty(memberName, BindingFlags.Static ||| BindingFlags.Public) with
          | null -> 
            raise (Errors.ScriptsPropertyNotFound (
                    scripts.path, scripts.memberFqName,
                    typ.GetProperties() |> Seq.map (fun p -> p.Name) |> Seq.toList))
          | x -> x.GetValue(null) :?> 'a
        with
        | :? System.InvalidCastException -> raise (Errors.ScriptsPropertyHasInvalidType (scripts.path, scripts.memberFqName))

      async {
        let! parsed = parse ()
        let! result = resolveNugets parsed
        let! assembly = compileScripts parsed result
        return getScripts assembly
      }