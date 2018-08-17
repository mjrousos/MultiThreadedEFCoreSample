using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SampleWebApi.Data;
using SampleWebApi.Models;
using SampleWebApi.Services;

namespace SampleWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly BookContext _context;
        private readonly NameService _nameService;
        private readonly ILogger<BooksController> _logger;

        public BooksController(BookContext context, NameService nameService, ILogger<BooksController> logger)
        {
            _context = context;
            _nameService = nameService;
            _logger = logger;
        }

        // GET api/values
        [HttpGet("count")]
        public ActionResult<int> Get()
        {
            return Ok(_context.Books.Count());
        }

        // GET api/books/5
        [HttpGet("{count}")]
        public async Task<ActionResult<IEnumerable<Book>>> Get([FromRoute] int count)
        {
            ConcurrentBag<Book> books = new ConcurrentBag<Book>();
            Random numGen = new Random((int)DateTime.Now.Ticks);

            var bookCount = _context.Books.Count();

            for (int i = 0; i < count; i++)
            {
                var id = numGen.Next(bookCount);
                var book = await _context.Books
                    .Include(b => b.Author)
                    .SingleOrDefaultAsync(b => b.ID == id);

                // To prevent trying to deserialize a loop, use this poor-man's DTO
                book.Author = new Author
                {
                    LastName = book.Author.LastName,
                    FirstName = book.Author.FirstName
                };

                books.Add(book);
            }

            return Ok(books.ToArray());
        }

        // POST api/books/{count}
        [HttpPost("{count}")]
        public async Task<ActionResult> Post([FromRoute] int count)
        {
            // TODO: Add {count} random books to the database
            for (int i = 0; i < count; i++)
            {
                var author = await GetRandomAuthorAsync();
                var book = GetRandomBook(author);

                await _context.Books.AddAsync(book);
            }

            await _context.SaveChangesAsync();

            return StatusCode((int)HttpStatusCode.Created);
        }

        private Book GetRandomBook(Author author) =>
            new Book
            {
                Author = author,
                Title = _nameService.GetWords(3, 7),
                Summary = _nameService.GetWords(15, 100),
                YearPublished = _nameService.GetYear(author.BirthDate.Year + 15)                
            };

        private async Task<Author> GetRandomAuthorAsync()
        {
            var firstName = _nameService.GetFirstName();
            var lastName = _nameService.GetLastName();

            var author = await _context.Authors.Where(a => a.FirstName == firstName && a.LastName == lastName).FirstOrDefaultAsync();
            if (author != null)
            {
                return author;
            }

            return new Author
            {
                LastName = lastName,
                FirstName = firstName,
                BirthDate = _nameService.GetBirthDate(),
            };
        }
    }
}
