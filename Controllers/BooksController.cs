using Microsoft.AspNetCore.Mvc;
using Library.Data;
using Library.Models;
using Library.DTO;
using Microsoft.EntityFrameworkCore;
using Nest;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Library.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        private readonly IElasticClient _elasticClient;

        public BooksController(LibraryDbContext context, IElasticClient elasticClient)
        {
            _context = context;
            _elasticClient = elasticClient;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Books>>> GetBooks()
        {
            return await _context.Books.ToListAsync();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] // Только админы могут добавлять книги
        public async Task<ActionResult<Books>> AddBook(Books book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            var bookSearchDto = new BookSearchDto
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                PublicationYear = book.PublicationYear
            };

            var indexResponse = await _elasticClient.IndexDocumentAsync(bookSearchDto);
            if (!indexResponse.IsValid)
            {
                return BadRequest(indexResponse.OriginalException.Message);
            }

            return CreatedAtAction(nameof(GetBooks), new { id = book.Id }, book);
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<BookSearchDto>>> SearchBooks(string query)
        {
            var searchResponse = await _elasticClient.SearchAsync<BookSearchDto>(s => s
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(f => f.Field(f => f.Title).Field(f => f.Author).Field(f => f.ISBN))
                        .Query(query)
                    )
                )
            );

            if (!searchResponse.IsValid)
            {
                return BadRequest(searchResponse.OriginalException.Message);
            }

            return Ok(searchResponse.Documents);
        }
    }
}