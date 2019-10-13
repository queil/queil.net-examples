namespace fsharp

open System

module Examples =
    type Person = {
        FirstName : string
        LastName : string
        DateOfBirth : DateTime 
    }

    let Projection () =
        let persons = 
            [|
                {FirstName = "Alice"; LastName = "Smith"; DateOfBirth = DateTime(2000,12,12)}
                {FirstName = "Bob"; LastName = "Green"; DateOfBirth = DateTime(2001,10,10)}
            |]

        let names = 
            query { 
                for p in persons do
                select {|Name = p.FirstName|}
            }

        names |> Seq.iter (printfn "%A")
