using System;
using System.Linq;
using System.Text.Json;

namespace csharp
{
    public static class Examples
    {
        class Person
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public DateTime DateOfBirth { get; set; }
        }

        public static void Projection()
        {
            var persons = new[]
            {
                new Person{ FirstName="Alice", LastName="Smith", DateOfBirth = new DateTime(2000,12,12) },
                new Person{ FirstName="Bob", LastName="Green", DateOfBirth = new DateTime(2001,10,10) }
            };

            var names = from p in persons
                        select new { Name = p.FirstName };

            foreach (var n in names) Console.WriteLine(n);
        }

        ///
        /// This would require a custom converter to insantiate an anonymous type 
        /// A similar one that is in FSharp example
        public static void Deserialization()
        {
            var input = @"
                {
                    ""success"": true,
                    ""message"" : ""Processed!"",
                    ""code"" : 0,
                    ""id"": ""89e8f9a1-fedb-440e-a596-e4277283fbcf""
                }";

            T Deserialize<T>(T template) => JsonSerializer.Deserialize<T>(input);

            var result = Deserialize(template: new { success = false, id = Guid.Empty });

            if (result.success) Console.WriteLine(result.id);
            else throw new Exception("Error");
        }

        public static void CopyAndUpdate()
        {
            var dob = new DateTime(2000, 12, 12);
            var data = new { FirstName = "Alice", LastName = "Smith", DateOfBirth = dob };
            Console.WriteLine(new { data.FirstName, LastName = "Jones", data.DateOfBirth });
        }
    }
}
