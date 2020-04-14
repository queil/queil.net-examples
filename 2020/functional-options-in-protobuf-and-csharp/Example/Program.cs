using System;
using System.Collections.Generic;

namespace Example
{
    public class Program
    {
        private static Dictionary<int,int> Store = new Dictionary<int, int>
        {
            [1] = 236429,
            [293] = 333
        };
        public static void Main(string[] args)
        {
            static Response GetBy(int key) 
             => Store.ContainsKey(key) ?
                   new Response{ Some = new MySecret{Number = Store[key]}}
                 : new Response();

            static void Handle(int key, Response response) 
            {
                if (response is { Some: MySecret s} )
                  Console.WriteLine($"My secret number for key {key} is {s.Number}");
                else 
                  Console.WriteLine($"No secret number found for key {key}!");
            }

            static void Test(int key) => Handle(key, GetBy(key));

            Test(1);
            Test(293);
            Test(100000000);
        }
    }
}
