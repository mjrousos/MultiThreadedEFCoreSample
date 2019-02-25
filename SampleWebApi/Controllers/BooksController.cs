using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly DbContextOptions<BookContext> _contextOptions;
        private readonly NameService _nameService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IServiceProvider _serviceProvider;

        public BooksController(BookContext context,
                               DbContextOptions<BookContext> contextOptions,
                               NameService nameService,
                               IServiceProvider serviceProvider,
                               IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _contextOptions = contextOptions;
            _nameService = nameService;
            _scopeFactory = scopeFactory;
            _serviceProvider = serviceProvider;
        }


        // POST api/books/broken/{count}
        // Adds and then removes a number of books (specified by the count parameter)
        // This API uses the DbContext on multiple threads and, therefore, will fail.
        [HttpPost("broken/{count}")]
        public async Task<ActionResult> ModifyBooks([FromRoute] int count)
        {
            await ParallelizeAsync(async () =>
            {
                while (Interlocked.Decrement(ref count) >= 0)
                {
                    // Add a new book
                    var author = await GetRandomAuthorAsync(_context);
                    var book = GetRandomBook(author);
                    var addedBook = await _context.Books.AddAsync(book);
                    await _context.SaveChangesAsync();

                    // Remove the book
                    _context.Remove(addedBook.Entity);
                    await _context.SaveChangesAsync();
                }
            });

            return Ok();
        }

        // POST api/books/fix1/{count}
        // Adds and then removes a number of books (specified by the count parameter)
        // This API fixes the EF Context multithreaded issue by using a transient DB context
        // and getting a separate instance for each execution task.
        [HttpPost("fix1/{count}")]
        public async Task<ActionResult> ModifyBooksFix1([FromRoute] int count)
        {
            await ParallelizeAsync(async () =>
            {
                // Using a separate (transient) context for each parallel worker prevents
                // issues from using a single context in parallel on multiple threads.
                var transientContext = _serviceProvider.GetRequiredService<BookContext>();

                while (Interlocked.Decrement(ref count) >= 0)
                {
                    // Add a new book
                    var author = await GetRandomAuthorAsync(transientContext);
                    var book = GetRandomBook(author);
                    var addedBook = await transientContext.Books.AddAsync(book);
                    await transientContext.SaveChangesAsync();

                    // Remove the book
                    transientContext.Remove(addedBook.Entity);
                    await transientContext.SaveChangesAsync();
                }
            });

            return Ok();
        }

        // POST api/books/fix2/{count}
        // Adds and then removes a number of books (specified by the count parameter)
        // This API fixes the EF Context multithreaded issue by creating separate contexts
        // for each worker using shared DbContext options from DI.
        [HttpPost("fix2/{count}")]
        public async Task<ActionResult> ModifyBooksFix2([FromRoute] int count)
        {
            await ParallelizeAsync(async () =>
            {
                // DbContext options can be safely shared between threads,
                // so individual workers can create their own contexts using 
                // shared options from DI.
                var newContext = new BookContext(_contextOptions);

                while (Interlocked.Decrement(ref count) >= 0)
                {
                    // Add a new book
                    var author = await GetRandomAuthorAsync(newContext);
                    var book = GetRandomBook(author);
                    var addedBook = await newContext.Books.AddAsync(book);
                    await newContext.SaveChangesAsync();

                    // Remove the book
                    newContext.Remove(addedBook.Entity);
                    await newContext.SaveChangesAsync();
                }
            });

            return Ok();
        }

        // POST api/books/fix3/{count}
        // Adds and then removes a number of books (specified by the count parameter)
        // This API fixes the EF Context multithreaded issue by using using
        // separate DI scopes for each parallel worker.
        [HttpPost("fix3/{count}")]
        public async Task<ActionResult> ModifyBooksFix3([FromRoute] int count)
        {
            await ParallelizeAsync(async () =>
            {
                // Creating a sub-scope allows separate DB contexts to be used
                // (even if BookContext is registered with Scoped lifetime, unlike Fix #1).
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedContext = scope.ServiceProvider.GetRequiredService<BookContext>();

                    while (Interlocked.Decrement(ref count) >= 0)
                    {
                        // Add a new book
                        var author = await GetRandomAuthorAsync(scopedContext);
                        var book = GetRandomBook(author);
                        var addedBook = await scopedContext.Books.AddAsync(book);
                        await scopedContext.SaveChangesAsync();

                        // Remove the book
                        scopedContext.Remove(addedBook.Entity);
                        await scopedContext.SaveChangesAsync();
                    }
                }
            });

            return Ok();
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
                tasks.Add(Task.Run(() => actionAsync()));
            }

            return Task.WhenAll(tasks);
#else
            return actionAsync();
#endif
        }
    }
}
