namespace Example
open FSharp.Compiler.SourceCodeServices
open System.IO
open System.Reflection
open FSharp.Compiler.Text

module CompilerHost =
  
  let bindAsync (ra:Async<Result<'a,'b>>) (b:'a -> Async<Result<'c, 'b>>) =
    async {
      match! ra with
      | Ok a -> return! (b a)
      | Error x -> return (Error x)
    }
  
  let (>>=) = bindAsync

  module Types =
    type ScriptInput = {
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
  module CompilerHost =
    open Types

    let getScriptMember<'a> (verbose:bool) (script:ScriptInput): Async<Result<'a,Error>> =
      let checker = FSharpChecker.Create()
      let compileScripts (nugetResolutions:string seq) =
        async {

          let refs = nugetResolutions |> Seq.map (sprintf "-r:%s")
          nugetResolutions |> Seq.iter (Assembly.LoadFrom >> ignore)

          let compilerArgs = [|
            "-a"; script.path
            "--targetprofile:netcore"
            "--target:module"
            yield! refs
            sprintf "-r:%s" (Assembly.GetEntryAssembly().GetName().Name)
            "--langversion:preview"
          |]

          if verbose then printfn "Compiler args: %s" (compilerArgs |> String.concat " ")

          let! errors, _, maybeAssembly =
            checker.CompileToDynamicAssembly(compilerArgs, None)
          
          return
            match maybeAssembly with
            | Some x -> Ok x
            | None -> Error (ScriptCompileError (errors |> Seq.map (fun d -> d.ToString())))
        }

      let resolveNugets () =
        async {
          let source = File.ReadAllText script.path |> SourceText.ofString
          let! projOptions, errors = checker.GetProjectOptionsFromScript(script.path, source)

          match errors with
          | [] -> 
            let! projResults = checker.ParseAndCheckProject(projOptions)
            return
              match projResults.HasCriticalErrors with
              | false -> 
                projResults.DependencyFiles 
                  |> Seq.choose(
                    function
                    | path when path.EndsWith(".dll") -> Some path
                    | _ -> None)
                  |> Seq.groupBy id
                  |> Seq.map (fun (path, _) -> path)
                  |> Ok
              | _ -> Error (ScriptParseError (projResults.Errors |> Seq.map string) )
          | _ -> return Error (ScriptParseError (errors |> Seq.map string) )
        }

      let extract (assembly:Assembly): Result<'a,Error> =
      
        let name = script.memberFqName
        let (fqTypeName, memberName) =
          let splitIndex = name.LastIndexOf(".")
          name.[0..splitIndex - 1], name.[splitIndex + 1..]

        let candidates = assembly.GetTypes() |> Seq.where (fun t -> t.FullName = fqTypeName) |> Seq.toList       
        if verbose then assembly.GetTypes() |> Seq.iter (fun t ->  printfn "%s" t.FullName)

        match candidates with
        | [t] ->
          match t.GetProperty(memberName, BindingFlags.Static ||| BindingFlags.Public) with
          | null -> Error (ScriptsPropertyNotFound (
                            script.path, script.memberFqName,
                            t.GetProperties() |> Seq.map (fun p -> p.Name) |> Seq.toList))
          | p ->
            try
              Ok (p.GetValue(null) :?> 'a)
            with
            | :? System.InvalidCastException -> Error (ScriptsPropertyHasInvalidType (script.path, script.memberFqName))

        | [] -> Error (ExpectedMemberParentTypeNotFound (script.path, script.memberFqName))
        | _ -> Error (MultipleMemberParentTypeCandidatesFound (script.path, script.memberFqName))
      
      async {
        return! 
          resolveNugets () 
          >>= compileScripts
          >>= (extract >> async.Return)
      }
