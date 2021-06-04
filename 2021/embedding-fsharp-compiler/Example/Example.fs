namespace Example
open FSharp.Compiler.SourceCodeServices
open FSharp.DependencyManager.Nuget
open FSharp.Compiler.SyntaxTree
open System.IO
open System.Reflection
open FSharp.Compiler.Text


module Parser =
  
  let bindAsync (ra:Async<Result<'a,'b>>) (b:'a -> Async<Result<'c, 'b>>) =
    async {
      match! ra with
      | Ok a -> return! (b a)
      | Error x -> return (Error x)
    }
  
  let (>>=) = bindAsync

  module Types =
    type ScriptsFile = {
      path: string
      memberFqName: string
    }

    type Error = 
    | NuGetRestoreFailed of message: string
    | ScriptParseError of errors: string seq
    | ScriptCompileError of errors: string seq
    | ScriptModuleNotFound of path: string * moduleName: string
    | ScriptsPropertyHasInvalidType of path: string * propertyName: string
    | ScriptsPropertyNotFound of path: string * propertyName: string * foundProperties: string list
    | ExpectedMemberParentTypeNotFound of path: string * memberFqName: string
    | MultipleMemberParentTypeCandidatesFound of path: string * memberFqName: string

  [<RequireQualifiedAccess>]
  module Parser =
    open Types

    let readScripts<'a> (verbose:bool) (scripts:ScriptsFile): Async<Result<'a,Error>> =
      let checker = FSharpChecker.Create()
      let compileScripts (nugetResolutions:string seq) =
        async {

          let refs = nugetResolutions |> Seq.map (fun r ->
            let refName = Path.GetFileNameWithoutExtension(FileInfo(r).Name)
            $"--reference:{refName}")

          let libPaths = nugetResolutions |> Seq.map (fun r ->
            let libPath = FileInfo(r).DirectoryName
            $"--lib:{libPath}")

          nugetResolutions |> Seq.iter (fun r -> Assembly.LoadFrom r |> ignore)

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

          return
            match maybeAssembly with
            | Some x -> Ok x
            | None -> Error (ScriptCompileError (errors |> Seq.map (fun d -> d.ToString())))
        }

      let parse () : Async<Result<ParsedInput,Error>> =
        async {
          let parsingOptions = { FSharpParsingOptions.Default with SourceFiles = [| scripts.path |] }
          let source = File.ReadAllText scripts.path |> SourceText.ofString
          let! parsed = checker.ParseFile(scripts.path, source, parsingOptions)
          
          return
            match parsed with
            | x when x.ParseHadErrors -> Error(ScriptParseError (parsed.Errors |> Seq.map(fun d -> d.ToString())))
            | x ->
              if verbose then printfn "%A" parsed.ParseTree.Value
              Ok x.ParseTree.Value
        }

      let resolveNugets (parsed:ParsedInput) : Async<Result<string seq, Error>> =
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
                  | SynModuleDecl.HashDirective(ParsedHashDirective("r", args, _), _) -> Some (args)
                  | _ -> None)
              |> Seq.collect (id)
              |> Seq.map (fun d ->
                   let chunks = d.Split(": ")
                   (chunks.[0], chunks.[1]))
              |> Seq.toList

          let mgr = FSharpDependencyManager(None)

          let tfm = "netstandard2.0"
          let rid = "linux-x64"
          let extension = FileInfo(scripts.path).Extension
          let result = mgr.ResolveDependencies(extension, nugets, tfm, rid, 36000)
          let nugetResult = result :?> ResolveDependenciesResult
          return 
            match nugetResult with
            | x when x.Success -> Ok x.Resolutions
            | _ -> Error (NuGetRestoreFailed(nugetResult.StdOut |> String.concat "\n"))
        }

      let extract (assembly:Assembly): Result<'a,Error> =
      
        let name = scripts.memberFqName
        let (fqTypeName, memberName) =
          let splitIndex = name.LastIndexOf(".")
          name.[0..splitIndex - 1], name.[splitIndex + 1..]

        let candidates = assembly.GetTypes() |> Seq.where (fun t -> t.FullName = fqTypeName) |> Seq.toList       
        if verbose then assembly.GetTypes() |> Seq.iter (fun t ->  printfn "%s" t.FullName)

        match candidates with
        | [t] ->
          match t.GetProperty(memberName, BindingFlags.Static ||| BindingFlags.Public) with
          | null -> Error (ScriptsPropertyNotFound (
                            scripts.path, scripts.memberFqName,
                            t.GetProperties() |> Seq.map (fun p -> p.Name) |> Seq.toList))
          | p ->
            try
              Ok (p.GetValue(null) :?> 'a)
            with
            | :? System.InvalidCastException -> Error (ScriptsPropertyHasInvalidType (scripts.path, scripts.memberFqName))

        | [] -> Error (ExpectedMemberParentTypeNotFound (scripts.path, scripts.memberFqName))
        | _ -> Error (MultipleMemberParentTypeCandidatesFound (scripts.path, scripts.memberFqName))
      
      async {
        return! 
          parse () 
          >>= resolveNugets
          >>= compileScripts
          >>= (extract >> async.Return)
      }
