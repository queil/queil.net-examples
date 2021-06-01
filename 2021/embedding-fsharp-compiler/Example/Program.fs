
open Example.Parser
open Example.Parser.Types

[<EntryPoint>]
let main argv =

  try  
    let funcToRun =
      {
        path = argv.[0]
        memberFqName = "This.Is.A.Namespace.HelloHost.myFunc"
      } |> Parser.readScripts<string -> Async<unit>> true |> Async.RunSynchronously

    funcToRun "The host!" |> Async.RunSynchronously
    0
  with
  | Errors.ScriptCompileError(errors) ->
    printfn "%s" "Script compilation failed:"
    errors |> Seq.iter (printfn "%s")
    -1
  | Errors.ScriptsPropertyNotFound(path, propertyName, foundProperties) ->
    printfn "%s in %s not found" propertyName path
    printfn "Found properties: "
    foundProperties |> Seq.iter (printfn "%s")
    -1
