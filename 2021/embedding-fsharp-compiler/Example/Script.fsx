namespace This.Is.A.Namespace

type MyModule = {
  test: string
}

module MyModule =
  let myProperty2 (x:string)  = async {
    printfn "Invoked by: %s" x
  }
  
  let myProperty = myProperty2
