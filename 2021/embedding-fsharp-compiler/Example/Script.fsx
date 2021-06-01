namespace This.Is.A.Namespace

type HelloHost = {
  test: string
}

module HelloHost =
  let original (x:string)  = async {
    printfn "Invoked by: %s" x
  }
  
  let myFunc = original
