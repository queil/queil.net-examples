namespace This.Is.A.Namespace

type MyModule = {
  test: string
}

module MyModule =
  let myProperty = [fun (x:string) -> async {
    printfn "Invoked by: %s" x
  }]
