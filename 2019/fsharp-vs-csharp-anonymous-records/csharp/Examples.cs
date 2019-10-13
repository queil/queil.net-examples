using System;
using System.Linq;

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
    }
}
