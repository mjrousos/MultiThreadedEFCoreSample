using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SampleWebApi.Models
{
    public class Book
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public int YearPublished { get; set; }
        public string Summary { get; set; }
        public Author Author { get; set; }
    }
}
