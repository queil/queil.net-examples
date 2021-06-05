namespace Example
open FSharp.Compiler.SourceCodeServices
open FSharp.DependencyManager.Nuget
open FSharp.Compiler.SyntaxTree
open System.IO
open System.Reflection
open FSharp.Compiler.Text
open System.Text.RegularExpressions
open FSharp.Compiler.SourceCodeServices


module Parser =
  
  let bindAsync (ra:Async<Result<'a,'b>>) (b:'a -> Async<Result<'c, 'b>>) =
    async {
      match! ra with
      | Ok a -> return! (b a)
      | Error x -> return (Error x)
    }
  
  let (>>=) = bindAsync

  let (|ParseRegex|_|) regex str =
    let m = Regex(regex).Match(str)
    if m.Success
    then Some (List.tail [ for x in m.Groups -> x.Value ])
    else None


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
      let compileScripts (checkResult:FSharpCheckProjectResults) =
        async {
          let refs = 
            checkResult.DependencyFiles 
              |> Seq.choose(
                function
                | path when path.EndsWith(".dll") -> Some path
                | _ -> None)
              |> Seq.groupBy (id)
              |> Seq.map(fun (dllPath,_) -> $"-r:{dllPath}")

          
          // let refs = nugetResolutions |> Seq.map (fun r ->
          //   let refName = Path.GetFileNameWithoutExtension(FileInfo(r).Name)
          //   $"--reference:{refName}")

          // let libPaths = nugetResolutions |> Seq.map (fun r ->
          //   let libPath = FileInfo(r).DirectoryName
          //   $"--lib:{libPath}")

          // nugetResolutions |> Seq.iter (fun r -> Assembly.LoadFrom r |> ignore)

          let compilerArgs = [|
            "-a"; scripts.path
            "--targetprofile:netcore"
            "--target:module"
            yield! refs
            sprintf "--reference:%s" (Assembly.GetEntryAssembly().GetName().Name)
            "--langversion:preview"
          |]

         
          //let sysDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
          
          // let assemblies = checked2.DependencyFiles//GetReferencedAssemblies() |> Seq.choose (fun x -> x.FileName) |> Seq.toList

          // let compilerArgs = [|
          //   "-a"; scripts.path
          //   sprintf "--reference:%s" (Assembly.GetEntryAssembly().GetName().Name)
          //   "--langversion:preview"
          //   "--targetprofile:netcore"
          //   "--target:module"
          //   yield! (projOptions.OtherOptions 
          //           |> Seq.choose(
          //               function
          //               | "--noframework" -> None
          //               | x -> Some x
          //   ))
          // |]
          if verbose then printfn "Compiler args: %s" (compilerArgs |> String.concat " ")


          let! errors, _, maybeAssembly =
            checker.CompileToDynamicAssembly(compilerArgs, None)
            // checker.CompileToDynamicAssembly([parsed.ParseTree.Value], 
            //   "x" , 
            //   assemblies,
            //   None,
            //   debug=false
            //   )
          
          return
            match maybeAssembly with
            | Some x -> Ok x
            | None -> Error (ScriptCompileError (errors |> Seq.map (fun d -> d.ToString())))
        }

      let parse () =
        async {
          
          let source = File.ReadAllText scripts.path |> SourceText.ofString
          let flags = //[||] 
            [|

            |]
          
          let! projOptions, errors =
            checker.GetProjectOptionsFromScript(scripts.path, source, otherFlags=flags)

          // let projOptions = {
          //   projOptions with
          //     UseScriptResolutionRules = true
              
              
          // }

          let! projResults =
                checker.ParseAndCheckProject(projOptions)

          return Ok projResults
          // let! parsed, checkFileResults =
          //   checker.ParseAndCheckFileInProject(scripts.path, 0, source, projOptions)
          // return
          //   match checkFileResults with
          //   | FSharpCheckFileAnswer.Succeeded(res) -> 
          //       match parsed with
          //       | x when x.ParseHadErrors -> Error(ScriptParseError (parsed.Errors |> Seq.map(fun d -> d.ToString())))
          //       | x ->
          //         if verbose then printfn "%A" parsed
          //         Ok (x, res)
          //   | res -> failwithf "Parsing did not finish... (%A)" res
        }

      // let resolveNugets (parsed:ParsedInput) : Async<Result<string seq, Error>> =
      //   async {

      //     let fileAst =
      //       match parsed with
      //       | ParsedInput.ImplFile x -> x
      //       | ParsedInput.SigFile _ -> failwith "Sig fles not supported"

      //     let (ParsedImplFileInput(_, _, _, _, topLevelDirectives, modules, _)) = fileAst
      //     let (SynModuleOrNamespace(_, _, _, moduleDeclarations, _, _, _, _)) = modules.[0]

      //     let nugets =
      //       moduleDeclarations
      //         |> Seq.choose (
      //             function
      //             | SynModuleDecl.HashDirective(ParsedHashDirective("r", _, _) as ``#r``, _) -> Some (``#r``)
      //             | _ -> None)
      //         |> Seq.append (topLevelDirectives |> Seq.ofList)
      //         |> Seq.choose (
      //           function
      //           | ParsedHashDirective("r", [body], _) ->
      //               match body with
      //               | ParseRegex "(nuget):\s+(.*)" [nuget; pkg] -> Some (nuget, pkg)
      //               | _ -> failwith "Only NuGet is supported so far"
      //           | _ -> None)
      //         |> Seq.toList

      //     let mgr = FSharpDependencyManager(None)

      //     let tfm = "netstandard2.0"
      //     let rid = "linux-x64"
      //     let extension = FileInfo(scripts.path).Extension
      //     let result = mgr.ResolveDependencies(extension, nugets, tfm, rid, 36000)
      //     let nugetResult = result :?> ResolveDependenciesResult
      //     return 
      //       match nugetResult with
      //       | x when x.Success -> Ok x.Resolutions
      //       | _ -> Error (NuGetRestoreFailed(nugetResult.StdOut |> String.concat "\n"))
      //   }

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
          //>>= resolveNugets
          >>= compileScripts
          >>= (extract >> async.Return)
      }
