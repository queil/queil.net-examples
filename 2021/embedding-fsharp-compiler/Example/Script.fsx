#r "nuget: Yzl, 1.3.0"
#load "Script.Dependency.fsx"

namespace This.Is.A.Namespace

open Yzl.Core

// type HelloHost = {
//   test: string
// }

module HelloHost =

  let script = Yzl.map "script"
  let helloFrom = Yzl.str "helloFrom"
  let numberOfTheDay = Yzl.float "numberOfTheDay"

  let original (x:string)  = async {
    
      ! [
        script [
          helloFrom !|- 
            x
          numberOfTheDay (Maths.pi)
        ]
      ] |> Yzl.render |> printfn "%s"
  }
  
  let myFunc = original
