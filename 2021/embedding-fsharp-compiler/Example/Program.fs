
open Example.CompilerHost
open Example.CompilerHost.Types

[<EntryPoint>]
let main argv =

  let result =
    {
      path = argv.[0]
      memberFqName = "This.Is.A.Namespace.HelloHost.myFunc"
    } |> CompilerHost.getScriptMember<string -> Async<unit>> false |> Async.RunSynchronously

  match result with
  | Ok func -> 
    func "The host!" |> Async.RunSynchronously
    0
  | Error e ->
    match e with
    | ScriptCompileError(errors) ->
      printfn "%s" "Script compilation failed:"
      errors |> Seq.iter (printfn "%s")
    | ScriptsPropertyNotFound(path, propertyName, foundProperties) ->
      printfn "%s in %s not found" propertyName path
      printfn "Found properties: "
      foundProperties |> Seq.iter (printfn "%s")
    | e -> printfn "Unhandled error: %A" e
    -1
