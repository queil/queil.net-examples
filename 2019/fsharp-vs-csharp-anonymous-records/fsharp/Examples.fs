namespace fsharp

open System
open System.Text.Json
open System.Text.Json.Serialization

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
    
    let Deserialization () =
        let input = 
            """
                {
                    "success": true,
                    "message" : "Processed!",
                    "code" : 0,
                    "id": "89e8f9a1-fedb-440e-a596-e4277283fbcf"

                }
            """
        let opts = JsonSerializerOptions()
        opts.Converters.Add(JsonFSharpConverter())   

        let result = JsonSerializer.Deserialize<{|success:bool; id:Guid|}>(input, opts)
        if result.success then printfn "%A" (result.id)
        else failwith "Error"

    let CopyAndUpdate () =
        let dob = DateTime(2000, 12, 12)
        let data = {| FirstName = "Alice"; LastName = "Smith"; DateOfBirth = dob |}
        printfn "%A" {| data with LastName = "Jones" |}
    
    let StructuralEquality () =
        
        let dob = DateTime(2000, 12, 12)
        let r1 = {| FirstName = "Alice"; LastName = "Smith"; DateOfBirth = dob |}
        let r2 = {| FirstName = "Alice"; LastName = "Smith"; DateOfBirth = dob |}
        printfn "Referential equality: obj.ReferenceEquals(%s, %s) : %b" "r1" "r2" (obj.ReferenceEquals(r1, r2))
        printfn "Structural equality: %s = %s : %b" "r1" "r2" (r1 = r2)
       
