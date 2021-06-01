namespace This.Is.A.Namespace

type MyModule = {
  test: string
}

module MyModule =
  let original (x:string)  = async {
    printfn "Invoked by: %s" x
  }
  
  let myFunc = original
