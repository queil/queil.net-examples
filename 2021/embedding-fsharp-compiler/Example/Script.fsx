#r "nuget: Yzl, 1.3.0"

namespace This.Is.A.Namespace

open Yzl.Core

// type HelloHost = {
//   test: string
// }

module HelloHost =

  let script = Yzl.map "script"
  let helloFrom = Yzl.str "helloFrom"

  let original (x:string)  = async {
    
      ! [
        script [
          helloFrom !|- 
            x
        ]
      ] |> Yzl.render |> printfn "%s"
  }
  
  let myFunc = original
