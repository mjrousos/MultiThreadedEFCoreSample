using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        const int ParallelizationFactor = 16;

        private readonly BookContext _context;
        private readonly NameService _nameService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BooksController> _logger;

        public BooksController(BookContext context, NameService nameService, ILogger<BooksController> logger, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _nameService = nameService;
            _scopeFactory = scopeFactory;
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

            await ParallelizeAsync(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    // By retrieving DbContext from a nested scope, we will get a unique
                    // instance per thread worker (even if the DbContext is not registered as transient)
                    var scopedContext = scope.ServiceProvider.GetRequiredService<BookContext>();

                    // Alternatively, a developer can have complete control over their DbContexts by creating them manually
                    // (as opposed to via dependency injection)
                    var alternativeContext = new BookContext(scope.ServiceProvider.GetRequiredService<DbContextOptions<BookContext>>());

                    while (Interlocked.Decrement(ref count) >= 0)
                    {
                        var id = numGen.Next(bookCount);
                        var book = await scopedContext.Books
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
                }
            });

            return Ok(books.ToArray());
        }

        // POST api/books/{count}
        [HttpPost("{count}")]
        public async Task<ActionResult> Post([FromRoute] int count)
        {
            await ParallelizeAsync(async () =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedContext = scope.ServiceProvider.GetRequiredService<BookContext>();

                    while (Interlocked.Decrement(ref count) >= 0)
                    {
                        var author = await GetRandomAuthorAsync(scopedContext);
                        var book = GetRandomBook(author);

                        await scopedContext.Books.AddAsync(book);
                        await scopedContext.SaveChangesAsync();
                    }
                }
            });

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

        private async Task<Author> GetRandomAuthorAsync(BookContext context)
        {
            var firstName = _nameService.GetFirstName();
            var lastName = _nameService.GetLastName();

            var author = await context.Authors.Where(a => a.FirstName == firstName && a.LastName == lastName).FirstOrDefaultAsync();
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

        private Task ParallelizeAsync(Func<Task> actionAsync)
        {
#if PARALLELIZE
            var tasks = new List<Task>();
            for (var i = 0; i < ParallelizationFactor; i++)
            {
                tasks.Add(actionAsync());
            }

            return Task.WhenAll(tasks);
#else
            return actionAsync();
#endif
        }
    }
}
