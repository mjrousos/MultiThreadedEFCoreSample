using System;
using System.Collections.Generic;

namespace SampleWebApi.Models
{
    public class Author
    {
        public int ID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public DateTimeOffset BirthDate { get; set; }
        public ICollection<Book> Books { get; set; }
    }
}