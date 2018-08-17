using Microsoft.EntityFrameworkCore;
using SampleWebApi.Models;

namespace SampleWebApi.Data
{
    public class BookContext : DbContext
    {
        public BookContext(DbContextOptions<BookContext> options)
            : base(options)
        { }

        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>()
                .ToTable("Book")
                .HasOne(b => b.Author).WithMany(a => a.Books);

            modelBuilder.Entity<Author>().ToTable("Author");
        }
    }
}
