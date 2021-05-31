
open Example.Parser
open Example.Parser.Types

[<EntryPoint>]
let main argv =

  try  
    let funcToRun =
      {
        path = argv.[0]
        memberFqName = "This.Is.A.Namespace.MyModuleModule.myProperty"
      } |> Parser.readScripts<(string -> Async<unit>) list> true |> Async.RunSynchronously

    (funcToRun |> Seq.head) "I am host!" |> Async.RunSynchronously
    0
  with
  | Errors.ScriptsPropertyNotFound(path, propertyName, foundProperties) ->
    printfn "%s in %s not found" propertyName path
    printfn "Found properties: "
    foundProperties |> Seq.iter (printfn "%s")
    -1